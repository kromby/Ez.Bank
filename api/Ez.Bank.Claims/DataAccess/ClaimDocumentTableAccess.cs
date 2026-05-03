using Azure;
using Azure.Data.Tables;
using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;

namespace Ez.Bank.Claims.DataAccess;

public class ClaimDocumentTableAccess : TableAccessBase, IClaimDocumentDataAccess
{
    public ClaimDocumentTableAccess(TableServiceClient serviceClient)
        : base(serviceClient, "ClaimDocuments")
    {
    }

    public async Task<ClaimDocument?> GetByIdAsync(string claimId, string documentId)
    {
        var entity = await GetEntityAsync<ClaimDocumentTableEntity>(claimId, documentId);
        return entity?.ToEntity();
    }

    public async Task<List<ClaimDocument>> GetByClaimIdAsync(string claimId)
    {
        var results = new List<ClaimDocument>();

        await foreach (var entity in Table.QueryAsync<ClaimDocumentTableEntity>(
            e => e.PartitionKey == claimId && !e.Deleted))
        {
            results.Add(entity.ToEntity());
        }

        return results;
    }

    public async Task InsertAsync(ClaimDocument document)
    {
        var entity = ClaimDocumentTableEntity.FromEntity(document);
        await Table.AddEntityAsync(entity);
    }

    public async Task SoftDeleteAsync(string claimId, string documentId)
    {
        var entity = await GetEntityAsync<ClaimDocumentTableEntity>(claimId, documentId);
        if (entity == null)
            return;

        entity.Deleted = true;
        await Table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
    }

    internal class ClaimDocumentTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long FileSizeBytes { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public DateTimeOffset UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public bool Deleted { get; set; }

        public ClaimDocument ToEntity() => new()
        {
            Id = RowKey,
            ClaimId = PartitionKey,
            FileName = FileName,
            ContentType = ContentType,
            FileSizeBytes = FileSizeBytes,
            StoragePath = StoragePath,
            UploadedAt = UploadedAt.UtcDateTime,
            UploadedBy = UploadedBy,
            Deleted = Deleted
        };

        public static ClaimDocumentTableEntity FromEntity(ClaimDocument document) => new()
        {
            PartitionKey = document.ClaimId,
            RowKey = document.Id,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            StoragePath = document.StoragePath,
            UploadedAt = new DateTimeOffset(document.UploadedAt, TimeSpan.Zero),
            UploadedBy = document.UploadedBy,
            Deleted = document.Deleted
        };
    }
}
