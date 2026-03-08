namespace Ez.Bank.FunctionsApi.Models.Requests;

public class ModifyClaimRequest
{
    public long? Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? CategoryCode { get; set; }
}
