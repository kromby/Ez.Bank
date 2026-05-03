using Ez.Bank.Core.Entities;

namespace Ez.Bank.Claims.Entities;

public class Currency : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
}
