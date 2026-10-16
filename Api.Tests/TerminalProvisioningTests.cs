using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Infrastructure;

namespace Api.Tests;

[Collection("api")]
public class TerminalProvisioningTests
{
    private readonly ApiTestFactory _factory;

    public TerminalProvisioningTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_CreaTerminal_YEstaPuedeEmitirSesion()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var code = $"VAL-TEST-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var create = await admin.PostAsJsonAsync("/api/Terminals", new
        {
            code,
            busUnitId = "UNIDAD-CEL-99",
            routeName = "Ruta de Prueba"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await TestHelpers.ReadJsonAsync(create);
        var credential = TestHelpers.ReadString(created, "credential");
        Assert.False(string.IsNullOrWhiteSpace(credential));

        // La credencial recién emitida sirve para abrir una sesión de terminal.
        var anon = _factory.CreateClient();
        var terminalToken = await TestHelpers.GetTerminalSessionTokenAsync(anon, code, credential!);
        Assert.False(string.IsNullOrWhiteSpace(terminalToken));

        // Queda listada y asociada a su unidad de transporte.
        var list = await admin.GetAsync($"/api/Terminals/by-bus-unit/UNIDAD-CEL-99");
        list.EnsureSuccessStatusCode();
        var terminals = await TestHelpers.ReadJsonAsync(list);
        Assert.Contains(terminals.EnumerateArray(), t => TestHelpers.ReadString(t, "code") == code);
    }

    [Fact]
    public async Task CrearTerminal_SinSerAdmin_Devuelve403()
    {
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 0m);

        var response = await user.Client.PostAsJsonAsync("/api/Terminals", new
        {
            code = "VAL-NO-PERMITIDA",
            busUnitId = "UNIDAD-CEL-00"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RotarCredencial_InvalidaLaAnterior()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var code = $"VAL-ROT-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var create = await admin.PostAsJsonAsync("/api/Terminals", new
        {
            code,
            busUnitId = "UNIDAD-CEL-98"
        });
        create.EnsureSuccessStatusCode();

        var created = await TestHelpers.ReadJsonAsync(create);
        var oldCredential = TestHelpers.ReadString(created, "credential")!;
        var id = created.GetProperty("id").GetString();

        var rotate = await admin.PostAsync($"/api/Terminals/{id}/rotate-credential", null);
        rotate.EnsureSuccessStatusCode();
        var rotated = await TestHelpers.ReadJsonAsync(rotate);
        var newCredential = TestHelpers.ReadString(rotated, "credential")!;
        Assert.NotEqual(oldCredential, newCredential);

        var anon = _factory.CreateClient();

        var withOld = await anon.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode = code,
            credential = oldCredential
        });
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);

        var withNew = await anon.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode = code,
            credential = newCredential
        });
        Assert.True(withNew.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DesactivarTerminal_ImpideEmitirSesionYAutorizar()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var code = $"VAL-OFF-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var create = await admin.PostAsJsonAsync("/api/Terminals", new
        {
            code,
            busUnitId = "UNIDAD-CEL-97"
        });
        create.EnsureSuccessStatusCode();
        var created = await TestHelpers.ReadJsonAsync(create);
        var credential = TestHelpers.ReadString(created, "credential")!;
        var id = created.GetProperty("id").GetString();

        var anon = _factory.CreateClient();

        // Antes de desactivar, la terminal puede emitir su sesión.
        var before = await anon.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode = code,
            credential
        });
        before.EnsureSuccessStatusCode();
        var beforeJson = await TestHelpers.ReadJsonAsync(before);
        var terminalToken = TestHelpers.ReadString(beforeJson, "token")!;

        var patch = await admin.PatchAsJsonAsync($"/api/Terminals/{id}/state", new { state = "Inactive" });
        patch.EnsureSuccessStatusCode();

        var after = await anon.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode = code,
            credential
        });
        Assert.Equal(HttpStatusCode.BadRequest, after.StatusCode);

        // Un token emitido antes de desactivar tampoco autoriza el cobro.
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 50m);
        var authorize = await user.Client.PostAsJsonAsync("/api/Payments/authorize", new
        {
            terminalToken
        });
        Assert.Equal(HttpStatusCode.BadRequest, authorize.StatusCode);
    }
}
