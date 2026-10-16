using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Infrastructure;

namespace Api.Tests;

[Collection("api")]
public class PaymentFlowTests
{
    private readonly ApiTestFactory _factory;

    public PaymentFlowTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FlujoCompleto_CobraElPasajeYEsIdempotente()
    {
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 100m);

        // La terminal obtiene su token de sesión (el que iría en el QR/NFC).
        var terminalToken = await TestHelpers.GetTerminalSessionTokenAsync(user.Client);
        Assert.False(string.IsNullOrWhiteSpace(terminalToken));

        // La app del cliente autoriza el cobro con su propio JWT.
        var authorize = await user.Client.PostAsJsonAsync("/api/Payments/authorize", new
        {
            terminalToken,
            paymentMethod = "QR_Terminal"
        });
        authorize.EnsureSuccessStatusCode();

        var result = await TestHelpers.ReadJsonAsync(authorize);
        Assert.Equal("Valid", TestHelpers.ReadString(result, "status"));
        Assert.Equal(8.00m, result.GetProperty("fareDeducted").GetDecimal());
        Assert.Equal(92.00m, result.GetProperty("remainingBalance").GetDecimal());
        Assert.Equal(TestHelpers.DemoTerminalCode, result.GetProperty("terminalCode").GetString());
        Assert.Equal("UNIDAD-CEL-01", result.GetProperty("busUnit").GetString());

        // Reusar el mismo token no vuelve a cobrar (idempotencia).
        var again = await user.Client.PostAsJsonAsync("/api/Payments/authorize", new
        {
            terminalToken,
            paymentMethod = "QR_Terminal"
        });
        again.EnsureSuccessStatusCode();

        var againResult = await TestHelpers.ReadJsonAsync(again);
        Assert.True(againResult.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(92.00m, againResult.GetProperty("remainingBalance").GetDecimal());
    }

    [Fact]
    public async Task Authorize_SinAutenticacion_Devuelve401()
    {
        var anon = _factory.CreateClient();
        var terminalToken = await TestHelpers.GetTerminalSessionTokenAsync(anon);

        var response = await anon.PostAsJsonAsync("/api/Payments/authorize", new { terminalToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_ConTokenInvalido_Devuelve400()
    {
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 50m);

        var response = await user.Client.PostAsJsonAsync("/api/Payments/authorize", new
        {
            terminalToken = "esto.no.es.un.jwt"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await TestHelpers.ReadJsonAsync(response);
        Assert.Equal("Invalid", TestHelpers.ReadString(result, "status"));
    }

    [Fact]
    public async Task Authorize_SinSaldo_DevuelveInsufficientBalance()
    {
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 0m);
        var terminalToken = await TestHelpers.GetTerminalSessionTokenAsync(user.Client);

        var response = await user.Client.PostAsJsonAsync("/api/Payments/authorize", new { terminalToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await TestHelpers.ReadJsonAsync(response);
        Assert.Equal("InsufficientBalance", TestHelpers.ReadString(result, "status"));
    }

    [Fact]
    public async Task TerminalSession_ConCredencialInvalida_Devuelve401()
    {
        var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode = TestHelpers.DemoTerminalCode,
            credential = "credencial-equivocada"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TerminalSession_DeTerminalInexistente_Devuelve401()
    {
        var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode = "TERMINAL-QUE-NO-EXISTE",
            credential = TestHelpers.DemoTerminalCredential
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
