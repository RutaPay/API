using Api.Models;

namespace Api.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
        string CreateToken(User user, IList<string> roles);
        string CreatePaymentToken(decimal amount, string route, string transactionId);
        string CreateUserPaymentToken(string userId, string route, string transactionId);
        Task<bool> ValidatePaymentToken(string token);
        System.Security.Claims.ClaimsPrincipal? GetPrincipalFromPaymentToken(string token);
    }
}
