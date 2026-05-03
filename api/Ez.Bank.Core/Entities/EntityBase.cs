namespace Ez.Bank.Core.Entities;

public abstract class EntityBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Inserted { get; set; } = DateTime.UtcNow;
    public string InsertedBy { get; set; } = string.Empty;
    public DateTime? Updated { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
}
