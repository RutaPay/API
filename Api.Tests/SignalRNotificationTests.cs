using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Api.Tests;

[Collection("api")]
public class SignalRNotificationTests
{
    private readonly ApiTestFactory _factory;

    public SignalRNotificationTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AlAutorizar_NotificaAlPasajeroYConfirmaEnLaTerminal()
    {
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 100m);
        var terminalToken = await TestHelpers.GetTerminalSessionTokenAsync(user.Client);

        var userPayload = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalPayload = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Canal del pasajero (autenticado con su JWT).
        await using var userHub = CreateHub(user.Token);
        userHub.On<string, JsonElement>("Transaction Status",
            (_, payload) => userPayload.TrySetResult(payload));
        await userHub.StartAsync();
        await userHub.InvokeAsync("JoinUserChannel");

        // Canal de la terminal (autenticado con su token de sesión).
        await using var terminalHub = CreateHub(accessToken: null);
        terminalHub.On<string, JsonElement>("Terminal Confirmation",
            (_, payload) => terminalPayload.TrySetResult(payload));
        await terminalHub.StartAsync();
        await terminalHub.InvokeAsync("JoinTerminalChannel", terminalToken);

        // La app del cliente autoriza el cobro.
        var authorize = await user.Client.PostAsJsonAsync("/api/Payments/authorize", new
        {
            terminalToken,
            paymentMethod = "NFC_Terminal"
        });
        authorize.EnsureSuccessStatusCode();

        var userUpdate = await WithTimeout(userPayload.Task, TimeSpan.FromSeconds(15));
        var terminalUpdate = await WithTimeout(terminalPayload.Task, TimeSpan.FromSeconds(15));

        Assert.Equal("Valid", TestHelpers.ReadString(userUpdate, "status"));
        Assert.Equal("Valid", TestHelpers.ReadString(terminalUpdate, "status"));
        Assert.Contains("Pasaje autorizado", TestHelpers.ReadString(terminalUpdate, "message") ?? string.Empty);
    }

    [Fact]
    public async Task JoinUserChannel_SinAutenticacion_Falla()
    {
        await using var hub = CreateHub(accessToken: null);
        await hub.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("JoinUserChannel"));
    }

    [Fact]
    public async Task JoinTerminalChannel_ConTokenInvalido_Falla()
    {
        await using var hub = CreateHub(accessToken: null);
        await hub.StartAsync();

        await Assert.ThrowsAsync<HubException>(
            () => hub.InvokeAsync("JoinTerminalChannel", "token-invalido"));
    }

    private HubConnection CreateHub(string? accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "payments/paymentHub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;

                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            })
            .Build();
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.True(completed == task, "No llegó la notificación de SignalR a tiempo.");
        return await task;
    }
}
