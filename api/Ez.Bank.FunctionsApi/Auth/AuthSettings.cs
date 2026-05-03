namespace Ez.Bank.FunctionsApi.Auth;

public class AuthSettings
{
    public string Password { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = string.Empty;
    public int JwtExpiryMinutes { get; set; } = 60;
}
