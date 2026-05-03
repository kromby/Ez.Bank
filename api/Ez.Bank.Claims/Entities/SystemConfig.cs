using Ez.Bank.Core.Entities;

namespace Ez.Bank.Claims.Entities;

public class SystemConfig : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
