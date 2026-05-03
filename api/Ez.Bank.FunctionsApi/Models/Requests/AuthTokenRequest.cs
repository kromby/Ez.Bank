namespace Ez.Bank.FunctionsApi.Models.Requests;

public class AuthTokenRequest
{
    public string Password { get; set; } = string.Empty;
    public string BankId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
}
