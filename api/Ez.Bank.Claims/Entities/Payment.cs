using Ez.Bank.Core.Entities;

namespace Ez.Bank.Claims.Entities;

public class Payment : EntityBase
{
    public string ClaimId { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public long Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string BankId { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
}
