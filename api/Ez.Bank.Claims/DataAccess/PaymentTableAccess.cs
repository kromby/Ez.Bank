using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public class PaymentTableAccess : TableAccessBase, IPaymentDataAccess
{
    public PaymentTableAccess(TableServiceClient serviceClient)
        : base(serviceClient, "Payments")
    {
    }

    public async Task<List<Payment>> GetByClaimIdAsync(string claimId)
    {
        var results = new List<Payment>();

        await foreach (var entity in Table.QueryAsync<PaymentTableEntity>(
            e => e.PartitionKey == claimId))
        {
            results.Add(entity.ToEntity());
        }

        return results;
    }

    public async Task InsertAsync(Payment payment)
    {
        var entity = PaymentTableEntity.FromEntity(payment);
        await Table.AddEntityAsync(entity);
    }

    internal class PaymentTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;
        public long Amount { get; set; }
        public DateTimeOffset PaymentDate { get; set; }
        public string BankId { get; set; } = string.Empty;
        public string PaymentReference { get; set; } = string.Empty;
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public Payment ToEntity() => new()
        {
            Id = RowKey,
            ClaimId = PartitionKey,
            CurrencyCode = CurrencyCode,
            Amount = Amount,
            PaymentDate = PaymentDate.UtcDateTime,
            BankId = BankId,
            PaymentReference = PaymentReference,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static PaymentTableEntity FromEntity(Payment payment) => new()
        {
            PartitionKey = payment.ClaimId,
            RowKey = payment.Id,
            CurrencyCode = payment.CurrencyCode,
            Amount = payment.Amount,
            PaymentDate = new DateTimeOffset(payment.PaymentDate, TimeSpan.Zero),
            BankId = payment.BankId,
            PaymentReference = payment.PaymentReference,
            Inserted = payment.Inserted,
            InsertedBy = payment.InsertedBy,
            Updated = payment.Updated,
            UpdatedBy = payment.UpdatedBy,
            Deleted = payment.Deleted
        };
    }
}
