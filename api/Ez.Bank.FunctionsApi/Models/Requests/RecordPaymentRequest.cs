namespace Ez.Bank.FunctionsApi.Models.Requests;

public class RecordPaymentRequest
{
    public long Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public string BankId { get; set; } = default!;
    public string PaymentReference { get; set; } = default!;
}
