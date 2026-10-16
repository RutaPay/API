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

        /// <summary>
        /// Genera el token de sesión firmado de una terminal. Es el valor que su
        /// frontend muestra en el QR y emite por NFC para que la app del cliente
        /// lo verifique al autorizar el cobro.
        /// </summary>
        string CreateTerminalSessionToken(Terminal terminal, string sessionId, int lifetimeSeconds = 180);

        /// <summary>Valida el token de sesión de una terminal y devuelve sus claims.</summary>
        System.Security.Claims.ClaimsPrincipal? GetPrincipalFromTerminalSessionToken(string token);
    }
}
