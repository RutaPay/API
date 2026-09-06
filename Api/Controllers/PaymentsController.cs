using Api.Data;
using Api.Dtos.Payment;
using Api.Hubs;
using Api.Interfaces;
using Api.Models;
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

        // POST: api/Payments/generate
        [HttpPost("generate")]
        [Authorize]
        public async Task<ActionResult> GeneratePaymentCode([FromBody] GeneratePaymentRequestDto request)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized("Usuario no identificado");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound("Usuario no encontrado");
            }

            var card = await _context.Cards.FirstOrDefaultAsync(c => c.UserID == user.Id);
            if (card == null)
            {
                return BadRequest("El usuario no cuenta con una tarjeta asociada");
            }

            if (card.State != "Active")
            {
                return BadRequest($"La tarjeta se encuentra {card.State}. No es posible generar pasaje.");
            }

            var transactionId = Guid.NewGuid().ToString();
            var route = string.IsNullOrWhiteSpace(request?.Route) ? "Ruta General Celaya" : request.Route;
            var token = _tokenService.CreateUserPaymentToken(user.Id, route, transactionId);

            return Ok(new
            {
                TransactionId = transactionId,
                Token = token,
                ExpiresInSeconds = 300,
                Route = route,
                UserFullName = user.FullName,
                CardBalance = card.Balance
            });
        }

        // POST: api/Payments/validate
        [HttpPost("validate")]
        public async Task<ActionResult> ValidatePaymentCode([FromBody] ValidatePaymentRequestDto request)
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new { Status = "Invalid", Message = "El token de pago es requerido." });
            }

            var principal = _tokenService.GetPrincipalFromPaymentToken(request.Token);
            if (principal == null)
            {
                return BadRequest(new { Status = "Invalid", Message = "Token de pago inválido o expirado." });
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var routeFromToken = principal.FindFirstValue("Route");
            var transactionId = principal.FindFirstValue("TransactionId") ?? request.SrGroupId ?? Guid.NewGuid().ToString();

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Status = "Invalid", Message = "Identidad de pasajero no encontrada en el token." });
            }

            var user = await _context.Users
                .Include(u => u.Card)
                .Include(u => u.Point)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.Card == null)
            {
                return NotFound(new { Status = "Invalid", Message = "Cuenta o tarjeta de pasajero no encontrada." });
            }

            if (user.Card.State != "Active")
            {
                var blockedResult = new
                {
                    Status = "CardBlocked",
                    Message = $"Tarjeta {user.Card.State}. Contacte a soporte o reactive su tarjeta."
                };

                await _paymentHub.Clients.Group(transactionId).SendAsync("Transaction Status", ETransactionState.Failed.ToString(), blockedResult);
                return BadRequest(blockedResult);
            }

            // Determinar tarifa según rol preferencial
            var roles = await _userManager.GetRolesAsync(user);
            var isPreferential = roles.Any(r => r == "Student" || r == "Health" || r == "Adult");

            decimal fare = isPreferential ? 5.50m : 8.00m;
            int pointsAwarded = isPreferential ? 2 : 1;
            string tariffName = isPreferential ? (roles.FirstOrDefault(r => r == "Student" || r == "Health" || r == "Adult") ?? "Preferencial") : "Ordinaria";

            if (user.Card.Balance < fare)
            {
                var insufficientResult = new
                {
                    Status = "InsufficientBalance",
                    Message = $"Saldo insuficiente (${user.Card.Balance:F2}). Tarifa requerida: ${fare:F2}",
                    RequiredFare = fare,
                    CurrentBalance = user.Card.Balance
                };

                await _paymentHub.Clients.Group(transactionId).SendAsync("Transaction Status", ETransactionState.Failed.ToString(), insufficientResult);
                return BadRequest(insufficientResult);
            }

            // Descuento atómico de saldo y registro
            decimal previousBalance = user.Card.Balance;
            user.Card.Balance -= fare;

            if (user.Point == null)
            {
                user.Point = new Point { UserID = user.Id, Points = 0 };
                _context.Points.Add(user.Point);
            }
            user.Point.Points += pointsAwarded;

            var finalRoute = !string.IsNullOrEmpty(request.Route) ? request.Route : (!string.IsNullOrEmpty(routeFromToken) ? routeFromToken : "Ruta General");
            var busUnit = !string.IsNullOrEmpty(request.BusUnitId) ? request.BusUnitId : "UNIDAD-CEL-01";

            var transaction = new Transaction
            {
                UserID = user.Id,
                CardUID = user.Card.UID,
                Type = "TripPayment",
                Amount = fare,
                PreviousBalance = previousBalance,
                CurrentBalance = user.Card.Balance,
                Status = "Completed",
                RouteName = finalRoute,
                BusUnitId = busUnit,
                PaymentMethod = "QR_Transit",
                Reference = $"TRIP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            var successPayload = new
            {
                Status = "Valid",
                TransactionId = transaction.Id,
                Reference = transaction.Reference,
                PassengerName = user.FullName,
                TariffType = tariffName,
                FareDeducted = fare,
                RemainingBalance = user.Card.Balance,
                PointsEarned = pointsAwarded,
                TotalPoints = user.Point.Points,
                Route = finalRoute,
                BusUnit = busUnit,
                Timestamp = transaction.CreatedAt
            };

            // Notificar instantáneamente vía SignalR
            await _paymentHub.Clients.Group(transactionId).SendAsync("Transaction Status", ETransactionState.Completed.ToString(), successPayload);

            return Ok(successPayload);
        }
    }
}
