using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;
using Moq;
using Xunit;

namespace Ez.Bank.UnitTests.Claims;

public class ClaimInteractorTests
{
    private readonly Mock<IClaimDataAccess> _claimDataAccess;
    private readonly Mock<ICreditorDataAccess> _creditorDataAccess;
    private readonly Mock<IDebtorDataAccess> _debtorDataAccess;
    private readonly Mock<ICurrencyDataAccess> _currencyDataAccess;
    private readonly ClaimInteractor _sut;
    private readonly CallerContext _creditorCaller;
    private readonly CallerContext _nonCreditorCaller;
    private readonly Creditor _validCreditor;
    private readonly Debtor _validDebtor;
    private readonly Currency _validCurrency;

    public ClaimInteractorTests()
    {
        _claimDataAccess = new Mock<IClaimDataAccess>();
        _creditorDataAccess = new Mock<ICreditorDataAccess>();
        _debtorDataAccess = new Mock<IDebtorDataAccess>();
        _currencyDataAccess = new Mock<ICurrencyDataAccess>();

        _sut = new ClaimInteractor(
            _claimDataAccess.Object,
            _creditorDataAccess.Object,
            _debtorDataAccess.Object,
            _currencyDataAccess.Object);

        _creditorCaller = new CallerContext("user-1", "bank-1", "Creditor");
        _nonCreditorCaller = new CallerContext("user-2", "bank-2", "Debtor");

        _validCreditor = new Creditor
        {
            Id = "creditor-1",
            Kennitala = "1234567890",
            Name = "Test Creditor",
            BankId = "bank-1",
            AccountNumber = "0001-26-000001"
        };

        _validDebtor = new Debtor
        {
            Id = "debtor-1",
            Kennitala = "0987654321",
            Name = "Test Debtor",
            BankId = "bank-2"
        };

        _validCurrency = new Currency
        {
            Id = "currency-1",
            Code = "ISK",
            Name = "Icelandic Krona",
            DecimalPlaces = 0
        };
    }

    // --- CreateAsync Tests ---

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesClaim()
    {
        // Arrange
        _creditorDataAccess.Setup(x => x.GetByIdAsync("creditor-1"))
            .ReturnsAsync(_validCreditor);
        _debtorDataAccess.Setup(x => x.GetByKennitalaAsync("0987654321"))
            .ReturnsAsync(_validDebtor);
        _currencyDataAccess.Setup(x => x.GetByCodeAsync("ISK"))
            .ReturnsAsync(_validCurrency);
        _claimDataAccess.Setup(x => x.GetNextClaimReferenceAsync())
            .ReturnsAsync("CLM-000001");
        _claimDataAccess.Setup(x => x.InsertAsync(It.IsAny<Claim>()))
            .Returns(Task.CompletedTask);

        var dueDate = DateTime.UtcNow.AddDays(30);

        // Act
        var result = await _sut.CreateAsync(
            "creditor-1", "0987654321", 10000, "ISK",
            dueDate, "UTILITY", "Test claim", _creditorCaller);

        // Assert
        Assert.NotNull(result);
        _claimDataAccess.Verify(x => x.InsertAsync(It.Is<Claim>(c =>
            c.CreditorId == "creditor-1" &&
            c.DebtorKennitala == "0987654321" &&
            c.Amount == 10000 &&
            c.CurrencyCode == "ISK" &&
            c.Status == ClaimStatus.Created)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonCreditorRole_ThrowsBusinessRuleException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "0987654321", 10000, "ISK",
                DateTime.UtcNow.AddDays(30), "UTILITY", "Test claim", _nonCreditorCaller));
    }

