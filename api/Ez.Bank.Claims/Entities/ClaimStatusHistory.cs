namespace Ez.Bank.Claims.Entities;

public class ClaimStatusHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClaimId { get; set; } = string.Empty;
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
