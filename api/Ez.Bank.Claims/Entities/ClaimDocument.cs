namespace Ez.Bank.Claims.Entities;

public class ClaimDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClaimId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
    public bool Deleted { get; set; }
}
