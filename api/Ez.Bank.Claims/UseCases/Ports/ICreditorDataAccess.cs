using Ez.Bank.Claims.Entities;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface ICreditorDataAccess
{
    Task<Creditor?> GetByIdAsync(string id);
    Task<Creditor?> GetByKennitalaAndBankAsync(string kennitala, string bankId);
    Task InsertAsync(Creditor creditor);
}
