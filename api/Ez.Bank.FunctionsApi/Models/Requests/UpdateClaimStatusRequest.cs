namespace Ez.Bank.FunctionsApi.Models.Requests;

public class UpdateClaimStatusRequest
{
    public string Status { get; set; } = default!;
    public string? Reason { get; set; }
}
