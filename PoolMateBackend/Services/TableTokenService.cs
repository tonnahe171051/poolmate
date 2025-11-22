using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PoolMate.Api.Dtos.Tournament;

namespace PoolMate.Api.Services
{
    public sealed class TableTokenOptions
    {
        public const string SectionName = "TableToken";

        public string? Issuer { get; set; }
        public string? Audience { get; set; }
        public string? SigningKey { get; set; }
        public int DefaultLifetimeMinutes { get; set; } = 180;
    }

    public sealed record TableTokenValidationResult(int TableId, int TournamentId, string TokenId, DateTimeOffset ExpiresAt, ClaimsPrincipal Principal);

    public interface ITableTokenService
    {
        TableTokenResponse GenerateToken(int tableId, int tournamentId, TimeSpan? lifetime = null);
        TableTokenValidationResult ValidateToken(string token);
        void RevokeToken(string token);
    }

    public class TableTokenService : ITableTokenService
    {
        private readonly IMemoryCache _cache;
        private TableTokenOptions _options;
        private readonly IConfiguration _configuration;
        private readonly JwtSecurityTokenHandler _tokenHandler = new();
        private const string ScopeValue = "table-scoring";

        public TableTokenService(IMemoryCache cache, IOptionsMonitor<TableTokenOptions> options, IConfiguration configuration)
        {
            _cache = cache;
            _options = options.CurrentValue;
            _configuration = configuration;
            options.OnChange(updated => _options = updated);
        }

        public TableTokenResponse GenerateToken(int tableId, int tournamentId, TimeSpan? lifetime = null)
        {
            var settings = ResolveSettings();
            var now = DateTimeOffset.UtcNow;
            var expires = now.Add(lifetime ?? TimeSpan.FromMinutes(settings.DefaultLifetimeMinutes));

            var claims = new List<Claim>
            {
                new("tableId", tableId.ToString()),
                new("tournamentId", tournamentId.ToString()),
                new("scope", ScopeValue),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };

            var creds = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: settings.Issuer,
                audience: settings.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expires.UtcDateTime,
                signingCredentials: creds);

            return new TableTokenResponse
            {
                Token = _tokenHandler.WriteToken(token),
                ExpiresAt = expires
            };
        }

        public TableTokenValidationResult ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new UnauthorizedAccessException("Table token is required.");

            var settings = ResolveSettings();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(settings.Issuer),
                ValidIssuer = settings.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(settings.Audience),
                ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = settings.SigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwt)
                throw new UnauthorizedAccessException("Invalid token format.");

            var scope = principal.FindFirst("scope")?.Value;
            if (!string.Equals(scope, ScopeValue, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Token scope is invalid.");

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                ?? throw new UnauthorizedAccessException("Token missing identifier.");

            if (IsRevoked(jti))
                throw new UnauthorizedAccessException("Token has been revoked.");

            var tableId = ParseClaim(principal, "tableId");
            var tournamentId = ParseClaim(principal, "tournamentId");

            var expiresAt = DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc);
            return new TableTokenValidationResult(tableId, tournamentId, jti, expiresAt, principal);
        }

        public void RevokeToken(string token)
        {
            var validation = ValidateToken(token);
            var cacheKey = GetRevokeKey(validation.TokenId);
            var expiry = validation.ExpiresAt;
            _cache.Set(cacheKey, true, expiry);
        }

        private static string GetRevokeKey(string jti) => $"table-token:revoked:{jti}";

        private TableTokenSettings ResolveSettings()
        {
            var issuer = _options.Issuer ?? _configuration["JWT:ValidIssuer"];
            var audience = _options.Audience ?? "table-scoring";
            var signingKey = _options.SigningKey ?? _configuration["JWT:Secret"];

            if (string.IsNullOrWhiteSpace(signingKey))
                throw new InvalidOperationException("Signing key for table tokens is not configured.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            return new TableTokenSettings
            {
                Issuer = issuer,
                Audience = audience,
                SigningKey = securityKey,
                DefaultLifetimeMinutes = _options.DefaultLifetimeMinutes > 0 ? _options.DefaultLifetimeMinutes : 180
            };
        }

        private static int ParseClaim(ClaimsPrincipal principal, string name)
        {
            var value = principal.FindFirst(name)?.Value
                ?? throw new UnauthorizedAccessException($"Token missing '{name}' claim.");

            if (!int.TryParse(value, out var parsed))
                throw new UnauthorizedAccessException($"Token claim '{name}' is invalid.");

            return parsed;
        }

        private bool IsRevoked(string jti)
            => _cache.TryGetValue(GetRevokeKey(jti), out _);

        private sealed class TableTokenSettings
        {
            public string? Issuer { get; init; }
            public string? Audience { get; init; }
            public SymmetricSecurityKey SigningKey { get; init; } = default!;
            public int DefaultLifetimeMinutes { get; init; }
        }
    }
}
