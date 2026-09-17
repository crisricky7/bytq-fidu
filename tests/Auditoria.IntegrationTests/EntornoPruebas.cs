using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Auditoria.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Auditoria.IntegrationTests;

/// <summary>
/// PostgreSQL efímero (Testcontainers) con el mismo script de esquema que usa docker compose,
/// y la API real levantada en memoria.
/// </summary>
public sealed class EntornoPruebas : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Issuer = "https://sts.windows.net/eval-tenant/";
    public const string Audience = "api://corefid-audit";

    // Clave generada por ejecución: no hay secretos fijos en las pruebas.
    public static readonly string ClaveFirma = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Guid.NewGuid();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string CadenaConexion => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "db", "001_esquema.sql"));
        await EjecutarSqlAsync(script);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Auditoria", CadenaConexion);
        builder.UseSetting("Autenticacion:Issuer", Issuer);
        builder.UseSetting("Autenticacion:Audience", Audience);
        builder.UseSetting("Autenticacion:ClaveFirma", ClaveFirma);
        // El despachador se prueba invocándolo explícitamente para que sea determinista.
        builder.UseSetting("Outbox:Habilitado", "false");
    }

    public HttpClient CrearCliente(params string[] roles)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CrearToken(roles));
        return cliente;
    }

    public static string CrearToken(
        string[] roles, string issuer = Issuer, string audience = Audience,
        DateTime? expira = null, string? clave = null)
    {
        var claims = new List<Claim>
        {
            new("sub", "b3f1a2c4-1111-2222-3333-444455556666"),
            new("preferred_username", "jperez")
        };
        claims.AddRange(roles.Select(r => new Claim("roles", r)));

        var ahora = DateTime.UtcNow;
        var vence = expira ?? ahora.AddMinutes(10);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = vence < ahora ? vence.AddMinutes(-10) : ahora.AddMinutes(-1),
            IssuedAt = vence < ahora ? vence.AddMinutes(-10) : ahora.AddMinutes(-1),
            Expires = vence,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave ?? ClaveFirma)), SecurityAlgorithms.HmacSha256)
        });
    }

    public async Task EjecutarSqlAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<T> EscalarAsync<T>(string sql, params object[] parametros)
    {
        await using var conexion = new NpgsqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        foreach (var p in parametros) comando.Parameters.AddWithValue(p);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    public AsyncServiceScope CrearScope() => Services.CreateAsyncScope();
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionIntegracion : ICollectionFixture<EntornoPruebas>
{
    public const string Nombre = "integracion";
}
