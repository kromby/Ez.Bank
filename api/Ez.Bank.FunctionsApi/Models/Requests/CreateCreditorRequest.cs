namespace Ez.Bank.FunctionsApi.Models.Requests;

public class CreateCreditorRequest
{
    public string Kennitala { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string BankId { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;
}
