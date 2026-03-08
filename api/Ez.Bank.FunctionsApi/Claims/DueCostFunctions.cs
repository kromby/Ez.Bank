using System.Net;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.FunctionsApi.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ez.Bank.FunctionsApi.Claims;

public class DueCostFunctions
{
    private readonly DueCostInteractor _interactor;

    public DueCostFunctions(DueCostInteractor interactor)
    {
        _interactor = interactor;
    }

    [Function("CalculateDueCosts")]
    public async Task<HttpResponseData> CalculateDueCosts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "claims/{claimId}/calculate-due-costs")] HttpRequestData req,
        FunctionContext context,
        string claimId)
    {
        var caller = context.GetCallerContext();
        var result = await _interactor.CalculateAndPersistAsync(claimId, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("PreviewDueCosts")]
    public async Task<HttpResponseData> PreviewDueCosts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/{claimId}/due-costs")] HttpRequestData req,
        FunctionContext context,
        string claimId)
    {
        var caller = context.GetCallerContext();
        var result = await _interactor.PreviewAsync(claimId, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }
}
