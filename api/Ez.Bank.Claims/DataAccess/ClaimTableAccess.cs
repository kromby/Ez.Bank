using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core.Models;

namespace Ez.Bank.Claims.DataAccess;

public class ClaimTableAccess : IClaimDataAccess
{
    private readonly TableClient _claimsTable;
    private readonly TableClient _claimsByCreditorTable;
    private readonly TableClient _statusHistoryTable;
    private readonly TableClient _sequencesTable;

    public ClaimTableAccess(TableServiceClient serviceClient)
    {
        _claimsTable = serviceClient.GetTableClient("Claims");
        _claimsTable.CreateIfNotExists();

        _claimsByCreditorTable = serviceClient.GetTableClient("ClaimsByCreditor");
        _claimsByCreditorTable.CreateIfNotExists();

        _statusHistoryTable = serviceClient.GetTableClient("ClaimStatusHistory");
        _statusHistoryTable.CreateIfNotExists();

        _sequencesTable = serviceClient.GetTableClient("Sequences");
        _sequencesTable.CreateIfNotExists();
    }

    public async Task<Claim?> GetByIdAsync(string id)
    {
        await foreach (var entity in _claimsTable.QueryAsync<ClaimTableEntity>(e => e.RowKey == id))
        {
            return entity.ToEntity();
        }

        return null;
    }

    public async Task<PagedResult<Claim>> GetByDebtorAsync(string debtorKennitala, int pageSize, string? continuationToken)
    {
        var pages = _claimsTable
            .QueryAsync<ClaimTableEntity>(e => e.PartitionKey == debtorKennitala)
            .AsPages(continuationToken, pageSize);

        await using var enumerator = pages.GetAsyncEnumerator();
        if (!await enumerator.MoveNextAsync())
        {
            return new PagedResult<Claim>
            {
                Items = [],
                PageSize = pageSize,
                ContinuationToken = null,
                HasMore = false
            };
        }

        var page = enumerator.Current;
        var items = page.Values.Select(e => e.ToEntity()).ToList();

        return new PagedResult<Claim>
        {
            Items = items,
            PageSize = pageSize,
            ContinuationToken = page.ContinuationToken,
            HasMore = page.ContinuationToken != null
        };
    }

    public async Task<PagedResult<Claim>> GetByCreditorAsync(string creditorId, ClaimStatus? status, int pageSize, string? continuationToken)
    {
        // Step 1: Query the secondary index to get claim IDs for this creditor
        var indexPages = _claimsByCreditorTable
            .QueryAsync<ClaimsByCreditorEntity>(e => e.PartitionKey == creditorId)
            .AsPages(continuationToken, pageSize);

        await using var enumerator = indexPages.GetAsyncEnumerator();
        if (!await enumerator.MoveNextAsync())
        {
            return new PagedResult<Claim>
            {
                Items = [],
                PageSize = pageSize,
                ContinuationToken = null,
                HasMore = false
            };
        }

        var page = enumerator.Current;

        // Step 2: Fetch full claim entities from the primary table
        var claims = new List<Claim>();
        foreach (var indexEntity in page.Values)
        {
            try
            {
                var response = await _claimsTable.GetEntityAsync<ClaimTableEntity>(
                    indexEntity.DebtorKennitala, indexEntity.RowKey);
                var claim = response.Value.ToEntity();

                if (status == null || claim.Status == status.Value)
                {
                    claims.Add(claim);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Claim was deleted but index entry remains; skip it
            }
        }

        return new PagedResult<Claim>
        {
            Items = claims,
            PageSize = pageSize,
            ContinuationToken = page.ContinuationToken,
            HasMore = page.ContinuationToken != null
        };
    }

    public async Task InsertAsync(Claim claim)
    {
        var entity = ClaimTableEntity.FromEntity(claim);
        await _claimsTable.AddEntityAsync(entity);

        var indexEntity = new ClaimsByCreditorEntity
        {
            PartitionKey = claim.CreditorId,
            RowKey = claim.Id,
            DebtorKennitala = claim.DebtorKennitala
        };
        await _claimsByCreditorTable.AddEntityAsync(indexEntity);
    }

    public async Task<bool> UpdateAsync(Claim claim, string expectedETag)
    {
        var entity = ClaimTableEntity.FromEntity(claim);
        entity.ETag = new ETag(expectedETag);

        try
        {
            await _claimsTable.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return false;
        }
    }

    public async Task<string> GetNextClaimReferenceAsync()
    {
        const string partitionKey = "SEQUENCE";
        const string rowKey = "ClaimReference";

        while (true)
        {
            SequenceEntity sequence;
            try
            {
                var response = await _sequencesTable.GetEntityAsync<SequenceEntity>(partitionKey, rowKey);
                sequence = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // First time: create the sequence entity
                sequence = new SequenceEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    CurrentValue = 0
                };
                await _sequencesTable.AddEntityAsync(sequence);
                var response = await _sequencesTable.GetEntityAsync<SequenceEntity>(partitionKey, rowKey);
                sequence = response.Value;
            }

            sequence.CurrentValue++;
            var nextValue = sequence.CurrentValue;

            try
            {
                await _sequencesTable.UpdateEntityAsync(sequence, sequence.ETag, TableUpdateMode.Replace);
                var year = DateTime.UtcNow.Year;
                return $"KB-{year}-{nextValue:D6}";
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // ETag mismatch - another process incremented; retry
            }
        }
    }

