using System.Security.Claims;
using Api.Interfaces;
using Api.Service;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs
{
    /// <summary>
    /// Hub de tiempo real para pagos.
    /// - La app del cliente se une a su canal personal autenticado.
    /// - La terminal se une al canal de su unidad usando su token de sesión.
    /// </summary>
    public class PaymentHub : Hub
    {
        private readonly ITokenService _tokenService;

        public PaymentHub(ITokenService tokenService)
        {
            _tokenService = tokenService;
        }

        /// <summary>Nombre del grupo personal de un pasajero autenticado.</summary>
        public static string UserGroup(string userId) => $"user-{userId}";

        /// <summary>Nombre del grupo de una terminal.</summary>
        public static string TerminalGroup(Guid terminalId) => $"terminal-{terminalId}";

        /// <summary>
        /// La app del cliente se une a su propio canal. Requiere JWT válido en la
        /// conexión; nadie puede escuchar el canal de otro pasajero.
        /// </summary>
        public async Task JoinUserChannel()
        {
            var userId = Context.UserIdentifier
                         ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("El canal del pasajero requiere autenticación.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            await Clients.Caller.SendAsync(
                "Connection Status",
                $"Canal del pasajero activo: {userId}");
        }

        /// <summary>
        /// La terminal se une a su canal presentando el token de sesión firmado
        /// que generó su frontend para el QR/NFC del viaje.
        /// </summary>
        public async Task JoinTerminalChannel(string sessionToken)
        {
            var principal = _tokenService.GetPrincipalFromTerminalSessionToken(sessionToken);
            if (principal == null)
            {
                throw new HubException("Token de sesión de terminal inválido o expirado.");
            }

            if (principal.FindFirstValue("Purpose") != TokenService.TerminalSessionPurpose)
            {
                throw new HubException("El token no corresponde a una sesión de terminal.");
            }

            var terminalId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(terminalId) || !Guid.TryParse(terminalId, out var terminalGuid))
            {
                throw new HubException("Identidad de terminal no encontrada en el token.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, TerminalGroup(terminalGuid));
            await Clients.Caller.SendAsync(
                "Connection Status",
                $"Terminal {terminalId} conectada a su canal.");
        }

        /// <summary>
        /// Canal por identificador de transacción (compatibilidad con el flujo previo).
        /// </summary>
        public async Task JoinPaymentChannel(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new HubException("Identificador de transacción inválido.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, transactionId);
            await Clients.Group(transactionId).SendAsync(
                "Connection Status",
                $"Usuario {Context.ConnectionId} se ha unido al canal de transacción {transactionId}.");
        }
    }
}
