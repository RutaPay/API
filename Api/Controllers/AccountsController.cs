using Api.Data;
using Api.Dtos.User;
using Api.Interfaces;
using Api.Models;
using Api.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<User> _signInManager;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDBContext _context;
        private readonly IEmailService _emailService;

        public AccountsController(UserManager<User> userManager, ITokenService tokenService, SignInManager<User> signInManager, IUserRepository userRepository, ApplicationDBContext dBContext, IEmailService emailService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _userRepository = userRepository;
            _context = dBContext;
            _emailService = emailService;
        }

        // GET: api/account
        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetCurrentUser()
        {
            var userName = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername);
            var fullName = User.FindFirstValue(ClaimTypes.GivenName);
            var lastNames = User.FindFirstValue(ClaimTypes.Surname);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var phoneNumber = User.FindFirstValue(JwtRegisteredClaimNames.PhoneNumber);
            var cardUID = User.FindFirstValue("CardUID");

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var accType = roles.FirstOrDefault();
            var cardBalance = user.Card?.Balance.ToString("F2");
            var points = user.Point?.Points;
            var createdOn = User.FindFirstValue("CreatedOn");

            return Ok(new 
            {
                UserName = userName,
                FullName = fullName,
                LastNames = lastNames,
                Email = email,
                PhoneNumber = phoneNumber,
                CardUID = cardUID,
                CardBalance = cardBalance,
                CardStatus = user.Card?.State ?? "Active",
                Points = points,
                CreatedOn = createdOn,
                AccountType = accType,
                IsAuthenticated = true
            });
        }

        // POST api/account/login
        [HttpPost("login")]
        public async Task<ActionResult<User>> Login(LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userRepository.GetUserByEmailAsync(loginDto.Email);
            if (user == null) return Unauthorized("Invalid Email");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut) return Unauthorized("Account Locked");
                if (result.IsNotAllowed) return Unauthorized("Email not confirmed or login not allowed");

                return Unauthorized("Invalid Password");
            }

            var roles = await _userManager.GetRolesAsync(user);

            var token = _tokenService.CreateToken(user, roles);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,   // Requires HTTPS
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                MaxAge = TimeSpan.FromDays(7),
                IsEssential = true
            };
            cookieOptions.Extensions.Add("Partitioned");

            Response.Cookies.Append("token", token, cookieOptions);

            return Ok(new NewUserDto
            {
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
            });
        }

        // POST api/account/register
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if(!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existingEmail = await _userManager.FindByEmailAsync(registerDto.Email);
                if (existingEmail != null)
                {
                    return BadRequest("Email already exists.");
                }

                var user = new User
                {
                    UserName = registerDto.FullName,
                    FullName = registerDto.FullName,
                    LastNames = registerDto.LastNames,
                    Email = registerDto.Email,
                    PhoneNumber = registerDto.PhoneNumber,
                    Card = new Card
                    {
                        UID = Guid.NewGuid().ToString(),
                        Balance = 0,
                        State = "Active"
                    },
                    Point = new Point
                    {
                        Points = 0
                    }
                };

                var createUserResult = await _userManager.CreateAsync(user, registerDto.Password);

                if (createUserResult.Succeeded)
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, "User");
                    if (roleResult.Succeeded)
                    {
                        return StatusCode(201, new NewUserDto
                        {
                            UserName = user.UserName,
                            Email = user.Email,
                            PhoneNumber = user.PhoneNumber,
                            //Token = _tokenService.CreateToken(user)
                        });
                    } else
                    {
                        return BadRequest(roleResult.Errors);
                    }
                } else
                {
                    return BadRequest(createUserResult.Errors);
                }

            } catch (Exception e)
            {
                return Problem(e.ToString());
            }
        }

        // POST api/account/logout
        [HttpPost("logout")]
        [Authorize]
        public ActionResult Logout()
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                IsEssential = true
            };
            cookieOptions.Extensions.Add("Partitioned");

            Response.Cookies.Delete("token", cookieOptions);
            return Ok();
        }

        /* GET api/account/Test
        [HttpGet("test")]
        public void Test()
        {
            _emailService.SendVerificationEmailAsync("a", "a");
        }*/

        //GET api/account/all
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserForAdminDto>>> GetAllUsers()
        {
            var users = await (
                from user in _context.Users
                join userRole in _context.UserRoles on user.Id equals userRole.UserId
                join role in _context.Roles on userRole.RoleId equals role.Id
                select new UserForAdminDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    FullName = user.FullName,
                    LastNames = user.LastNames,
                    AccountType = role.Name,
                    CreatedOn = user.CreatedOn,
                    CardId = user.Card.UID,
                    CardBalance = user.Card.Balance,
                    CardStatus = user.Card.State,
                    Points = user.Point.Points,
                }).ToListAsync();

            return Ok(users);
        }

        // GET api/account/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetUserById([FromRoute] string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("Usuario no encontrado.");
            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                user.Id,
                user.UserName,
                user.FullName,
                user.LastNames,
                user.Email,
                user.PhoneNumber,
                Roles = roles
            });
        }

        // POST api/account/{id}/updaterole
        [HttpPost("{id}/updaterole")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> AddRole([FromRoute] string id, [FromBody] RoleChangeDto roleChange)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("Usuario no encontrado.");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            var result = await _userManager.AddToRoleAsync(user, roleChange.RoleId);
            if (result.Succeeded)
            {
                return Ok(new { Message = "Rol de usuario actualizado exitosamente.", Role = roleChange.RoleId });
            }

            return BadRequest(result.Errors);
        }

        // DELETE api/account/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteUser([FromRoute] string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "Usuario no encontrado." });
            }

            // Un administrador no puede eliminar su propia cuenta (evita quedarse sin acceso al panel).
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentEmail = User.FindFirstValue(ClaimTypes.Email);
            var isSelf = (!string.IsNullOrEmpty(currentUserId) && currentUserId == user.Id)
                         || (!string.IsNullOrEmpty(currentEmail)
                             && string.Equals(currentEmail, user.Email, StringComparison.OrdinalIgnoreCase));

            if (isSelf)
            {
                return BadRequest(new { Message = "No puedes eliminar tu propia cuenta de administrador." });
            }

            try
            {
                // La base de datos elimina en cascada la tarjeta, los puntos, las transacciones,
                // las solicitudes de verificación y las recompensas asociadas al usuario.
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        Message = "No se pudo eliminar el usuario.",
                        Errors = result.Errors.Select(e => e.Description)
                    });
                }

                return Ok(new
                {
                    Message = $"Usuario {user.Email} eliminado correctamente.",
                    UserId = user.Id
                });
            }
            catch (DbUpdateException ex)
            {
                return Problem(
                    title: "No se pudo eliminar el usuario por datos relacionados.",
                    detail: ex.InnerException?.Message ?? ex.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        }
    }
}
