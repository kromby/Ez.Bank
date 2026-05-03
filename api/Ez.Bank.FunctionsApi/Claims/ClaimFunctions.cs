using System.Net;
using System.Text.Json;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.FunctionsApi.Extensions;
using Ez.Bank.FunctionsApi.Models.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ez.Bank.FunctionsApi.Claims;

public class ClaimFunctions
{
    private readonly ClaimInteractor _interactor;
    private readonly IClaimDataAccess _claimDataAccess;

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ClaimFunctions(ClaimInteractor interactor, IClaimDataAccess claimDataAccess)
    {
        _interactor = interactor;
        _claimDataAccess = claimDataAccess;
    }

    [Function("CreateClaim")]
    public async Task<HttpResponseData> CreateClaim(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "claims")] HttpRequestData req,
        FunctionContext context)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<CreateClaimRequest>(body!, DeserializeOptions)!;

        var claim = await _interactor.CreateAsync(
            request.CreditorId,
            request.DebtorKennitala,
            request.Amount,
            request.CurrencyCode,
            request.DueDate,
            request.CategoryCode,
            request.Description,
            caller);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(claim);
        return response;
    }

    [Function("GetClaim")]
    public async Task<HttpResponseData> GetClaim(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/{id}")] HttpRequestData req,
        FunctionContext context,
        string id)
    {
        var caller = context.GetCallerContext();
        var claim = await _interactor.GetByIdAsync(id, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(claim);
        return response;
    }

    [Function("GetClaimsByDebtor")]
    public async Task<HttpResponseData> GetClaimsByDebtor(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/by-debtor/{debtorKennitala}")] HttpRequestData req,
        FunctionContext context,
        string debtorKennitala)
    {
        var caller = context.GetCallerContext();
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var pageSize = int.TryParse(query["pageSize"], out var ps) ? ps : 50;
        var continuationToken = query["continuationToken"];

        var result = await _interactor.GetByDebtorAsync(debtorKennitala, pageSize, continuationToken, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("GetClaimsByCreditor")]
    public async Task<HttpResponseData> GetClaimsByCreditor(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/by-creditor/{creditorId}")] HttpRequestData req,
        FunctionContext context,
        string creditorId)
    {
        var caller = context.GetCallerContext();
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var statusStr = query["status"];
        ClaimStatus? status = Enum.TryParse<ClaimStatus>(statusStr, true, out var s) ? s : null;
        var pageSize = int.TryParse(query["pageSize"], out var ps) ? ps : 50;
        var continuationToken = query["continuationToken"];

        var result = await _interactor.GetByCreditorAsync(creditorId, status, pageSize, continuationToken, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("UpdateClaimStatus")]
    public async Task<HttpResponseData> UpdateClaimStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "claims/{id}/status")] HttpRequestData req,
        FunctionContext context,
        string id)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<UpdateClaimStatusRequest>(body!, DeserializeOptions)!;

        if (!Enum.TryParse<ClaimStatus>(request.Status, true, out var newStatus))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = $"Invalid status: {request.Status}" });
            return badRequest;
        }

        var result = await _interactor.UpdateStatusAsync(id, newStatus, request.Reason, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("ModifyClaim")]
    public async Task<HttpResponseData> ModifyClaim(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "claims/{id}")] HttpRequestData req,
        FunctionContext context,
        string id)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<ModifyClaimRequest>(body!, DeserializeOptions)!;

        var result = await _interactor.ModifyAsync(id, request.Amount, request.DueDate, request.Description, request.CategoryCode, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("CancelClaim")]
    public async Task<HttpResponseData> CancelClaim(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "claims/{id}")] HttpRequestData req,
        FunctionContext context,
        string id)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<CancelClaimRequest>(body!, DeserializeOptions)!;

        var result = await _interactor.CancelAsync(id, request.Reason, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("GetClaimHistory")]
    public async Task<HttpResponseData> GetClaimHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/{id}/history")] HttpRequestData req,
        string id)
    {
        var history = await _claimDataAccess.GetHistoryAsync(id);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(history);
        return response;
    }
}
