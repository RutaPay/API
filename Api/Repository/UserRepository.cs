using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<User> _userManager;
        private readonly ILookupNormalizer _keyNormalizer;

        public UserRepository(UserManager<User> userManager, ILookupNormalizer keyNormalizer)
        {
            _userManager = userManager;
            _keyNormalizer = keyNormalizer;
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            var normalizedEmail = _keyNormalizer.NormalizeEmail(email);
            var user = await _userManager.Users
                .Include(u => u.Card)
                .Include(u => u.Point)
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);
            return user;
        }
    }
}
