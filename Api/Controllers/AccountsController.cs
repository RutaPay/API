using Api.Data;
using Api.Dtos.User;
using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<User> _signInManager;

        public AccountsController(UserManager<User> userManager, ITokenService tokenService, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
        }

        // GET: api/account
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/account/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/account/login
        [HttpPost("login")]
        public async Task<ActionResult<User>> Login(LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null) return Unauthorized("Invalid Email");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized("Invalid Password");

            return Ok(new NewUserDto
            {
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Token = _tokenService.CreateToken(user)
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

                var user = new User
                {
                    UserName = registerDto.FullName,
                    FullName = registerDto.FullName,
                    LastNames = registerDto.LastNames,
                    Email = registerDto.Email,
                    PhoneNumber = registerDto.PhoneNumber,
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
                            Token = _tokenService.CreateToken(user)
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

        // PUT api/account/5
        [HttpPut("{id}")]
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
