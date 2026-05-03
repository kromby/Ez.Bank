using Ez.Bank.Claims.Entities;

using BankEntity = Ez.Bank.Claims.Entities.Bank;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface IBankDataAccess
{
    Task<BankEntity?> GetByIdAsync(string id);
    Task<List<BankEntity>> GetAllAsync();
    Task InsertAsync(BankEntity bank);
}
