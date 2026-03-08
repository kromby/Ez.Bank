using System.Net;
using System.Text.Json;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.FunctionsApi.Extensions;
using Ez.Bank.FunctionsApi.Models.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ez.Bank.FunctionsApi.Claims;

public class DebtorFunctions
{
    private readonly IDebtorDataAccess _debtorDataAccess;

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DebtorFunctions(IDebtorDataAccess debtorDataAccess)
    {
        _debtorDataAccess = debtorDataAccess;
    }

    [Function("RegisterDebtor")]
    public async Task<HttpResponseData> RegisterDebtor(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "debtors")] HttpRequestData req,
        FunctionContext context)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<CreateDebtorRequest>(body!, DeserializeOptions)!;

        var debtor = new Debtor
        {
            Kennitala = request.Kennitala,
            Name = request.Name,
            BankId = request.BankId,
            InsertedBy = caller.UserId
        };

        await _debtorDataAccess.InsertAsync(debtor);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(debtor);
        return response;
    }

    [Function("GetDebtor")]
    public async Task<HttpResponseData> GetDebtor(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debtors/{id}")] HttpRequestData req,
        string id)
    {
        var debtor = await _debtorDataAccess.GetByIdAsync(id);

        if (debtor == null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(debtor);
        return response;
    }
}
