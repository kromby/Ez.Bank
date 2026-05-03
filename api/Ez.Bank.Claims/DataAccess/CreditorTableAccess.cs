using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public class CreditorTableAccess : TableAccessBase, ICreditorDataAccess
{
    public CreditorTableAccess(TableServiceClient serviceClient)
        : base(serviceClient, "Creditors")
    {
    }

    public async Task<Creditor?> GetByIdAsync(string id)
    {
        // RowKey is the Id, but PartitionKey is BankId which is unknown here.
        // Must scan all partitions to find the entity by RowKey.
        await foreach (var entity in Table.QueryAsync<CreditorTableEntity>(e => e.RowKey == id))
        {
            return entity.ToEntity();
        }

        return null;
    }

    public async Task<Creditor?> GetByKennitalaAndBankAsync(string kennitala, string bankId)
    {
        await foreach (var entity in Table.QueryAsync<CreditorTableEntity>(
            e => e.PartitionKey == bankId && e.Kennitala == kennitala))
        {
            return entity.ToEntity();
        }

        return null;
    }

    public async Task InsertAsync(Creditor creditor)
    {
        var entity = CreditorTableEntity.FromEntity(creditor);
        await Table.AddEntityAsync(entity);
    }

    internal class CreditorTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Kennitala { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public Creditor ToEntity() => new()
        {
            Id = RowKey,
            Kennitala = Kennitala,
            Name = Name,
            BankId = PartitionKey,
            AccountNumber = AccountNumber,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static CreditorTableEntity FromEntity(Creditor creditor) => new()
        {
            PartitionKey = creditor.BankId,
            RowKey = creditor.Id,
            Kennitala = creditor.Kennitala,
            Name = creditor.Name,
            AccountNumber = creditor.AccountNumber,
            Inserted = creditor.Inserted,
            InsertedBy = creditor.InsertedBy,
            Updated = creditor.Updated,
            UpdatedBy = creditor.UpdatedBy,
            Deleted = creditor.Deleted
        };
    }
}
