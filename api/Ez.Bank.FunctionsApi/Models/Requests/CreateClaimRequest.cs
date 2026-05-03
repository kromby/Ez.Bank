namespace Ez.Bank.FunctionsApi.Models.Requests;

public class CreateClaimRequest
{
    public string CreditorId { get; set; } = default!;
    public string DebtorKennitala { get; set; } = default!;
    public long Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public DateTime DueDate { get; set; }
    public string CategoryCode { get; set; } = default!;
    public string? Description { get; set; }
}
