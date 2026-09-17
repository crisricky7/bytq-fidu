using System.Text;
using Auditoria.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Auditoria.Api.Configuracion;

public sealed class OpcionesAutenticacion
{
    public const string Seccion = "Autenticacion";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Producción: authority de Entra ID. Las claves RS256 se obtienen del JWKS.</summary>
    public string? Authority { get; set; }

    /// <summary>Solo desarrollo, cuando no hay Authority: clave simétrica HS256.</summary>
    public string? ClaveFirma { get; set; }
}

public static class Autenticacion
{
    public static IServiceCollection AddAutenticacionJwt(this IServiceCollection services, IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection(OpcionesAutenticacion.Seccion).Get<OpcionesAutenticacion>() ?? new();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Mantiene los nombres originales de los claims (roles, preferred_username).
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opciones.Issuer,
                    ValidateAudience = true,
                    ValidAudience = opciones.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles"
                };

                // La diferencia entre desarrollo y Entra ID queda solo en configuración.
                if (!string.IsNullOrWhiteSpace(opciones.Authority))
                {
                    o.Authority = opciones.Authority;
                    o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(opciones.ClaveFirma) || opciones.ClaveFirma.Length < 32)
                        throw new InvalidOperationException(
                            "Configure Autenticacion:Authority (JWKS) o Autenticacion:ClaveFirma (mínimo 32 caracteres).");

                    o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.ClaveFirma));
                    o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
                }
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuditoriaEndpoints.PoliticaEscritura, p => p.RequireAuthenticatedUser().RequireRole("audit.write"))
            .AddPolicy(AuditoriaEndpoints.PoliticaLectura, p => p.RequireAuthenticatedUser().RequireRole("audit.read"));

        return services;
    }
}