    public async Task InsertStatusHistoryAsync(ClaimStatusHistory history)
    {
        var entity = ClaimStatusHistoryEntity.FromEntity(history);
        await _statusHistoryTable.AddEntityAsync(entity);
    }

    public async Task<List<ClaimStatusHistory>> GetHistoryAsync(string claimId)
    {
        var results = new List<ClaimStatusHistory>();

        await foreach (var entity in _statusHistoryTable.QueryAsync<ClaimStatusHistoryEntity>(
            e => e.PartitionKey == claimId))
        {
            results.Add(entity.ToEntity());
        }

        return results;
    }

    public async Task<List<Claim>> GetOverdueClaimsAsync(int batchSize)
    {
        var now = DateTimeOffset.UtcNow;
        var overdueStatuses = new[]
        {
            (int)ClaimStatus.Sent,
            (int)ClaimStatus.Viewed,
            (int)ClaimStatus.PartiallyPaid
        };

        var results = new List<Claim>();

        await foreach (var entity in _claimsTable.QueryAsync<ClaimTableEntity>(
            e => e.DueDate < now
                 && (e.Status == overdueStatuses[0]
                     || e.Status == overdueStatuses[1]
                     || e.Status == overdueStatuses[2])))
        {
            results.Add(entity.ToEntity());
            if (results.Count >= batchSize)
                break;
        }

        return results;
    }

    internal class ClaimTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string ClaimReference { get; set; } = string.Empty;
        public string CreditorId { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public long Amount { get; set; }
        public long PaidAmount { get; set; }
        public DateTimeOffset DueDate { get; set; }
        public int Status { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset? FinalDueDate { get; set; }
        public long LateFee { get; set; }
        public double InterestRate { get; set; }
        public long AccruedInterest { get; set; }
        public string? CancellationReason { get; set; }
        public string? DisputeReason { get; set; }
        public DateTime Inserted { get; set; }
        public string InsertedBy { get; set; } = string.Empty;
        public DateTime? Updated { get; set; }
        public string? UpdatedBy { get; set; }
        public bool Deleted { get; set; }

        public Claim ToEntity() => new()
        {
            Id = RowKey,
            ClaimReference = ClaimReference,
            CreditorId = CreditorId,
            DebtorKennitala = PartitionKey,
            CurrencyCode = CurrencyCode,
            Amount = Amount,
            PaidAmount = PaidAmount,
            DueDate = DueDate.UtcDateTime,
            Status = (ClaimStatus)Status,
            CategoryCode = CategoryCode,
            Description = Description,
            FinalDueDate = FinalDueDate?.UtcDateTime,
            LateFee = LateFee,
            InterestRate = InterestRate,
            AccruedInterest = AccruedInterest,
            CancellationReason = CancellationReason,
            DisputeReason = DisputeReason,
            Inserted = Inserted,
            InsertedBy = InsertedBy,
            Updated = Updated,
            UpdatedBy = UpdatedBy,
            Deleted = Deleted
        };

        public static ClaimTableEntity FromEntity(Claim claim) => new()
        {
            PartitionKey = claim.DebtorKennitala,
            RowKey = claim.Id,
            ClaimReference = claim.ClaimReference,
            CreditorId = claim.CreditorId,
            CurrencyCode = claim.CurrencyCode,
            Amount = claim.Amount,
            PaidAmount = claim.PaidAmount,
            DueDate = new DateTimeOffset(claim.DueDate, TimeSpan.Zero),
            Status = (int)claim.Status,
            CategoryCode = claim.CategoryCode,
            Description = claim.Description,
            FinalDueDate = claim.FinalDueDate.HasValue
                ? new DateTimeOffset(claim.FinalDueDate.Value, TimeSpan.Zero)
                : null,
            LateFee = claim.LateFee,
            InterestRate = claim.InterestRate,
            AccruedInterest = claim.AccruedInterest,
            CancellationReason = claim.CancellationReason,
            DisputeReason = claim.DisputeReason,
            Inserted = claim.Inserted,
            InsertedBy = claim.InsertedBy,
            Updated = claim.Updated,
            UpdatedBy = claim.UpdatedBy,
            Deleted = claim.Deleted
        };
    }

    internal class ClaimsByCreditorEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string DebtorKennitala { get; set; } = string.Empty;
    }

    internal class ClaimStatusHistoryEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public int FromStatus { get; set; }
        public int ToStatus { get; set; }
        public DateTimeOffset ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string? Reason { get; set; }

        public ClaimStatusHistory ToEntity() => new()
        {
            Id = RowKey.Contains('_') ? RowKey[(RowKey.IndexOf('_') + 1)..] : RowKey,
            ClaimId = PartitionKey,
            FromStatus = (ClaimStatus)FromStatus,
            ToStatus = (ClaimStatus)ToStatus,
            ChangedAt = ChangedAt.UtcDateTime,
            ChangedBy = ChangedBy,
            Reason = Reason
        };

        public static ClaimStatusHistoryEntity FromEntity(ClaimStatusHistory history) => new()
        {
            PartitionKey = history.ClaimId,
            RowKey = $"{history.ChangedAt:O}_{history.Id}",
            FromStatus = (int)history.FromStatus,
            ToStatus = (int)history.ToStatus,
            ChangedAt = new DateTimeOffset(history.ChangedAt, TimeSpan.Zero),
            ChangedBy = history.ChangedBy,
            Reason = history.Reason
        };
    }

    internal class SequenceEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public long CurrentValue { get; set; }
    }
}
