using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;

namespace Ez.Bank.Claims.UseCases;

public record DueCostPreview(
    string ClaimId,
    string ClaimReference,
    long OriginalAmount,
    long LateFee,
    int DaysOverdue,
    double InterestRate,
    long AccruedInterest,
    long TotalDue,
    long PaidAmount,
    long RemainingBalance,
    DateTime CalculatedAt);

public class DueCostInteractor
{
    private readonly IClaimDataAccess _claimDataAccess;
    private readonly ISystemConfigDataAccess _systemConfigDataAccess;

    public DueCostInteractor(
        IClaimDataAccess claimDataAccess,
        ISystemConfigDataAccess systemConfigDataAccess)
    {
        _claimDataAccess = claimDataAccess;
        _systemConfigDataAccess = systemConfigDataAccess;
    }

    public async Task<Claim> CalculateAndPersistAsync(string claimId, CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(claimId);
        if (claim == null)
            throw new NotFoundException("Claim", claimId);

        ValidateClaimForCostCalculation(claim);

        var (lateFee, interestRate) = await GetDefaultsAsync();
        var daysOverdue = (DateTime.UtcNow.Date - claim.DueDate).Days;

        if (claim.LateFee == 0)
            claim.LateFee = lateFee;

        claim.InterestRate = interestRate;
        claim.AccruedInterest = (long)Math.Floor(claim.Amount * (interestRate / 100) * ((double)daysOverdue / 365));
        claim.Updated = DateTime.UtcNow;
        claim.UpdatedBy = caller.UserId;

        await _claimDataAccess.UpdateAsync(claim, "*");
        return claim;
    }

    public async Task<DueCostPreview> PreviewAsync(string claimId, CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(claimId);
        if (claim == null)
            throw new NotFoundException("Claim", claimId);

        ValidateClaimForCostCalculation(claim);

        var (defaultLateFee, defaultInterestRate) = await GetDefaultsAsync();
        var daysOverdue = (DateTime.UtcNow.Date - claim.DueDate).Days;
        var lateFee = claim.LateFee == 0 ? defaultLateFee : claim.LateFee;
        var accruedInterest = (long)Math.Floor(claim.Amount * (defaultInterestRate / 100) * ((double)daysOverdue / 365));
        var totalDue = claim.Amount + lateFee + accruedInterest;

        return new DueCostPreview(
            ClaimId: claim.Id,
            ClaimReference: claim.ClaimReference,
            OriginalAmount: claim.Amount,
            LateFee: lateFee,
            DaysOverdue: daysOverdue,
            InterestRate: defaultInterestRate,
            AccruedInterest: accruedInterest,
            TotalDue: totalDue,
            PaidAmount: claim.PaidAmount,
            RemainingBalance: totalDue - claim.PaidAmount,
            CalculatedAt: DateTime.UtcNow);
    }

    private async Task<(long LateFee, double InterestRate)> GetDefaultsAsync()
    {
        var lateFeeConfig = await _systemConfigDataAccess.GetByKeyAsync("DefaultLateFee");
        var interestConfig = await _systemConfigDataAccess.GetByKeyAsync("DefaultInterestRate");

        var lateFee = long.Parse(lateFeeConfig?.Value ?? "950");
        var interestRate = double.Parse(interestConfig?.Value ?? "12.00");

        return (lateFee, interestRate);
    }

    private static void ValidateClaimForCostCalculation(Claim claim)
    {
        if (claim.Status == ClaimStatus.Paid)
            throw new BusinessRuleException("Claim is already fully paid.");

        if (claim.Status == ClaimStatus.Cancelled)
            throw new BusinessRuleException("Cannot calculate costs for a cancelled claim.");

        if (claim.DueDate >= DateTime.UtcNow)
            throw new BusinessRuleException($"Claim is not overdue. Due date is {claim.DueDate:yyyy-MM-dd}.");
    }
}
