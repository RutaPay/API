using Api.Models;

namespace Api.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
        string CreateToken(User user, IList<string> roles);
    }
}
