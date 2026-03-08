using Ez.Bank.Claims.Entities;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface ICurrencyDataAccess
{
    Task<Currency?> GetByCodeAsync(string code);
    Task<List<Currency>> GetAllAsync();
    Task InsertAsync(Currency currency);
}