    [Fact]
    public async Task CreateAsync_InvalidAmount_ThrowsBusinessRuleException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "0987654321", 0, "ISK",
                DateTime.UtcNow.AddDays(30), "UTILITY", "Test claim", _creditorCaller));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "0987654321", -500, "ISK",
                DateTime.UtcNow.AddDays(30), "UTILITY", "Test claim", _creditorCaller));
    }

    [Fact]
    public async Task CreateAsync_PastDueDate_ThrowsBusinessRuleException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "0987654321", 10000, "ISK",
                DateTime.UtcNow.AddDays(-1), "UTILITY", "Test claim", _creditorCaller));
    }

    [Fact]
    public async Task CreateAsync_InvalidCategoryCode_ThrowsBusinessRuleException()
    {
        // Arrange
        _creditorDataAccess.Setup(x => x.GetByIdAsync("creditor-1"))
            .ReturnsAsync(_validCreditor);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "0987654321", 10000, "ISK",
                DateTime.UtcNow.AddDays(30), "", "Test claim", _creditorCaller));
    }

    [Fact]
    public async Task CreateAsync_InvalidKennitala_ThrowsBusinessRuleException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "", 10000, "ISK",
                DateTime.UtcNow.AddDays(30), "UTILITY", "Test claim", _creditorCaller));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", null!, 10000, "ISK",
                DateTime.UtcNow.AddDays(30), "UTILITY", "Test claim", _creditorCaller));
    }

    [Fact]
    public async Task CreateAsync_UnknownCurrency_ThrowsBusinessRuleException()
    {
        // Arrange
        _creditorDataAccess.Setup(x => x.GetByIdAsync("creditor-1"))
            .ReturnsAsync(_validCreditor);
        _debtorDataAccess.Setup(x => x.GetByKennitalaAsync("0987654321"))
            .ReturnsAsync(_validDebtor);
        _currencyDataAccess.Setup(x => x.GetByCodeAsync("XYZ"))
            .ReturnsAsync((Currency?)null);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(
                "creditor-1", "0987654321", 10000, "XYZ",
                DateTime.UtcNow.AddDays(30), "UTILITY", "Test claim", _creditorCaller));
    }

    [Fact]
    public async Task CreateAsync_DebtorNotFound_CreatesDebtorAutomatically()
    {
        // Arrange
        _creditorDataAccess.Setup(x => x.GetByIdAsync("creditor-1"))
            .ReturnsAsync(_validCreditor);
        _debtorDataAccess.Setup(x => x.GetByKennitalaAsync("0987654321"))
            .ReturnsAsync((Debtor?)null);
        _debtorDataAccess.Setup(x => x.InsertAsync(It.IsAny<Debtor>()))
            .Returns(Task.CompletedTask);
        _currencyDataAccess.Setup(x => x.GetByCodeAsync("ISK"))
            .ReturnsAsync(_validCurrency);
        _claimDataAccess.Setup(x => x.GetNextClaimReferenceAsync())
            .ReturnsAsync("CLM-000002");
        _claimDataAccess.Setup(x => x.InsertAsync(It.IsAny<Claim>()))
            .Returns(Task.CompletedTask);

        var dueDate = DateTime.UtcNow.AddDays(30);

        // Act
        var result = await _sut.CreateAsync(
            "creditor-1", "0987654321", 10000, "ISK",
            dueDate, "UTILITY", "Test claim", _creditorCaller);

        // Assert
        Assert.NotNull(result);
        _debtorDataAccess.Verify(x => x.InsertAsync(It.Is<Debtor>(d =>
            d.Kennitala == "0987654321")), Times.Once);
        _claimDataAccess.Verify(x => x.InsertAsync(It.IsAny<Claim>()), Times.Once);
    }

    // --- GetByIdAsync Tests ---

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        _claimDataAccess.Setup(x => x.GetByIdAsync("nonexistent"))
            .ReturnsAsync((Claim?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetByIdAsync("nonexistent", _creditorCaller));
    }

    // --- UpdateStatusAsync Tests ---

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesClaim()
    {
        // Arrange
        var existingClaim = new Claim
        {
            Id = "claim-1",
            ClaimReference = "CLM-000001",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 0,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Sent,
            CategoryCode = "UTILITY",
            Description = "Test claim"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1"))
            .ReturnsAsync(existingClaim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), "*"))
            .ReturnsAsync(true);
        _claimDataAccess.Setup(x => x.InsertStatusHistoryAsync(It.IsAny<ClaimStatusHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateStatusAsync("claim-1", ClaimStatus.Viewed, null, _creditorCaller);

        // Assert
        Assert.NotNull(result);
        _claimDataAccess.Verify(x => x.UpdateAsync(
            It.Is<Claim>(c => c.Status == ClaimStatus.Viewed), "*"), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsConflictException()
    {
        // Arrange
        var paidClaim = new Claim
        {
            Id = "claim-2",
            ClaimReference = "CLM-000002",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 10000,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Paid,
            CategoryCode = "UTILITY",
            Description = "Paid claim"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-2"))
            .ReturnsAsync(paidClaim);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateStatusAsync("claim-2", ClaimStatus.Sent, null, _creditorCaller));
    }

    // --- ModifyAsync Tests ---

    [Fact]
    public async Task ModifyAsync_ValidInCreatedStatus_ModifiesClaim()
    {
        // Arrange
        var existingClaim = new Claim
        {
            Id = "claim-3",
            ClaimReference = "CLM-000003",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 0,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Created,
            CategoryCode = "UTILITY",
            Description = "Original description",
            InsertedBy = "user-1"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-3"))
            .ReturnsAsync(existingClaim);
        _creditorDataAccess.Setup(x => x.GetByIdAsync("creditor-1"))
            .ReturnsAsync(_validCreditor);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), "*"))
            .ReturnsAsync(true);

        var newDueDate = DateTime.UtcNow.AddDays(60);

        // Act
        var result = await _sut.ModifyAsync(
            "claim-3", 20000, newDueDate, "Updated description", "TELECOM", _creditorCaller);

        // Assert
        Assert.NotNull(result);
        _claimDataAccess.Verify(x => x.UpdateAsync(
            It.Is<Claim>(c =>
                c.Amount == 20000 &&
                c.Description == "Updated description" &&
                c.CategoryCode == "TELECOM"),
            "*"), Times.Once);
    }

    [Fact]
    public async Task ModifyAsync_InViewedStatus_ThrowsConflictException()
    {
        // Arrange
        var viewedClaim = new Claim
        {
            Id = "claim-4",
            ClaimReference = "CLM-000004",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 0,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Viewed,
            CategoryCode = "UTILITY",
            Description = "Viewed claim",
            InsertedBy = "user-1"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-4"))
            .ReturnsAsync(viewedClaim);
        _creditorDataAccess.Setup(x => x.GetByIdAsync("creditor-1"))
            .ReturnsAsync(_validCreditor);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.ModifyAsync("claim-4", 20000, null, null, null, _creditorCaller));
    }

    [Fact]
    public async Task ModifyAsync_NonCreditor_ThrowsBusinessRuleException()
    {
        // Arrange
        var existingClaim = new Claim
        {
            Id = "claim-5",
            ClaimReference = "CLM-000005",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 0,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Created,
            CategoryCode = "UTILITY",
            Description = "Test claim",
            InsertedBy = "user-1"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-5"))
            .ReturnsAsync(existingClaim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.ModifyAsync("claim-5", 20000, null, null, null, _nonCreditorCaller));
    }

    // --- CancelAsync Tests ---

    [Fact]
    public async Task CancelAsync_ValidFromSent_CancelsClaim()
    {
        // Arrange
        var sentClaim = new Claim
        {
            Id = "claim-6",
            ClaimReference = "CLM-000006",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 0,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Sent,
            CategoryCode = "UTILITY",
            Description = "Sent claim",
            InsertedBy = "user-1"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-6"))
            .ReturnsAsync(sentClaim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), "*"))
            .ReturnsAsync(true);
        _claimDataAccess.Setup(x => x.InsertStatusHistoryAsync(It.IsAny<ClaimStatusHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CancelAsync("claim-6", "No longer needed", _creditorCaller);

        // Assert
        Assert.NotNull(result);
        _claimDataAccess.Verify(x => x.UpdateAsync(
            It.Is<Claim>(c => c.Status == ClaimStatus.Cancelled), "*"), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_FromPaid_ThrowsConflictException()
    {
        // Arrange
        var paidClaim = new Claim
        {
            Id = "claim-7",
            ClaimReference = "CLM-000007",
            CreditorId = "creditor-1",
            DebtorKennitala = "0987654321",
            CurrencyCode = "ISK",
            Amount = 10000,
            PaidAmount = 10000,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = ClaimStatus.Paid,
            CategoryCode = "UTILITY",
            Description = "Paid claim",
            InsertedBy = "user-1"
        };

        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-7"))
            .ReturnsAsync(paidClaim);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CancelAsync("claim-7", "Trying to cancel paid", _creditorCaller));
    }
}
