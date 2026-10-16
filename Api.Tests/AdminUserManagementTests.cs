using System.Net;
using System.Net.Http.Json;
using Api.Tests.Infrastructure;

namespace Api.Tests;

[Collection("api")]
public class AdminUserManagementTests
{
    private readonly ApiTestFactory _factory;

    public AdminUserManagementTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_EliminaUsuario_YaNoPuedeIniciarSesion()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var user = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 50m);

        var userId = await TestHelpers.GetUserIdByEmailAsync(admin, user.Email);
        Assert.False(string.IsNullOrEmpty(userId));

        var response = await admin.DeleteAsync($"/api/account/{userId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El usuario ya no existe: no puede iniciar sesión...
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/account/login", new
        {
            email = user.Email,
            password = user.Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // ...y ya no aparece en el padrón.
        var stillThere = await TestHelpers.GetUserIdByEmailAsync(admin, user.Email);
        Assert.Null(stillThere);
    }

    [Fact]
    public async Task EliminarUsuario_SinSerAdmin_Devuelve403()
    {
        var attacker = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 0m);
        var victim = await TestHelpers.RegisterAndLoginAsync(_factory, initialBalance: 0m);

        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var victimId = await TestHelpers.GetUserIdByEmailAsync(admin, victim.Email);
        Assert.False(string.IsNullOrEmpty(victimId));

        var response = await attacker.Client.DeleteAsync($"/api/account/{victimId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // La víctima sigue existiendo.
        Assert.NotNull(await TestHelpers.GetUserIdByEmailAsync(admin, victim.Email));
    }

    [Fact]
    public async Task Admin_NoPuedeEliminarseASiMismo_Devuelve400()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var ownId = TestHelpers.GetUserIdFromToken(admin.DefaultRequestHeaders.Authorization!.Parameter!);
        Assert.False(string.IsNullOrEmpty(ownId));

        var response = await admin.DeleteAsync($"/api/account/{ownId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EliminarUsuarioInexistente_Devuelve404()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);

        var response = await admin.DeleteAsync($"/api/account/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
