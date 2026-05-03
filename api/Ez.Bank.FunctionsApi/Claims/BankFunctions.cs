using System.Net;
using Ez.Bank.Claims.UseCases.Ports;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

using BankEntity = Ez.Bank.Claims.Entities.Bank;

namespace Ez.Bank.FunctionsApi.Claims;

public class BankFunctions
{
    private readonly IBankDataAccess _bankDataAccess;

    public BankFunctions(IBankDataAccess bankDataAccess)
    {
        _bankDataAccess = bankDataAccess;
    }

    [Function("GetBanks")]
    public async Task<HttpResponseData> GetBanks(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "banks")] HttpRequestData req)
    {
        var banks = await _bankDataAccess.GetAllAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(banks);
        return response;
    }

    [Function("GetBank")]
    public async Task<HttpResponseData> GetBank(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "banks/{id}")] HttpRequestData req,
        string id)
    {
        var bank = await _bankDataAccess.GetByIdAsync(id);

        if (bank == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(bank);
        return response;
    }
}
