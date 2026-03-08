using Ez.Bank.Claims.Entities;
using Ez.Bank.Core.Models;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface IClaimDataAccess
{
    Task<Claim?> GetByIdAsync(string id);
    Task<PagedResult<Claim>> GetByDebtorAsync(string debtorKennitala, int pageSize, string? continuationToken);
    Task<PagedResult<Claim>> GetByCreditorAsync(string creditorId, ClaimStatus? status, int pageSize, string? continuationToken);
    Task InsertAsync(Claim claim);
    Task<bool> UpdateAsync(Claim claim, string expectedETag);
    Task<string> GetNextClaimReferenceAsync();
    Task InsertStatusHistoryAsync(ClaimStatusHistory history);
    Task<List<ClaimStatusHistory>> GetHistoryAsync(string claimId);
    Task<List<Claim>> GetOverdueClaimsAsync(int batchSize);
}
