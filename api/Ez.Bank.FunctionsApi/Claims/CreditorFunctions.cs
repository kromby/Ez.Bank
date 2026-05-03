using System.Net;
using System.Text.Json;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.FunctionsApi.Extensions;
using Ez.Bank.FunctionsApi.Models.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ez.Bank.FunctionsApi.Claims;

public class CreditorFunctions
{
    private readonly ICreditorDataAccess _creditorDataAccess;

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CreditorFunctions(ICreditorDataAccess creditorDataAccess)
    {
        _creditorDataAccess = creditorDataAccess;
    }

    [Function("RegisterCreditor")]
    public async Task<HttpResponseData> RegisterCreditor(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "creditors")] HttpRequestData req,
        FunctionContext context)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<CreateCreditorRequest>(body!, DeserializeOptions)!;

        var creditor = new Creditor
        {
            Kennitala = request.Kennitala,
            Name = request.Name,
            BankId = string.IsNullOrWhiteSpace(request.BankId) ? caller.BankId : request.BankId,
            AccountNumber = request.AccountNumber,
            InsertedBy = caller.UserId
        };

        await _creditorDataAccess.InsertAsync(creditor);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(creditor);
        return response;
    }

    [Function("GetCreditor")]
    public async Task<HttpResponseData> GetCreditor(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "creditors/{id}")] HttpRequestData req,
        string id)
    {
        var creditor = await _creditorDataAccess.GetByIdAsync(id);

        if (creditor == null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(creditor);
        return response;
    }
}
