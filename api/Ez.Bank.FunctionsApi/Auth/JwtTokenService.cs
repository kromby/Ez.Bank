using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ez.Bank.Core;
using Microsoft.IdentityModel.Tokens;

namespace Ez.Bank.FunctionsApi.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly AuthSettings _settings;

    public JwtTokenService(AuthSettings settings)
    {
        _settings = settings;
    }

    public string GenerateToken(string userId, string bankId, string userRole)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim("bankId", bankId),
            new Claim(ClaimTypes.Role, userRole),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: "ez-bank-claims",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.JwtExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public CallerContext? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = "ez-bank-claims",
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var bankId = principal.FindFirst("bankId")?.Value;
            var userRole = principal.FindFirst(ClaimTypes.Role)?.Value;

            if (userId is null || bankId is null || userRole is null)
                return null;

            return new CallerContext(userId, bankId, userRole);
        }
        catch
        {
            return null;
        }
    }
}
