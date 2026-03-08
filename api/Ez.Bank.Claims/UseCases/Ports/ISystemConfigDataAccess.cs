using Ez.Bank.Claims.Entities;

namespace Ez.Bank.Claims.UseCases.Ports;

public interface ISystemConfigDataAccess
{
    Task<SystemConfig?> GetByKeyAsync(string key);
    Task InsertOrUpdateAsync(SystemConfig config);
}
