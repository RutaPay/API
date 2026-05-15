using Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs
{
    public class PaymentHub : Hub
    {
        public async Task JoinPaymentChannel(string transactionId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                transactionId
            );
            await Clients.Group(transactionId).SendAsync(
                "Connection Status",
                $"Usuario {Context.ConnectionId} se ha unido al canal de transacción {transactionId}."
            );
        }

        public async Task SendTransactionState(string transactionId, ETransactionState transactionStatus)
        {
            await Clients.Group(transactionId).SendAsync(
                "Transaction Status",
                transactionStatus.ToString()
            );
        }
    }
}
