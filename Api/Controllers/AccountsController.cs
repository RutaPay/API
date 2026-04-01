using Api.Data;
using Api.Dtos.User;
using Api.Interfaces;
using Api.Models;
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
        private readonly ILookupNormalizer _keyNormalizer;
        private readonly ApplicationDBContext _context;

        public AccountsController(UserManager<User> userManager, ITokenService tokenService, SignInManager<User> signInManager, ILookupNormalizer keyNormalizer, ApplicationDBContext dBContext)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _keyNormalizer = keyNormalizer;
            _context = dBContext;
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
            var cardBalance = User.FindFirstValue("CardBalance");
            var points = User.FindFirstValue("Points");
            var createdOn = User.FindFirstValue("CreatedOn");
            var accType = _userManager.GetRolesAsync(await _userManager.FindByNameAsync(userName)).Result.FirstOrDefault();

            return Ok(new 
            {
                UserName = userName,
                FullName = fullName,
                LastNames = lastNames,
                Email = email,
                PhoneNumber = phoneNumber,
                CardUID = cardUID,
                CardBalance = cardBalance,
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

            var normalizedEmail = _keyNormalizer.NormalizeEmail(loginDto.Email);
            var user = await _userManager.Users
                .Include(u => u.Card)
                .Include(u => u.Point)
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);
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

            Response.Cookies.Append("token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,   // Requires HTTPS
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Ok(new NewUserDto
            {
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    //Token = _tokenService.CreateToken(user)
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
            Response.Cookies.Delete("token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });
            return Ok();
        }

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
        public async Task<ActionResult> GetUserById([FromRoute]string id)
        {
            var user = _userManager.FindByIdAsync(id);
            return Ok(user);
        }

        // POST api/account/5/addrole
        [HttpPost]
        [Route("{id}/updaterole")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> AddRole(RoleChangeDto roleChange)
        {
            var roles = _userManager.GetRolesAsync;

            return Ok();
        }

        // PUT api/account/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/account/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
