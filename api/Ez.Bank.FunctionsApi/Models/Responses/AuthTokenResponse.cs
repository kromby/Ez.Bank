namespace Ez.Bank.FunctionsApi.Models.Responses;

public class AuthTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
