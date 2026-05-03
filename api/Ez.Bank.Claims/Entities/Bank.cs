using Ez.Bank.Core.Entities;

namespace Ez.Bank.Claims.Entities;

public class Bank : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Kennitala { get; set; } = string.Empty;
}
