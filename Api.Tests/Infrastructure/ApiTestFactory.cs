using Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Levanta la API en memoria (TestServer) contra una base de datos SQL Server
/// dedicada y descartable. Por defecto usa SQL Server LocalDB; en CI se apunta a
/// un contenedor/servidor con la variable de entorno RUTAPAY_TEST_CONNECTION.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultTestConnection =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True";

    private readonly string _connectionString;

    public ApiTestFactory()
    {
        // Si viene una cadena base (local o CI), se le cambia únicamente el nombre de la BD.
        var baseConnection = Environment.GetEnvironmentVariable("RUTAPAY_TEST_CONNECTION")
                             ?? DefaultTestConnection;

        var builder = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"RutaPay_Test_{Guid.NewGuid():N}"
        };

        _connectionString = builder.ConnectionString;
    }

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:Issuer"] = "RutaPay-API",
                ["Jwt:Audience"] = "RutaPay-Web/Mobile",
                ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef0123456789abcdef0123456789"
            });
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        await MigrateWithRetryAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (Environment.GetEnvironmentVariable("RUTAPAY_TEST_KEEP_DB") != "1")
        {
            try
            {
                await using var scope = Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
                await db.Database.EnsureDeletedAsync();
            }
            catch
            {
                // La limpieza no debe hacer fallar la corrida de pruebas.
            }
        }

        await base.DisposeAsync();
    }

    private static async Task MigrateWithRetryAsync(ApplicationDBContext db)
    {
        const int attempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch when (attempt < attempts)
            {
                // LocalDB puede tardar en arrancar la primera vez.
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
    }
}
