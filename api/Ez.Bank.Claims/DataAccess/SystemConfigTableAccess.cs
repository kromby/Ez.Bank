using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace Ez.Bank.Claims.DataAccess;

public class SystemConfigTableAccess : TableAccessBase, ISystemConfigDataAccess
{
    private const string PartitionKeyValue = "CONFIG";
    private const string CacheKeyPrefix = "systemconfig_";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public SystemConfigTableAccess(TableServiceClient serviceClient, IMemoryCache cache)
        : base(serviceClient, "SystemConfig")
    {
        _cache = cache;
    }

    public async Task<SystemConfig?> GetByKeyAsync(string key)
    {
        var cacheKey = CacheKeyPrefix + key;
        if (_cache.TryGetValue(cacheKey, out SystemConfig? cached) && cached is not null)
        {
            return cached;
        }

        var entity = await GetEntityAsync<SystemConfigTableEntity>(PartitionKeyValue, key);
        var result = entity?.ToEntity();

        if (result is not null)
        {
            _cache.Set(cacheKey, result, CacheTtl);
        }

        return result;
    }

    public async Task InsertOrUpdateAsync(SystemConfig config)
    {
        var entity = SystemConfigTableEntity.FromEntity(config);
        await Table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        _cache.Remove(CacheKeyPrefix + config.Key);
    }

    internal class SystemConfigTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = PartitionKeyValue;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Id { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public SystemConfig ToEntity() => new()
        {
            Id = Id,
            Key = RowKey,
            Value = Value,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static SystemConfigTableEntity FromEntity(SystemConfig config) => new()
        {
            RowKey = config.Key,
            Id = config.Id,
            Value = config.Value,
            Inserted = config.Inserted,
            InsertedBy = config.InsertedBy,
            Updated = config.Updated,
            UpdatedBy = config.UpdatedBy,
            Deleted = config.Deleted
        };
    }
}
