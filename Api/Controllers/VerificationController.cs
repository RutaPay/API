using Api.Data;
using Api.Dtos.Verification;
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
    public class VerificationController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        private readonly UserManager<User> _userManager;

        public VerificationController(ApplicationDBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: api/verification/submit
        [HttpPost("submit")]
        public async Task<ActionResult> SubmitVerification([FromBody] SubmitVerificationDto dto)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return NotFound("Usuario no encontrado");

            var validRoles = new[] { "Student", "Health", "Adult" };
            if (!validRoles.Contains(dto.TargetRole))
            {
                return BadRequest("El tipo de tarjeta preferencial solicitada no es válido.");
            }

            // Revisar si ya tiene una solicitud pendiente
            var existing = await _context.VerificationRequests
                .FirstOrDefaultAsync(v => v.UserID == user.Id && v.Status == "Pending");

            if (existing != null)
            {
                existing.TargetRole = dto.TargetRole;
                existing.ReviewNotes = dto.Notes;
                existing.SubmittedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Solicitud previa actualizada correctamente.", RequestId = existing.Id, Status = existing.Status });
            }

            var request = new VerificationRequest
            {
                UserID = user.Id,
                TargetRole = dto.TargetRole,
                Status = "Pending",
                ReviewNotes = dto.Notes,
                CurpDocumentUrl = "documentos/curp_" + user.Id + ".pdf",
                CredentialDocumentUrl = "documentos/credencial_" + user.Id + ".pdf",
                SubmittedAt = DateTime.UtcNow
            };

            _context.VerificationRequests.Add(request);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                Message = "Solicitud de tarifa preferencial enviada exitosamente para revisión.",
                RequestId = request.Id,
                Status = request.Status,
                TargetRole = request.TargetRole
            });
        }

        // GET: api/verification/my-status
        [HttpGet("my-status")]
        public async Task<ActionResult> GetMyVerificationStatus()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return NotFound("Usuario no encontrado");

            var latestRequest = await _context.VerificationRequests
                .Where(v => v.UserID == user.Id)
                .OrderByDescending(v => v.SubmittedAt)
                .FirstOrDefaultAsync();

            if (latestRequest == null)
            {
                return Ok(new { HasRequest = false, Status = "None" });
            }

            return Ok(new
            {
                HasRequest = true,
                latestRequest.Id,
                latestRequest.TargetRole,
                latestRequest.Status,
                latestRequest.ReviewNotes,
                latestRequest.SubmittedAt,
                latestRequest.ReviewedAt
            });
        }

        // GET: api/verification/all (Solo Admin)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<VerificationResponseDto>>> GetAllRequests()
        {
            var requests = await _context.VerificationRequests
                .Include(v => v.User)
                .OrderByDescending(v => v.SubmittedAt)
                .Select(v => new VerificationResponseDto
                {
                    Id = v.Id,
                    UserId = v.UserID,
                    UserName = v.User != null ? v.User.FullName : "N/A",
                    UserEmail = v.User != null ? v.User.Email! : "N/A",
                    TargetRole = v.TargetRole,
                    Status = v.Status,
                    ReviewNotes = v.ReviewNotes,
                    SubmittedAt = v.SubmittedAt,
                    ReviewedAt = v.ReviewedAt
                })
                .ToListAsync();

            return Ok(requests);
        }

        // POST: api/verification/{id}/review (Solo Admin)
        [HttpPost("{id}/review")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ReviewRequest([FromRoute] Guid id, [FromBody] ReviewVerificationDto reviewDto)
        {
            var request = await _context.VerificationRequests.Include(v => v.User).FirstOrDefaultAsync(v => v.Id == id);
            if (request == null) return NotFound("Solicitud no encontrada.");

            if (reviewDto.Decision != "Approved" && reviewDto.Decision != "Rejected")
            {
                return BadRequest("La decisión debe ser 'Approved' o 'Rejected'.");
            }

            var adminEmail = User.FindFirstValue(ClaimTypes.Email);
            request.Status = reviewDto.Decision;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = adminEmail;
            request.ReviewNotes = reviewDto.Reason;

            if (reviewDto.Decision == "Approved" && request.User != null)
            {
                // Remover roles preferenciales anteriores si los tuviera y añadir el nuevo
                var currentRoles = await _userManager.GetRolesAsync(request.User);
                var preferentials = new[] { "Student", "Health", "Adult" };
                foreach (var r in currentRoles.Where(r => preferentials.Contains(r)))
                {
                    await _userManager.RemoveFromRoleAsync(request.User, r);
                }

                await _userManager.AddToRoleAsync(request.User, request.TargetRole);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"Solicitud {request.Status} exitosamente.",
                RequestId = request.Id,
                Status = request.Status,
                UserEmail = request.User?.Email
            });
        }
    }
}
