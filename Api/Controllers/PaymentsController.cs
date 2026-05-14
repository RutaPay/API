using Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentsController : Controller
    {

        private readonly ITokenService _tokenService;

        public PaymentsController(ITokenService tokenService)
        {
            _tokenService = tokenService;
        }
        public IActionResult Index()
        {
            return View();
        }

        // POST: api/Payments/generate
        [HttpPost("{generate}")]
        public async Task<ActionResult> generatePaymentCode(decimal amount, string route) {
            var token = _tokenService.CreatePaymentToken(amount, route);
            return Ok(new {
                Token = token
            });
        }

        // POST: api/Payments/validate
        [HttpPost("{validate}")]
        public async Task<ActionResult> validatePaymentCode(string token)
        {
            var isValid = _tokenService.ValidatePaymentToken(token);
            return isValid ? Ok(new { Status = "Success" }) : BadRequest(new { Status = "Invalid" });
        }
    }
}
