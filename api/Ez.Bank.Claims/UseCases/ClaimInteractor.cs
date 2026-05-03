using System.Text.RegularExpressions;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;
using Ez.Bank.Core.Models;

namespace Ez.Bank.Claims.UseCases;

public class ClaimInteractor
{
    private readonly IClaimDataAccess _claimDataAccess;
    private readonly ICreditorDataAccess _creditorDataAccess;
    private readonly IDebtorDataAccess _debtorDataAccess;
    private readonly ICurrencyDataAccess _currencyDataAccess;

    private static readonly HashSet<string> ValidCategoryCodes = new() { "UTILITY", "TELECOM", "LOAN", "OTHER" };

    private static readonly Dictionary<ClaimStatus, HashSet<ClaimStatus>> ValidTransitions = new()
    {
        { ClaimStatus.Created, new HashSet<ClaimStatus> { ClaimStatus.Sent, ClaimStatus.Cancelled } },
        { ClaimStatus.Sent, new HashSet<ClaimStatus> { ClaimStatus.Viewed, ClaimStatus.Overdue, ClaimStatus.Cancelled } },
        { ClaimStatus.Viewed, new HashSet<ClaimStatus> { ClaimStatus.PartiallyPaid, ClaimStatus.Paid, ClaimStatus.Overdue, ClaimStatus.Cancelled, ClaimStatus.Disputed } },
        { ClaimStatus.PartiallyPaid, new HashSet<ClaimStatus> { ClaimStatus.Paid, ClaimStatus.Overdue, ClaimStatus.Cancelled, ClaimStatus.Disputed } },
        { ClaimStatus.Overdue, new HashSet<ClaimStatus> { ClaimStatus.PartiallyPaid, ClaimStatus.Paid, ClaimStatus.Cancelled, ClaimStatus.Disputed } },
        { ClaimStatus.Disputed, new HashSet<ClaimStatus> { ClaimStatus.Sent, ClaimStatus.Cancelled } },
    };

    private static readonly Regex KennitalaRegex = new(@"^\d{10}$", RegexOptions.Compiled);

    public ClaimInteractor(
        IClaimDataAccess claimDataAccess,
        ICreditorDataAccess creditorDataAccess,
        IDebtorDataAccess debtorDataAccess,
        ICurrencyDataAccess currencyDataAccess)
    {
        _claimDataAccess = claimDataAccess;
        _creditorDataAccess = creditorDataAccess;
        _debtorDataAccess = debtorDataAccess;
        _currencyDataAccess = currencyDataAccess;
    }

