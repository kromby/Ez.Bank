using System.Net;
using System.Text.Json;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.FunctionsApi.Extensions;
using Ez.Bank.FunctionsApi.Models.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ez.Bank.FunctionsApi.Claims;

public class PaymentFunctions
{
    private readonly PaymentInteractor _interactor;

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PaymentFunctions(PaymentInteractor interactor)
    {
        _interactor = interactor;
    }

    [Function("RecordPayment")]
    public async Task<HttpResponseData> RecordPayment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "claims/{claimId}/payments")] HttpRequestData req,
        FunctionContext context,
        string claimId)
    {
        var caller = context.GetCallerContext();
        var body = await req.ReadAsStringAsync();
        var request = JsonSerializer.Deserialize<RecordPaymentRequest>(body!, DeserializeOptions)!;

        var (payment, updatedClaim) = await _interactor.RecordPaymentAsync(
            claimId,
            request.Amount,
            request.CurrencyCode,
            request.BankId,
            request.PaymentReference,
            caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            payment.Id,
            payment.ClaimId,
            payment.Amount,
            payment.CurrencyCode,
            payment.PaymentDate,
            payment.BankId,
            payment.PaymentReference,
            claim = new
            {
                updatedClaim.Id,
                updatedClaim.Amount,
                PaidAmount = updatedClaim.PaidAmount,
                RemainingAmount = updatedClaim.RemainingBalance,
                Status = updatedClaim.Status.ToString()
            }
        });
        return response;
    }

    [Function("GetPayments")]
    public async Task<HttpResponseData> GetPayments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "claims/{claimId}/payments")] HttpRequestData req,
        FunctionContext context,
        string claimId)
    {
        var caller = context.GetCallerContext();
        var payments = await _interactor.GetByClaimIdAsync(claimId, caller);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(payments);
        return response;
    }
}
