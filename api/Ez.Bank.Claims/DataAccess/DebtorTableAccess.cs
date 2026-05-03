using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public class DebtorTableAccess : TableAccessBase, IDebtorDataAccess
{
    private const string PartitionKeyValue = "DEBTOR";

    public DebtorTableAccess(TableServiceClient serviceClient)
        : base(serviceClient, "Debtors")
    {
    }

    public async Task<Debtor?> GetByIdAsync(string id)
    {
        // RowKey is Kennitala, not Id. Must scan to find by Id.
        await foreach (var entity in Table.QueryAsync<DebtorTableEntity>(
            e => e.PartitionKey == PartitionKeyValue && e.Id == id))
        {
            return entity.ToEntity();
        }

        return null;
    }

    public async Task<Debtor?> GetByKennitalaAsync(string kennitala)
    {
        var entity = await GetEntityAsync<DebtorTableEntity>(PartitionKeyValue, kennitala);
        return entity?.ToEntity();
    }

    public async Task InsertAsync(Debtor debtor)
    {
        var entity = DebtorTableEntity.FromEntity(debtor);
        await Table.AddEntityAsync(entity);
    }

    public async Task UpdateAsync(Debtor debtor)
    {
        var entity = DebtorTableEntity.FromEntity(debtor);
        await Table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    internal class DebtorTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = PartitionKeyValue;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? BankId { get; set; }
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public Debtor ToEntity() => new()
        {
            Id = Id,
            Kennitala = RowKey,
            Name = Name,
            BankId = BankId,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static DebtorTableEntity FromEntity(Debtor debtor) => new()
        {
            RowKey = debtor.Kennitala,
            Id = debtor.Id,
            Name = debtor.Name,
            BankId = debtor.BankId,
            Inserted = debtor.Inserted,
            InsertedBy = debtor.InsertedBy,
            Updated = debtor.Updated,
            UpdatedBy = debtor.UpdatedBy,
            Deleted = debtor.Deleted
        };
    }
}
