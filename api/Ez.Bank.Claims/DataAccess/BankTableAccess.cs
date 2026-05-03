using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.UseCases.Ports;

using BankEntity = Ez.Bank.Claims.Entities.Bank;

namespace Ez.Bank.Claims.DataAccess;

public class BankTableAccess : TableAccessBase, IBankDataAccess
{
    private const string PartitionKeyValue = "BANK";

    public BankTableAccess(TableServiceClient serviceClient)
        : base(serviceClient, "Banks")
    {
    }

    public async Task<BankEntity?> GetByIdAsync(string id)
    {
        var entity = await GetEntityAsync<BankTableEntity>(PartitionKeyValue, id);
        return entity?.ToEntity();
    }

    public async Task<List<BankEntity>> GetAllAsync()
    {
        var entities = new List<BankEntity>();
        await foreach (var entity in Table.QueryAsync<BankTableEntity>(e => e.PartitionKey == PartitionKeyValue))
        {
            entities.Add(entity.ToEntity());
        }
        return entities;
    }

    public async Task InsertAsync(BankEntity bank)
    {
        var entity = BankTableEntity.FromEntity(bank);
        await Table.AddEntityAsync(entity);
    }

    internal class BankTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = PartitionKeyValue;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Kennitala { get; set; } = string.Empty;
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public BankEntity ToEntity() => new()
        {
            Id = RowKey,
            Name = Name,
            Kennitala = Kennitala,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static BankTableEntity FromEntity(BankEntity bank) => new()
        {
            RowKey = bank.Id,
            Name = bank.Name,
            Kennitala = bank.Kennitala,
            Inserted = bank.Inserted,
            InsertedBy = bank.InsertedBy,
            Updated = bank.Updated,
            UpdatedBy = bank.UpdatedBy,
            Deleted = bank.Deleted
        };
    }
}
