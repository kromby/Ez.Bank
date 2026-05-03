using Ez.Bank.Claims.Entities;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface IDebtorDataAccess
{
    Task<Debtor?> GetByIdAsync(string id);
    Task<Debtor?> GetByKennitalaAsync(string kennitala);
    Task InsertAsync(Debtor debtor);
    Task UpdateAsync(Debtor debtor);
}
