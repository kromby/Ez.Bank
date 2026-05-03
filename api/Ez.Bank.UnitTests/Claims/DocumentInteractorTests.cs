using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;
using Moq;
using Xunit;

namespace Ez.Bank.UnitTests.Claims;

public class DocumentInteractorTests
{
    private readonly Mock<IClaimDataAccess> _claimDataAccess;
    private readonly Mock<IClaimDocumentDataAccess> _documentDataAccess;
    private readonly Mock<IDocumentStorage> _documentStorage;
    private readonly DocumentInteractor _sut;
    private readonly CallerContext _caller = new("user-1", "bank-1", "Admin");

    public DocumentInteractorTests()
    {
        _claimDataAccess = new Mock<IClaimDataAccess>();
        _documentDataAccess = new Mock<IClaimDocumentDataAccess>();
        _documentStorage = new Mock<IDocumentStorage>();
        _sut = new DocumentInteractor(
            _claimDataAccess.Object,
            _documentDataAccess.Object,
            _documentStorage.Object);
    }

    private static Stream CreateValidPdfStream()
    {
        var stream = new MemoryStream();
        // PDF magic bytes: %PDF-
        byte[] magicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D };
        stream.Write(magicBytes, 0, magicBytes.Length);
        // Dummy data after the magic bytes
        byte[] dummyData = "1.4 dummy pdf content for testing purposes"u8.ToArray();
        stream.Write(dummyData, 0, dummyData.Length);
        stream.Position = 0;
        return stream;
    }

    private static Stream CreateInvalidPdfStream()
    {
        var stream = new MemoryStream();
        byte[] invalidBytes = { 0x00, 0x01, 0x02, 0x03, 0x04 };
        stream.Write(invalidBytes, 0, invalidBytes.Length);
        stream.Position = 0;
        return stream;
    }

    private static Claim CreateClaim(ClaimStatus status = ClaimStatus.Sent)
    {
        return new Claim
        {
            Id = "claim-1",
            Status = status,
            Amount = 10000,
            CurrencyCode = "ISK"
        };
    }

    private static ClaimDocument CreateDocument(
        string id = "doc-1",
        string uploadedBy = "user-1",
        bool deleted = false)
    {
        return new ClaimDocument
        {
            Id = id,
            ClaimId = "claim-1",
            FileName = "invoice.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            StoragePath = "claims/claim-1/invoice.pdf",
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy,
            Deleted = deleted
        };
    }

    [Fact]
    public async Task Upload_ValidPdf_Succeeds()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _documentDataAccess.Setup(x => x.GetByClaimIdAsync("claim-1")).ReturnsAsync(new List<ClaimDocument>());
        _documentStorage.Setup(x => x.UploadAsync("claim-1", "invoice.pdf", It.IsAny<Stream>()))
            .ReturnsAsync("claims/claim-1/invoice.pdf");

        using var pdfStream = CreateValidPdfStream();

        // Act
        var result = await _sut.UploadAsync(
            "claim-1", "invoice.pdf", "application/pdf", pdfStream.Length, pdfStream, _caller);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("claim-1", result.ClaimId);
        Assert.Equal("invoice.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        _documentStorage.Verify(x => x.UploadAsync("claim-1", "invoice.pdf", It.IsAny<Stream>()), Times.Once);
        _documentDataAccess.Verify(x => x.InsertAsync(It.IsAny<ClaimDocument>()), Times.Once);
    }

    [Fact]
    public async Task Upload_NonPdfContentType_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        using var pdfStream = CreateValidPdfStream();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.UploadAsync("claim-1", "image.png", "image/png", pdfStream.Length, pdfStream, _caller));
    }

    [Fact]
    public async Task Upload_InvalidPdfMagicBytes_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _documentDataAccess.Setup(x => x.GetByClaimIdAsync("claim-1")).ReturnsAsync(new List<ClaimDocument>());

        using var invalidStream = CreateInvalidPdfStream();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.UploadAsync("claim-1", "fake.pdf", "application/pdf", invalidStream.Length, invalidStream, _caller));
    }

    [Fact]
    public async Task Upload_ExceedsMaxSize_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        using var pdfStream = CreateValidPdfStream();
        long oversizedFileSize = 10 * 1024 * 1024 + 1; // 10MB + 1 byte

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.UploadAsync("claim-1", "large.pdf", "application/pdf", oversizedFileSize, pdfStream, _caller));
    }

    [Fact]
    public async Task Upload_MaxDocumentsReached_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        var existingDocs = Enumerable.Range(1, 5)
            .Select(i => CreateDocument(id: $"doc-{i}"))
            .ToList();
        _documentDataAccess.Setup(x => x.GetByClaimIdAsync("claim-1")).ReturnsAsync(existingDocs);

        using var pdfStream = CreateValidPdfStream();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.UploadAsync("claim-1", "sixth.pdf", "application/pdf", pdfStream.Length, pdfStream, _caller));
    }

    [Fact]
    public async Task Upload_CancelledClaim_ThrowsConflictException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Cancelled);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        using var pdfStream = CreateValidPdfStream();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UploadAsync("claim-1", "invoice.pdf", "application/pdf", pdfStream.Length, pdfStream, _caller));
    }

    [Fact]
    public async Task Delete_ByUploader_Succeeds()
    {
        // Arrange
        var document = CreateDocument(id: "doc-1", uploadedBy: "user-1");
        _documentDataAccess.Setup(x => x.GetByIdAsync("claim-1", "doc-1")).ReturnsAsync(document);
        _documentDataAccess.Setup(x => x.SoftDeleteAsync("claim-1", "doc-1")).Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteAsync("claim-1", "doc-1", _caller);

        // Assert — only soft delete, no storage delete
        _documentDataAccess.Verify(x => x.SoftDeleteAsync("claim-1", "doc-1"), Times.Once);
    }

    [Fact]
    public async Task Delete_ByOtherUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var document = CreateDocument(id: "doc-1", uploadedBy: "other-user");
        _documentDataAccess.Setup(x => x.GetByIdAsync("claim-1", "doc-1")).ReturnsAsync(document);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.DeleteAsync("claim-1", "doc-1", _caller));
    }
}
