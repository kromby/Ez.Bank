using Ez.Bank.Claims.Entities;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface IClaimDocumentDataAccess
{
    Task<ClaimDocument?> GetByIdAsync(string claimId, string documentId);
    Task<List<ClaimDocument>> GetByClaimIdAsync(string claimId);
    Task InsertAsync(ClaimDocument document);
    Task SoftDeleteAsync(string claimId, string documentId);
}
