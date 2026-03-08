using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;

namespace Ez.Bank.Claims.UseCases;

public class DocumentInteractor
{
    private readonly IClaimDataAccess _claimDataAccess;
    private readonly IClaimDocumentDataAccess _claimDocumentDataAccess;
    private readonly IDocumentStorage _documentStorage;

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxDocumentsPerClaim = 5;
    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

    public DocumentInteractor(
        IClaimDataAccess claimDataAccess,
        IClaimDocumentDataAccess claimDocumentDataAccess,
        IDocumentStorage documentStorage)
    {
        _claimDataAccess = claimDataAccess;
        _claimDocumentDataAccess = claimDocumentDataAccess;
        _documentStorage = documentStorage;
    }

    public async Task<ClaimDocument> UploadAsync(
        string claimId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(claimId);
        if (claim == null)
            throw new NotFoundException("Claim", claimId);

        if (claim.Status == ClaimStatus.Cancelled)
            throw new ConflictException("Cannot attach documents to a cancelled claim.");

        if (contentType != "application/pdf")
            throw new BusinessRuleException($"Only PDF files are accepted. Received: {contentType}.");

        var header = new byte[5];
        var bytesRead = await content.ReadAsync(header, 0, 5);
        if (bytesRead < 5 || !header.AsSpan().SequenceEqual(PdfMagicBytes))
            throw new BusinessRuleException("File is not a valid PDF document.");
        content.Position = 0;

        if (fileSizeBytes > MaxFileSizeBytes)
            throw new BusinessRuleException("File exceeds maximum size of 10 MB.");

        var existingDocs = await _claimDocumentDataAccess.GetByClaimIdAsync(claimId);
        if (existingDocs.Count >= MaxDocumentsPerClaim)
            throw new BusinessRuleException("Maximum of 5 documents per claim reached.");

        var storagePath = await _documentStorage.UploadAsync(claimId, fileName, content);

        var document = new ClaimDocument
        {
            ClaimId = claimId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StoragePath = storagePath,
            UploadedBy = caller.UserId
        };

        await _claimDocumentDataAccess.InsertAsync(document);
        return document;
    }

    public async Task<List<ClaimDocument>> GetByClaimIdAsync(string claimId, CallerContext caller)
    {
        var claim = await _claimDataAccess.GetByIdAsync(claimId);
        if (claim == null)
            throw new NotFoundException("Claim", claimId);

        return await _claimDocumentDataAccess.GetByClaimIdAsync(claimId);
    }

    public async Task<Stream> DownloadAsync(string claimId, string documentId, CallerContext caller)
    {
        var document = await _claimDocumentDataAccess.GetByIdAsync(claimId, documentId);
        if (document == null || document.Deleted)
            throw new NotFoundException("Document", documentId);

        return await _documentStorage.DownloadAsync(document.StoragePath);
    }

    public async Task DeleteAsync(string claimId, string documentId, CallerContext caller)
    {
        var document = await _claimDocumentDataAccess.GetByIdAsync(claimId, documentId);
        if (document == null)
            throw new NotFoundException("Document", documentId);

        if (document.UploadedBy != caller.UserId)
            throw new UnauthorizedAccessException("Only the uploader can delete this document.");

        await _claimDocumentDataAccess.SoftDeleteAsync(claimId, documentId);
    }
}
