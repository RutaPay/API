using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Utilidades para ejercitar la API como lo harían la app del cliente, la
/// terminal y un administrador.
/// </summary>
public static class TestHelpers
{
    public const string DemoTerminalCode = "VAL-NFC-04";
    public const string DemoTerminalCredential = "rutapay-val-nfc-04-demo";

    /// <summary>Registra un usuario nuevo, inicia sesión y (opcional) le recarga saldo.</summary>
    public static async Task<TestUser> RegisterAndLoginAsync(ApiTestFactory factory, decimal initialBalance = 100m)
    {
        var anon = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"test_{suffix}@rutapay.test";
        const string password = "RutaPay!2026";

        // El endpoint usa FullName como UserName: debe ser único y sin espacios.
        var register = await anon.PostAsJsonAsync("/api/account/register", new
        {
            fullName = $"TestUser{suffix}",
            lastNames = "Prueba",
            email,
            password,
            phoneNumber = "4611234567"
        });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());

        var token = await LoginAsync(anon, email, password);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (initialBalance > 0)
        {
            var recharge = await client.PostAsJsonAsync("/api/Cards/UpdateBalance", new
            {
                balance = initialBalance,
                oppType = "recharge",
                paymentMethod = "Card"
            });
            Assert.True(recharge.IsSuccessStatusCode, await recharge.Content.ReadAsStringAsync());
        }

        return new TestUser(client, email, password, token, initialBalance);
    }

    /// <summary>Crea un usuario con rol Admin y devuelve un cliente autenticado.</summary>
    public static async Task<HttpClient> CreateAdminClientAsync(ApiTestFactory factory)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"admin_{suffix}@rutapay.test";
        const string password = "Admin!2026";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = $"Admin{suffix}",
                FullName = $"Admin{suffix}",
                LastNames = "Prueba",
                Email = email,
                PhoneNumber = "4610000000",
                Card = new Card { UID = Guid.NewGuid().ToString(), Balance = 0, State = "Active" },
                Point = new Point { Points = 0 }
            };

            var created = await users.CreateAsync(user, password);
            Assert.True(created.Succeeded,
                string.Join("; ", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, "Admin");
        }

        var anon = factory.CreateClient();
        var token = await LoginAsync(anon, email, password);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Inicia sesión y extrae el JWT de la cookie "token".</summary>
    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/account/login", new { email, password });
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies),
            "El login no devolvió cookies (se esperaba la cookie 'token').");

        var tokenCookie = cookies!.FirstOrDefault(c =>
            c.StartsWith("token=", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(tokenCookie);
        return tokenCookie!.Split(';')[0]["token=".Length..];
    }

    /// <summary>La terminal pide su token de sesión (el que muestra en QR/NFC).</summary>
    public static async Task<string> GetTerminalSessionTokenAsync(
        HttpClient client,
        string terminalCode = DemoTerminalCode,
        string credential = DemoTerminalCredential)
    {
        var response = await client.PostAsJsonAsync("/api/Payments/terminal/session", new
        {
            terminalCode,
            credential
        });
        response.EnsureSuccessStatusCode();

        var payload = await TestHelpers.ReadJsonAsync(response);
        return TestHelpers.ReadString(payload, "token")!;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.Clone();
    }

    public static string? ReadString(JsonElement element, string property)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, property, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.ToString();
            }
        }

        return null;
    }

    /// <summary>Busca el Id de un usuario por correo usando el padrón (solo Admin).</summary>
    public static async Task<string?> GetUserIdByEmailAsync(HttpClient adminClient, string email)
    {
        var response = await adminClient.GetAsync("/api/account/all");
        response.EnsureSuccessStatusCode();

        var users = await ReadJsonAsync(response);
        foreach (var user in users.EnumerateArray())
        {
            if (string.Equals(ReadString(user, "email"), email, StringComparison.OrdinalIgnoreCase))
            {
                return ReadString(user, "id");
            }
        }

        return null;
    }

    /// <summary>Extrae el claim "sub" (Id del usuario) de un JWT.</summary>
    public static string? GetUserIdFromToken(string jwt)
    {
        var parts = jwt.Split('.');
        Assert.True(parts.Length >= 2, "El token no tiene el formato JWT esperado.");

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);
        return ReadString(doc.RootElement, "sub");
    }
}

public sealed record TestUser(
    HttpClient Client,
    string Email,
    string Password,
    string Token,
    decimal InitialBalance);
