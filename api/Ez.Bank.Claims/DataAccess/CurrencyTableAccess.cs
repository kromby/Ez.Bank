using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace Ez.Bank.Claims.DataAccess;

public class CurrencyTableAccess : TableAccessBase, ICurrencyDataAccess
{
    private const string PartitionKeyValue = "CURRENCY";
    private const string AllCurrenciesCacheKey = "currencies_all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public CurrencyTableAccess(TableServiceClient serviceClient, IMemoryCache cache)
        : base(serviceClient, "Currencies")
    {
        _cache = cache;
    }

    public async Task<Currency?> GetByCodeAsync(string code)
    {
        var entity = await GetEntityAsync<CurrencyTableEntity>(PartitionKeyValue, code);
        return entity?.ToEntity();
    }

    public async Task<List<Currency>> GetAllAsync()
    {
        if (_cache.TryGetValue(AllCurrenciesCacheKey, out List<Currency>? cached) && cached is not null)
            return cached;

        var entities = new List<Currency>();
        await foreach (var entity in Table.QueryAsync<CurrencyTableEntity>(e => e.PartitionKey == PartitionKeyValue))
        {
            entities.Add(entity.ToEntity());
        }

        _cache.Set(AllCurrenciesCacheKey, entities, CacheTtl);
        return entities;
    }

    public async Task InsertAsync(Currency currency)
    {
        var entity = CurrencyTableEntity.FromEntity(currency);
        await Table.AddEntityAsync(entity);
        _cache.Remove(AllCurrenciesCacheKey);
    }

    internal class CurrencyTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = PartitionKeyValue;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DecimalPlaces { get; set; }
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public Currency ToEntity() => new()
        {
            Id = Id,
            Code = RowKey,
            Name = Name,
            DecimalPlaces = DecimalPlaces,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static CurrencyTableEntity FromEntity(Currency currency) => new()
        {
            RowKey = currency.Code,
            Id = currency.Id,
            Name = currency.Name,
            DecimalPlaces = currency.DecimalPlaces,
            Inserted = currency.Inserted,
            InsertedBy = currency.InsertedBy,
            Updated = currency.Updated,
            UpdatedBy = currency.UpdatedBy,
            Deleted = currency.Deleted
        };
    }
}
