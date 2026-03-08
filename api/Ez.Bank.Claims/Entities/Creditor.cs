using Ez.Bank.Core.Entities;

namespace Ez.Bank.Claims.Entities;

public class Creditor : EntityBase
{
    public string Kennitala { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BankId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}
