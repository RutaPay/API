using Api.Models;

namespace Api.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
        string CreateToken(User user, IList<string> roles);
        string CreatePaymentToken(decimal amount, string route);
        bool ValidatePaymentToken(string token);
    }
}