    public async Task<Claim> CreateAsync(
        string creditorId,
        string debtorKennitala,
        long amount,
        string currencyCode,
        DateTime dueDate,
        string categoryCode,
        string? description,
        CallerContext caller)
    {
        if (caller.UserRole != "Creditor")
            throw new BusinessRuleException("Only creditors can create claims.");

        if (amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");

        if (dueDate <= DateTime.UtcNow.Date)
            throw new BusinessRuleException("Due date must be at least tomorrow.");

        if (!ValidCategoryCodes.Contains(categoryCode))
            throw new BusinessRuleException($"Invalid category code '{categoryCode}'. Valid codes are: {string.Join(", ", ValidCategoryCodes)}.");

        if (description?.Length > 500)
            throw new BusinessRuleException("Description must not exceed 500 characters.");

        if (string.IsNullOrWhiteSpace(debtorKennitala) || !KennitalaRegex.IsMatch(debtorKennitala))
            throw new BusinessRuleException("Kennitala must be exactly 10 digits.");

        var currency = await _currencyDataAccess.GetByCodeAsync(currencyCode);
        if (currency == null)
            throw new BusinessRuleException($"Currency code '{currencyCode}' is not supported.");

        var creditor = await _creditorDataAccess.GetByIdAsync(creditorId);
        if (creditor == null)
            throw new NotFoundException("Creditor", creditorId);

        var debtor = await _debtorDataAccess.GetByKennitalaAsync(debtorKennitala);
        if (debtor == null)
        {
            debtor = new Debtor
            {
                Kennitala = debtorKennitala,
                Name = "Unknown",
                BankId = null,
                InsertedBy = caller.UserId
            };
            await _debtorDataAccess.InsertAsync(debtor);
        }

        var claimReference = await _claimDataAccess.GetNextClaimReferenceAsync();

        var claim = new Claim
        {
            ClaimReference = claimReference,
            CreditorId = creditorId,
            DebtorKennitala = debtorKennitala,
            CurrencyCode = currencyCode,
            Amount = amount,
            DueDate = dueDate,
            CategoryCode = categoryCode,
            Description = description ?? string.Empty,
            InsertedBy = caller.UserId
        };

        await _claimDataAccess.InsertAsync(claim);
        return claim;
    }

    public async Task<Claim> GetByIdAsync(string id, CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(id);
        if (claim == null)
            throw new NotFoundException("Claim", id);

        return claim;
    }

    public async Task<PagedResult<Claim>> GetByDebtorAsync(
        string debtorKennitala,
        int pageSize,
        string? continuationToken,
        CallerContext caller)
    {
        if (string.IsNullOrWhiteSpace(debtorKennitala) || !KennitalaRegex.IsMatch(debtorKennitala))
            throw new BusinessRuleException("Kennitala must be exactly 10 digits.");

        return await _claimDataAccess.GetByDebtorAsync(debtorKennitala, pageSize, continuationToken);
    }

    public async Task<PagedResult<Claim>> GetByCreditorAsync(
        string creditorId,
        ClaimStatus? status,
        int pageSize,
        string? continuationToken,
        CallerContext caller)
    {
        if (caller.UserRole != "Creditor")
            throw new BusinessRuleException("Only creditors can view their claims.");

        return await _claimDataAccess.GetByCreditorAsync(creditorId, status, pageSize, continuationToken);
    }

    public async Task<Claim> UpdateStatusAsync(
        string id,
        ClaimStatus newStatus,
        string? reason,
        CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(id);
        if (claim == null)
            throw new NotFoundException("Claim", id);

        if (!ValidTransitions.TryGetValue(claim.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new ConflictException($"Cannot transition from {claim.Status} to {newStatus}. Allowed: [{string.Join(", ", allowed ?? [])}].");

        var previousStatus = claim.Status;
        claim.Status = newStatus;
        claim.Updated = DateTime.UtcNow;
        claim.UpdatedBy = caller.UserId;

        if (newStatus == ClaimStatus.Cancelled)
            claim.CancellationReason = reason;

        if (newStatus == ClaimStatus.Disputed)
            claim.DisputeReason = reason;

        await _claimDataAccess.UpdateAsync(claim, "*");
        await _claimDataAccess.InsertStatusHistoryAsync(new ClaimStatusHistory
        {
            ClaimId = claim.Id,
            FromStatus = previousStatus,
            ToStatus = newStatus,
            ChangedBy = caller.UserId,
            Reason = reason
        });

        return claim;
    }

    public async Task<Claim> ModifyAsync(
        string id,
        long? amount,
        DateTime? dueDate,
        string? description,
        string? categoryCode,
        CallerContext caller)
    {
        if (caller.UserRole != "Creditor")
            throw new BusinessRuleException("Only creditors can modify claims.");

        var claim = await _claimDataAccess.GetByIdAsync(id);
        if (claim == null)
            throw new NotFoundException("Claim", id);

        if (claim.Status != ClaimStatus.Created && claim.Status != ClaimStatus.Sent)
            throw new ConflictException($"Claim cannot be modified in status {claim.Status}. Allowed statuses: Created, Sent.");

        if (amount.HasValue)
        {
            if (amount.Value <= 0)
                throw new BusinessRuleException("Amount must be greater than zero.");
            claim.Amount = amount.Value;
        }

        if (dueDate.HasValue)
        {
            if (dueDate.Value <= DateTime.UtcNow.Date)
                throw new BusinessRuleException("Due date must be at least tomorrow.");
            claim.DueDate = dueDate.Value;
        }

        if (description != null)
        {
            if (description.Length > 500)
                throw new BusinessRuleException("Description must not exceed 500 characters.");
            claim.Description = description;
        }

        if (categoryCode != null)
        {
            if (!ValidCategoryCodes.Contains(categoryCode))
                throw new BusinessRuleException($"Invalid category code '{categoryCode}'.");
            claim.CategoryCode = categoryCode;
        }

        claim.Updated = DateTime.UtcNow;
        claim.UpdatedBy = caller.UserId;

        await _claimDataAccess.UpdateAsync(claim, "*");
        await _claimDataAccess.InsertStatusHistoryAsync(new ClaimStatusHistory
        {
            ClaimId = claim.Id,
            FromStatus = claim.Status,
            ToStatus = claim.Status,
            ChangedBy = caller.UserId,
            Reason = "Claim modified"
        });

        return claim;
    }

    public async Task<Claim> CancelAsync(string id, string reason, CallerContext caller)
    {
        if (caller.UserRole != "Creditor")
            throw new BusinessRuleException("Only creditors can cancel claims.");

        var claim = await _claimDataAccess.GetByIdAsync(id);
        if (claim == null)
            throw new NotFoundException("Claim", id);

        if (!ValidTransitions.TryGetValue(claim.Status, out var allowed) || !allowed.Contains(ClaimStatus.Cancelled))
            throw new ConflictException($"Cannot cancel a claim in {claim.Status} status.");

        var previousStatus = claim.Status;
        claim.Status = ClaimStatus.Cancelled;
        claim.CancellationReason = reason;
        claim.Updated = DateTime.UtcNow;
        claim.UpdatedBy = caller.UserId;

        await _claimDataAccess.UpdateAsync(claim, "*");
        await _claimDataAccess.InsertStatusHistoryAsync(new ClaimStatusHistory
        {
            ClaimId = claim.Id,
            FromStatus = previousStatus,
            ToStatus = ClaimStatus.Cancelled,
            ChangedBy = caller.UserId,
            Reason = reason
        });

        return claim;
    }
}
