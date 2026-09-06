using Api.Data;
using Api.Dtos.Reward;
using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RewardsController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly IUserRepository _userRepository;

        public RewardsController(ApplicationDBContext context, IUserRepository userRepository)
        {
            _context = context;
            _userRepository = userRepository;
        }

        // GET: api/rewards
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RewardDto>>> GetRewards()
        {
            var rewards = await _context.Rewards
                .Where(r => r.IsActive)
                .Select(r => new RewardDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    Description = r.Description,
                    PointsCost = r.PointsCost,
                    RewardType = r.RewardType,
                    Value = r.Value,
                    IsActive = r.IsActive
                })
                .ToListAsync();

            return Ok(rewards);
        }

        // POST: api/rewards/redeem
        [HttpPost("redeem")]
        public async Task<ActionResult> RedeemReward([FromBody] RedeemRewardDto dto)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null || user.Point == null || user.Card == null)
            {
                return NotFound("Datos del usuario incompletos.");
            }

            var reward = await _context.Rewards.FindAsync(dto.RewardId);
            if (reward == null || !reward.IsActive)
            {
                return NotFound("Recompensa no disponible.");
            }

            if (user.Point.Points < reward.PointsCost)
            {
                return BadRequest($"Puntos insuficientes. Tienes {user.Point.Points} puntos y requieres {reward.PointsCost}.");
            }

            // Deducir puntos
            user.Point.Points -= reward.PointsCost;

            // Si es saldo directo a la tarjeta
            decimal previousBalance = user.Card.Balance;
            if (reward.RewardType == "Balance")
            {
                user.Card.Balance += reward.Value;

                var transaction = new Transaction
                {
                    UserID = user.Id,
                    CardUID = user.Card.UID,
                    Type = "RewardRedemption",
                    Amount = reward.Value,
                    PreviousBalance = previousBalance,
                    CurrentBalance = user.Card.Balance,
                    Status = "Completed",
                    PaymentMethod = "Points_Redeem",
                    Reference = $"RWD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(transaction);
            }

            var code = $"RUTA-{reward.Id}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            var userReward = new UserReward
            {
                UserID = user.Id,
                RewardId = reward.Id,
                Code = code,
                RedeemedAt = DateTime.UtcNow,
                IsUsed = reward.RewardType == "Balance" // Si es saldo, ya se aplicó
            };
            _context.UserRewards.Add(userReward);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"¡Has canjeado '{reward.Title}' exitosamente!",
                RemainingPoints = user.Point.Points,
                NewBalance = user.Card.Balance,
                CouponCode = code,
                RewardType = reward.RewardType
            });
        }

        // GET: api/rewards/my-rewards
        [HttpGet("my-rewards")]
        public async Task<ActionResult<IEnumerable<UserRewardDto>>> GetMyRedeemedRewards()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return NotFound();

            var list = await _context.UserRewards
                .Include(ur => ur.Reward)
                .Where(ur => ur.UserID == user.Id)
                .OrderByDescending(ur => ur.RedeemedAt)
                .Select(ur => new UserRewardDto
                {
                    Id = ur.Id,
                    RewardId = ur.RewardId,
                    Title = ur.Reward != null ? ur.Reward.Title : "Recompensa",
                    RedeemedAt = ur.RedeemedAt,
                    Code = ur.Code,
                    IsUsed = ur.IsUsed
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}
