using Api.Data;
using Api.Dtos.Payment;
using Api.Hubs;
using Api.Interfaces;
using Api.Models;
using Api.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private const int TerminalSessionSeconds = 180;
        private readonly ITokenService _tokenService;
        private readonly ApplicationDBContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IHubContext<PaymentHub> _paymentHub;

        public PaymentsController(
            ITokenService tokenService,
            ApplicationDBContext context,
            UserManager<User> userManager,
            IHubContext<PaymentHub> paymentHub)
        {
            _tokenService = tokenService;
            _context = context;
            _userManager = userManager;
            _paymentHub = paymentHub;
        }

        // POST: api/payments/terminal/session
        [HttpPost("terminal/session")]
        [AllowAnonymous]
        public async Task<ActionResult> CreateTerminalSession([FromBody] TerminalSessionRequestDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.TerminalCode) ||
                string.IsNullOrWhiteSpace(request.Credential))
            {
                return BadRequest(new { Status = "Invalid", Message = "El código de terminal y su credencial son requeridos." });
            }

            var code = request.TerminalCode.Trim();
            var terminal = await _context.Terminals
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code);

            if (terminal == null || !terminal.VerifyCredential(request.Credential))
            {
                return Unauthorized(new { Status = "Invalid", Message = "Credencial de terminal inválida." });
            }

            if (!string.Equals(terminal.State, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Status = "TerminalInactive", Message = $"La terminal se encuentra {terminal.State}." });
            }

            var sessionId = Guid.NewGuid().ToString();
            var issuedAt = DateTime.UtcNow;
            var token = _tokenService.CreateTerminalSessionToken(terminal, sessionId, TerminalSessionSeconds);

            return Ok(new TerminalSessionResponseDto
            {
                TerminalCode = terminal.Code,
                BusUnitId = terminal.BusUnitId,
                Route = terminal.RouteName,
                SessionId = sessionId,
                Token = token,
                ExpiresInSeconds = TerminalSessionSeconds,
                IssuedAt = issuedAt
            });
        }

        // POST: api/Payments/authorize
        [HttpPost("authorize")]
        [Authorize]
        public async Task<ActionResult> AuthorizePayment([FromBody] AuthorizePaymentRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TerminalToken))
            {
                return BadRequest(new { Status = "Invalid", Message = "El token de la terminal es requerido." });
            }

            // 1. Verificar el token de sesión emitido por la terminal.
            var principal = _tokenService.GetPrincipalFromTerminalSessionToken(request.TerminalToken);
            if (principal == null)
            {
                return BadRequest(new { Status = "Invalid", Message = "Código de terminal inválido o expirado." });
            }

            if (principal.FindFirstValue("Purpose") != TokenService.TerminalSessionPurpose)
            {
                return BadRequest(new { Status = "Invalid", Message = "El código no corresponde a una sesión de terminal." });
            }

            var terminalIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionIdClaim = principal.FindFirstValue("SessionId");
            if (!Guid.TryParse(terminalIdClaim, out var terminalId) ||
                !Guid.TryParse(sessionIdClaim, out var terminalSessionId))
            {
                return BadRequest(new { Status = "Invalid", Message = "El token de la terminal está mal formado." });
            }

            var terminal = await _context.Terminals
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == terminalId);

            if (terminal == null || !string.Equals(terminal.State, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Status = "Invalid", Message = "La terminal no está disponible." });
            }

            // 2. Identificar al pasajero autenticado en la app del cliente.
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                return Unauthorized(new { Status = "Invalid", Message = "Usuario no identificado." });
            }

            if (user.Card == null)
            {
                return NotFound(new { Status = "Invalid", Message = "No cuentas con una tarjeta asociada." });
            }

            // 3. Idempotencia: el mismo pasajero no paga dos veces la misma sesión.
            var alreadyPaid = await _context.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserID == user.Id
                                          && t.TerminalSessionId == terminalSessionId
                                          && t.Status == "Completed");

            if (alreadyPaid != null)
            {
                var reusePayload = new
                {
                    Status = "Valid",
                    AlreadyProcessed = true,
                    Message = "Este código ya fue cobrado para tu tarjeta.",
                    TransactionId = alreadyPaid.Id,
                    alreadyPaid.Reference,
                    PassengerName = user.FullName,
                    FareDeducted = alreadyPaid.Amount,
                    RemainingBalance = user.Card.Balance,
                    Route = alreadyPaid.RouteName,
                    BusUnit = alreadyPaid.BusUnitId,
                    alreadyPaid.TerminalCode,
                    Timestamp = alreadyPaid.CreatedAt
                };

                await NotifyTerminalAsync(terminal, ETransactionState.Completed, BuildTerminalPayload(
                    "Valid", $"Pasaje ya autorizado: {user.FullName}", user.FullName, alreadyPaid.Amount,
                    alreadyPaid.Reference, alreadyPaid.RouteName));

                return Ok(reusePayload);
            }

            // 4. Estado de la tarjeta.
            if (!string.Equals(user.Card.State, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var blocked = new
                {
                    Status = "CardBlocked",
                    Message = $"Tarjeta {user.Card.State}. Contacte a soporte o reactive su tarjeta."
                };

                await NotifyUserAsync(user.Id, ETransactionState.Failed, blocked);
                await NotifyTerminalAsync(terminal, ETransactionState.Failed, BuildTerminalPayload(
                    "CardBlocked", $"Rechazado: tarjeta {user.Card.State}", user.FullName, null, null, terminal.RouteName));

                return BadRequest(blocked);
            }

            // 5. Tarifa según rol preferencial.
            var roles = await _userManager.GetRolesAsync(user);
            var preferentialRole = roles.FirstOrDefault(r => r == "Student" || r == "Health" || r == "Adult");
            var isPreferential = preferentialRole != null;

            decimal fare = isPreferential ? 5.50m : 8.00m;
            int pointsAwarded = isPreferential ? 2 : 1;
            string tariffName = preferentialRole ?? "Ordinaria";

            var finalRoute = !string.IsNullOrWhiteSpace(terminal.RouteName)
                ? terminal.RouteName!
                : (principal.FindFirstValue("Route") ?? "Ruta General");

            var paymentMethod = string.Equals(request.PaymentMethod, "NFC_Terminal", StringComparison.OrdinalIgnoreCase)
                ? "NFC_Terminal"
                : "QR_Terminal";

            Transaction? transaction = null;
            decimal remainingBalance;
            int totalPoints;

            // 6. Cobro y registro atómicos.
            await using (var dbTx = await _context.Database.BeginTransactionAsync())
            {
                // UPDATE condicional: solo descuenta si hay estado válido y saldo suficiente.
                var rows = await _context.Cards
                    .Where(c => c.UserID == user.Id && c.State == "Active" && c.Balance >= fare)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(c => c.Balance, c => c.Balance - fare));

                if (rows == 0)
                {
                    var freshCard = await _context.Cards
                        .AsNoTracking()
                        .FirstAsync(c => c.UserID == user.Id);

                    if (!string.Equals(freshCard.State, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        var blocked = new
                        {
                            Status = "CardBlocked",
                            Message = $"Tarjeta {freshCard.State}. Contacte a soporte o reactive su tarjeta."
                        };
                        await NotifyUserAsync(user.Id, ETransactionState.Failed, blocked);
                        await NotifyTerminalAsync(terminal, ETransactionState.Failed, BuildTerminalPayload(
                            "CardBlocked", $"Rechazado: tarjeta {freshCard.State}", user.FullName, null, null, finalRoute));
                        return BadRequest(blocked);
                    }

                    var insufficient = new
                    {
                        Status = "InsufficientBalance",
                        Message = $"Saldo insuficiente (${freshCard.Balance:F2}). Tarifa requerida: ${fare:F2}",
                        RequiredFare = fare,
                        CurrentBalance = freshCard.Balance
                    };
                    await NotifyUserAsync(user.Id, ETransactionState.Failed, insufficient);
                    await NotifyTerminalAsync(terminal, ETransactionState.Failed, BuildTerminalPayload(
                        "InsufficientBalance", $"Rechazado: saldo insuficiente (${freshCard.Balance:F2})",
                        user.FullName, fare, null, finalRoute));
                    return BadRequest(insufficient);
                }

                // Puntos de lealtad.
                var point = await _context.Points.FirstOrDefaultAsync(p => p.UserID == user.Id);
                if (point == null)
                {
                    _context.Points.Add(new Point { UserID = user.Id, Points = 0 });
                    await _context.SaveChangesAsync();
                }

                await _context.Points
                    .Where(p => p.UserID == user.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.Points, p => p.Points + pointsAwarded));

                remainingBalance = await _context.Cards
                    .AsNoTracking()
                    .Where(c => c.UserID == user.Id)
                    .Select(c => c.Balance)
                    .FirstAsync();

                totalPoints = await _context.Points
                    .AsNoTracking()
                    .Where(p => p.UserID == user.Id)
                    .Select(p => p.Points)
                    .FirstAsync();

                transaction = new Transaction
                {
                    UserID = user.Id,
                    CardUID = user.Card.UID,
                    Type = "TripPayment",
                    Amount = fare,
                    PreviousBalance = remainingBalance + fare,
                    CurrentBalance = remainingBalance,
                    Status = "Completed",
                    RouteName = finalRoute,
                    BusUnitId = terminal.BusUnitId,
                    TerminalId = terminal.Id,
                    TerminalCode = terminal.Code,
                    TerminalSessionId = terminalSessionId,
                    PaymentMethod = paymentMethod,
                    Reference = $"TRIP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();
                await dbTx.CommitAsync();
            }

            var successPayload = new
            {
                Status = "Valid",
                TransactionId = transaction!.Id,
                transaction.Reference,
                PassengerName = user.FullName,
                TariffType = tariffName,
                FareDeducted = fare,
                RemainingBalance = remainingBalance,
                PointsEarned = pointsAwarded,
                TotalPoints = totalPoints,
                Route = finalRoute,
                BusUnit = terminal.BusUnitId,
                TerminalCode = terminal.Code,
                Timestamp = transaction.CreatedAt
            };

            // 7. Notificar a la app del cliente y confirmar en la terminal.
            await NotifyUserAsync(user.Id, ETransactionState.Completed, successPayload);
            await NotifyTerminalAsync(terminal, ETransactionState.Completed, BuildTerminalPayload(
                "Valid",
                $"Pasaje autorizado: {user.FullName} · ${fare:F2}",
                user.FullName, fare, transaction.Reference, finalRoute));

            return Ok(successPayload);
        }

        // Helpers
        private async Task<User?> GetAuthenticatedUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var byId = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Card)
                    .Include(u => u.Point)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                if (byId != null)
                {
                    return byId;
                }
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return null;
            }

            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Card)
                .Include(u => u.Point)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        private Task NotifyUserAsync(string userId, ETransactionState state, object payload)
            => _paymentHub.Clients
                .Group(PaymentHub.UserGroup(userId))
                .SendAsync("Transaction Status", state.ToString(), payload);

        private Task NotifyTerminalAsync(Terminal terminal, ETransactionState state, object payload)
            => _paymentHub.Clients
                .Group(PaymentHub.TerminalGroup(terminal.Id))
                .SendAsync("Terminal Confirmation", state.ToString(), payload);

        private static object BuildTerminalPayload(
            string status,
            string message,
            string? passengerName,
            decimal? fare,
            string? reference,
            string? route)
            => new
            {
                Status = status,
                Message = message,
                PassengerName = passengerName,
                FareDeducted = fare,
                Reference = reference,
                Route = route,
                Timestamp = DateTime.UtcNow
            };
    }
}
