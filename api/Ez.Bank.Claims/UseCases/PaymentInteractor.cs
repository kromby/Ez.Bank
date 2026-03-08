using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;

namespace Ez.Bank.Claims.UseCases;

public class PaymentInteractor
{
    private readonly IClaimDataAccess _claimDataAccess;
    private readonly IPaymentDataAccess _paymentDataAccess;

    private static readonly HashSet<ClaimStatus> PayableStatuses = new()
    {
        ClaimStatus.Sent,
        ClaimStatus.Viewed,
        ClaimStatus.PartiallyPaid,
        ClaimStatus.Overdue
    };

    public PaymentInteractor(
        IClaimDataAccess claimDataAccess,
        IPaymentDataAccess paymentDataAccess)
    {
        _claimDataAccess = claimDataAccess;
        _paymentDataAccess = paymentDataAccess;
    }

    public async Task<(Payment Payment, Claim UpdatedClaim)> RecordPaymentAsync(
        string claimId,
        long amount,
        string currencyCode,
        string bankId,
        string paymentReference,
        CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(claimId);
        if (claim == null)
            throw new NotFoundException("Claim", claimId);

        if (!PayableStatuses.Contains(claim.Status))
            throw new ConflictException($"Cannot record payment. Claim status is {claim.Status}.");

        if (amount <= 0)
            throw new BusinessRuleException("Amount must be a positive integer.");

        if (currencyCode != claim.CurrencyCode)
            throw new BusinessRuleException($"Payment currency {currencyCode} does not match claim currency {claim.CurrencyCode}.");

        long remaining = (claim.LateFee > 0 || claim.AccruedInterest > 0)
            ? claim.TotalDue - claim.PaidAmount
            : claim.Amount - claim.PaidAmount;

        if (amount > remaining)
            throw new BusinessRuleException($"Payment of {amount} exceeds remaining balance of {remaining}.");

        var previousStatus = claim.Status;
        claim.PaidAmount += amount;

        long totalOwed = (claim.LateFee > 0 || claim.AccruedInterest > 0) ? claim.TotalDue : claim.Amount;
        claim.Status = claim.PaidAmount >= totalOwed ? ClaimStatus.Paid : ClaimStatus.PartiallyPaid;
        claim.Updated = DateTime.UtcNow;
        claim.UpdatedBy = caller.UserId;

        var updated = await _claimDataAccess.UpdateAsync(claim, "*");
        if (!updated)
            throw new ConflictException("Claim balance changed. Please retry.");

        var payment = new Payment
        {
            ClaimId = claimId,
            Amount = amount,
            CurrencyCode = currencyCode,
            BankId = bankId,
            PaymentReference = paymentReference,
            InsertedBy = caller.UserId
        };

        await _paymentDataAccess.InsertAsync(payment);

        if (claim.Status != previousStatus)
        {
            await _claimDataAccess.InsertStatusHistoryAsync(new ClaimStatusHistory
            {
                ClaimId = claim.Id,
                FromStatus = previousStatus,
                ToStatus = claim.Status,
                ChangedBy = caller.UserId
            });
        }

        return (payment, claim);
    }

    public async Task<List<Payment>> GetByClaimIdAsync(string claimId, CallerContext caller)
    {
        return await _paymentDataAccess.GetByClaimIdAsync(claimId);
    }
}
