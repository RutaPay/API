using Api.Data;
using Api.Dtos.Card;
using Api.Interfaces;
using Api.Models;
using Api.Repository;
using Microsoft.AspNetCore.Authorization;
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

        public CardsController(IUserRepository userRepository, ApplicationDBContext context)
        {
            _userRepository = userRepository;
            _context = context;
        }

        /* GET: api/Cards
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Card>>> GetCards()
        {
            return await _context.Cards.ToListAsync();
        }*/

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
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Route("UpdateBalance")]
        [Authorize]
        public async Task<ActionResult> UpdateBalance([FromBody] BalanceChangeDto balanceChangeDto)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var user = await _userRepository.GetUserByEmailAsync(email);

            var cardModel = await _context.Cards.FindAsync(user.Id);

            if (cardModel == null) return NotFound();

            if(balanceChangeDto.OppType == "recharge")
            {
                cardModel.Balance += balanceChangeDto.Balance;
            }
            else if (balanceChangeDto.OppType == "payment")
            {
                if (cardModel.Balance < balanceChangeDto.Balance)
                {
                    return BadRequest("Insufficient balance");
                }
                cardModel.Balance -= balanceChangeDto.Balance;
            }
            else
            {
                return BadRequest("Invalid operation type");
            }

            try
            {
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }

        }

        /* POST: api/Cards
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Card>> PostCard(Card card)
        {
            _context.Cards.Add(card);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (CardExists(card.UserID))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return CreatedAtAction("GetCard", new { id = card.UserID }, card);
        }*/

        /* DELETE: api/Cards/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCard(string id)
        {
            var card = await _context.Cards.FindAsync(id);
            if (card == null)
            {
                return NotFound();
            }

            _context.Cards.Remove(card);
            await _context.SaveChangesAsync();

            return NoContent();
        }*/

        private bool CardExists(string id)
        {
            return _context.Cards.Any(e => e.UserID == id);
        }
    }
}
