using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Ez.Bank.FunctionsApi.Scheduling;

public class OverdueProcessingFunction
{
    private readonly IClaimDataAccess _claimDataAccess;
    private readonly ILogger<OverdueProcessingFunction> _logger;

    public OverdueProcessingFunction(IClaimDataAccess claimDataAccess, ILogger<OverdueProcessingFunction> logger)
    {
        _claimDataAccess = claimDataAccess;
        _logger = logger;
    }

    [Function("OverdueProcessing")]
    public async Task Run([TimerTrigger("0 0 1 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Overdue processing started at {Time}", DateTime.UtcNow);

        var claims = await _claimDataAccess.GetOverdueClaimsAsync(500);

        _logger.LogInformation("Found {Count} overdue claims to process", claims.Count);

        foreach (var claim in claims)
        {
            var previousStatus = claim.Status;
            claim.Status = ClaimStatus.Overdue;
            claim.Updated = DateTime.UtcNow;
            claim.UpdatedBy = "system";

            await _claimDataAccess.UpdateAsync(claim, "*");
            await _claimDataAccess.InsertStatusHistoryAsync(new ClaimStatusHistory
            {
                ClaimId = claim.Id,
                FromStatus = previousStatus,
                ToStatus = ClaimStatus.Overdue,
                ChangedBy = "system",
                Reason = "Automatically marked as overdue by scheduled processing"
            });
        }

        _logger.LogInformation("Overdue processing completed. Total claims processed: {Total}", claims.Count);
    }
}
