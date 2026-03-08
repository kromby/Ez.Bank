using Ez.Bank.Claims.Entities;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface IPaymentDataAccess
{
    Task<List<Payment>> GetByClaimIdAsync(string claimId);
    Task InsertAsync(Payment payment);
}
