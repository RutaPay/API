using Api.Data;
using Api.Dtos.Card;
using Api.Dtos.Transaction;
using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CardsController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDBContext _context;
        private readonly UserManager<User> _userManager;

        public CardsController(IUserRepository userRepository, ApplicationDBContext context, UserManager<User> userManager)
        {
            _userRepository = userRepository;
            _context = context;
            _userManager = userManager;
        }

        // GET: api/Cards/my-card
        [HttpGet("my-card")]
        public async Task<ActionResult> GetMyCard()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null || user.Card == null) return NotFound("Tarjeta no encontrada.");

            return Ok(new
            {
                user.Card.UID,
                user.Card.Balance,
                user.Card.State,
                UserFullName = user.FullName,
                UserEmail = user.Email
            });
        }

        // GET: api/Cards/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Card>> GetCard(string id)
        {
            var card = await _context.Cards.FindAsync(id);

            if (card == null)
            {
                return NotFound();
            }

            return card;
        }

        // POST: api/Cards/UpdateBalance
        [HttpPost]
        [Route("UpdateBalance")]
        public async Task<ActionResult> UpdateBalance([FromBody] BalanceChangeDto balanceChangeDto)
        {
            if (balanceChangeDto.Balance <= 0)
            {
                return BadRequest("El monto debe ser mayor a 0.");
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return NotFound("Usuario no encontrado.");

            var cardModel = await _context.Cards.FindAsync(user.Id);
            if (cardModel == null) return NotFound("Tarjeta no encontrada.");

            decimal previousBalance = cardModel.Balance;
            string transType;
            string refPrefix;

            if (balanceChangeDto.OppType.Equals("recharge", StringComparison.OrdinalIgnoreCase))
            {
                cardModel.Balance += balanceChangeDto.Balance;
                transType = "Recharge";
                refPrefix = "REC";
            }
            else if (balanceChangeDto.OppType.Equals("payment", StringComparison.OrdinalIgnoreCase))
            {
                if (cardModel.State != "Active")
                {
                    return BadRequest($"La tarjeta está {cardModel.State}. No es posible procesar el pago.");
                }

                if (cardModel.Balance < balanceChangeDto.Balance)
                {
                    return BadRequest("Saldo insuficiente");
                }
                cardModel.Balance -= balanceChangeDto.Balance;
                transType = "TripPayment";
                refPrefix = "PAY";
            }
            else
            {
                return BadRequest("Tipo de operación inválido.");
            }

            var paymentMethod = !string.IsNullOrWhiteSpace(balanceChangeDto.PaymentMethod) ? balanceChangeDto.PaymentMethod : "Card";

            var transaction = new Transaction
            {
                UserID = user.Id,
                CardUID = cardModel.UID,
                Type = transType,
                Amount = balanceChangeDto.Balance,
                PreviousBalance = previousBalance,
                CurrentBalance = cardModel.Balance,
                Status = "Completed",
                PaymentMethod = paymentMethod,
                Reference = $"{refPrefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Operación realizada con éxito.",
                    NewBalance = cardModel.Balance,
                    TransactionId = transaction.Id,
                    Reference = transaction.Reference
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }
        }

        // POST: api/Cards/toggle-status
        [HttpPost("toggle-status")]
        public async Task<ActionResult> ToggleCardStatus()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null || user.Card == null) return NotFound("Tarjeta no encontrada.");

            user.Card.State = user.Card.State == "Active" ? "Blocked" : "Active";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = user.Card.State == "Active" ? "Tarjeta activada con éxito." : "Tarjeta bloqueada preventivamente por seguridad.",
                CurrentState = user.Card.State
            });
        }

        // GET: api/Cards/transactions
        [HttpGet("transactions")]
        public async Task<ActionResult<IEnumerable<TransactionDto>>> GetUserTransactions()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return NotFound("Usuario no encontrado.");

            var transactions = await _context.Transactions
                .Where(t => t.UserID == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .Select(t => new TransactionDto
                {
                    Id = t.Id,
                    Type = t.Type,
                    Amount = t.Amount,
                    PreviousBalance = t.PreviousBalance,
                    CurrentBalance = t.CurrentBalance,
                    Status = t.Status,
                    RouteName = t.RouteName,
                    BusUnitId = t.BusUnitId,
                    PaymentMethod = t.PaymentMethod,
                    Reference = t.Reference,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(transactions);
        }

        // GET: api/Cards/all-transactions (Admin audit)
        [HttpGet("all-transactions")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllTransactions()
        {
            var transactions = await _context.Transactions
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .Take(100)
                .Select(t => new
                {
                    t.Id,
                    t.UserID,
                    UserName = t.User != null ? t.User.FullName : "N/A",
                    UserEmail = t.User != null ? t.User.Email : "N/A",
                    t.CardUID,
                    t.Type,
                    t.Amount,
                    t.PreviousBalance,
                    t.CurrentBalance,
                    t.Status,
                    t.RouteName,
                    t.BusUnitId,
                    t.PaymentMethod,
                    t.Reference,
                    t.CreatedAt
                })
                .ToListAsync();

            return Ok(transactions);
        }
    }
}
