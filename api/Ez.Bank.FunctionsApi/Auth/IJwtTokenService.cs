using Ez.Bank.Core;

namespace Ez.Bank.FunctionsApi.Auth;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string bankId, string userRole);
    CallerContext? ValidateToken(string token);
}
