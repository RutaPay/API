using System.Security.Cryptography;
using Api.Data;
using Api.Dtos.Payment;
using Api.Models;
using Api.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    /// <summary>
    /// Administración de las terminales de pago instaladas en las unidades de
    /// transporte. Solo accesible por administradores.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class TerminalsController : ControllerBase
    {
        private static readonly string[] ValidStates = { "Active", "Inactive", "Maintenance" };

        private readonly ApplicationDBContext _context;

        public TerminalsController(ApplicationDBContext context)
        {
            _context = context;
        }

        // GET: api/Terminals
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TerminalResponseDto>>> GetAll([FromQuery] string? busUnitId)
        {
            var query = _context.Terminals.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(busUnitId))
            {
                query = query.Where(t => t.BusUnitId == busUnitId);
            }

            var terminals = await query
                .OrderBy(t => t.BusUnitId)
                .ThenBy(t => t.Code)
                .Select(ToDto())
                .ToListAsync();

            return Ok(terminals);
        }

        // GET: api/Terminals/by-bus-unit/UNIDAD-CEL-01
        [HttpGet("by-bus-unit/{busUnitId}")]
        public async Task<ActionResult<IEnumerable<TerminalResponseDto>>> GetByBusUnit(string busUnitId)
        {
            var terminals = await _context.Terminals
                .AsNoTracking()
                .Where(t => t.BusUnitId == busUnitId)
                .OrderBy(t => t.Code)
                .Select(ToDto())
                .ToListAsync();

            return Ok(terminals);
        }

        // GET: api/Terminals/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TerminalResponseDto>> GetById(Guid id)
        {
            var terminal = await _context.Terminals
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(ToDto())
                .FirstOrDefaultAsync();

            if (terminal == null)
            {
                return NotFound(new { Message = "Terminal no encontrada." });
            }

            return Ok(terminal);
        }

        // POST: api/Terminals
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] CreateTerminalRequestDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.BusUnitId))
            {
                return BadRequest(new { Message = "El código de la terminal y la unidad de transporte son requeridos." });
            }

            var code = request.Code.Trim();
            if (await _context.Terminals.AnyAsync(t => t.Code == code))
            {
                return Conflict(new { Message = $"Ya existe una terminal con el código {code}." });
            }

            var (credential, salt, hash) = GenerateCredential();

            var terminal = new Terminal
            {
                Code = code,
                BusUnitId = request.BusUnitId.Trim(),
                RouteName = string.IsNullOrWhiteSpace(request.RouteName) ? null : request.RouteName.Trim(),
                State = "Active",
                CredentialSalt = salt,
                CredentialHash = hash,
                CreatedAt = DateTime.UtcNow
            };

            _context.Terminals.Add(terminal);
            await _context.SaveChangesAsync();

            // La credencial se muestra UNA sola vez: no se puede recuperar después.
            return StatusCode(StatusCodes.Status201Created, new
            {
                terminal.Id,
                terminal.Code,
                terminal.BusUnitId,
                terminal.RouteName,
                terminal.State,
                terminal.CreatedAt,
                Credential = credential
            });
        }

        // POST: api/Terminals/{id}/rotate-credential
        [HttpPost("{id:guid}/rotate-credential")]
        public async Task<ActionResult> RotateCredential(Guid id)
        {
            var terminal = await _context.Terminals.FirstOrDefaultAsync(t => t.Id == id);
            if (terminal == null)
            {
                return NotFound(new { Message = "Terminal no encontrada." });
            }

            var (credential, salt, hash) = GenerateCredential();
            terminal.CredentialSalt = salt;
            terminal.CredentialHash = hash;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                terminal.Id,
                terminal.Code,
                Message = "Credencial rotada. Guarda la nueva credencial, no se volverá a mostrar.",
                Credential = credential
            });
        }

        // PATCH: api/Terminals/{id}/state
        [HttpPatch("{id:guid}/state")]
        public async Task<ActionResult> SetState(Guid id, [FromBody] SetTerminalStateRequestDto request)
        {
            if (request == null || !ValidStates.Contains(request.State))
            {
                return BadRequest(new { Message = "El estado debe ser Active, Inactive o Maintenance." });
            }

            var terminal = await _context.Terminals.FirstOrDefaultAsync(t => t.Id == id);
            if (terminal == null)
            {
                return NotFound(new { Message = "Terminal no encontrada." });
            }

            terminal.State = request.State;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                terminal.Id,
                terminal.Code,
                terminal.State,
                Message = $"Terminal actualizada a {terminal.State}."
            });
        }

        private static System.Linq.Expressions.Expression<Func<Terminal, TerminalResponseDto>> ToDto()
            => t => new TerminalResponseDto
            {
                Id = t.Id,
                Code = t.Code,
                BusUnitId = t.BusUnitId,
                RouteName = t.RouteName,
                State = t.State,
                CreatedAt = t.CreatedAt
            };

        private static (string credential, string salt, string hash) GenerateCredential()
        {
            var credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var salt = HashService.GenerateSalt();
            var hash = HashService.Hash(credential, salt);
            return (credential, salt, hash);
        }
    }
}
