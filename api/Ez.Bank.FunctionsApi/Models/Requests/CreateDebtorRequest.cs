namespace Ez.Bank.FunctionsApi.Models.Requests;

public class CreateDebtorRequest
{
    public string Kennitala { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string BankId { get; set; } = default!;
}
