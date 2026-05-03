using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public static class DataSeeder
{
    public static async Task SeedAsync(
        ICurrencyDataAccess currencyDataAccess,
        ISystemConfigDataAccess systemConfigDataAccess)
    {
        await SeedCurrenciesAsync(currencyDataAccess);
        await SeedSystemConfigAsync(systemConfigDataAccess);
    }

    private static async Task SeedCurrenciesAsync(ICurrencyDataAccess currencyDataAccess)
    {
        var currencies = new (string Code, int DecimalPlaces)[]
        {
            ("ISK", 0),
            ("EUR", 2),
            ("USD", 2),
            ("GBP", 2),
            ("DKK", 2),
            ("SEK", 2),
            ("NOK", 2)
        };

        foreach (var (code, decimalPlaces) in currencies)
        {
            var existing = await currencyDataAccess.GetByCodeAsync(code);
            if (existing is null)
            {
                var currency = new Currency
                {
                    Code = code,
                    DecimalPlaces = decimalPlaces
                };
                await currencyDataAccess.InsertAsync(currency);
            }
        }
    }

    private static async Task SeedSystemConfigAsync(ISystemConfigDataAccess systemConfigDataAccess)
    {
        var configs = new (string Key, string Value)[]
        {
            ("DefaultLateFee", "950"),
            ("DefaultInterestRate", "12.00")
        };

        foreach (var (key, value) in configs)
        {
            var existing = await systemConfigDataAccess.GetByKeyAsync(key);
            if (existing is null)
            {
                var config = new SystemConfig
                {
                    Key = key,
                    Value = value
                };
                await systemConfigDataAccess.InsertOrUpdateAsync(config);
            }
        }
    }
}
