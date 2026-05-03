using Ez.Bank.Core.Entities;

namespace Ez.Bank.Claims.Entities;

public class Claim : EntityBase
{
    public string ClaimReference { get; set; } = string.Empty;
    public string CreditorId { get; set; } = string.Empty;
    public string DebtorKennitala { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long PaidAmount { get; set; }
    public DateTime DueDate { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Created;
    public string CategoryCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? FinalDueDate { get; set; }
    public long LateFee { get; set; }
    public double InterestRate { get; set; }
    public long AccruedInterest { get; set; }
    public string? CancellationReason { get; set; }
    public string? DisputeReason { get; set; }

    public long TotalDue => Amount + LateFee + AccruedInterest;
    public long RemainingBalance => TotalDue - PaidAmount;
}
