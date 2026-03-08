using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;
using Moq;
using Xunit;

namespace Ez.Bank.UnitTests.Claims;

public class DueCostInteractorTests
{
    private readonly Mock<IClaimDataAccess> _claimDataAccess;
    private readonly Mock<ISystemConfigDataAccess> _systemConfigDataAccess;
    private readonly DueCostInteractor _sut;
    private readonly CallerContext _caller = new("user-1", "bank-1", "Admin");

    public DueCostInteractorTests()
    {
        _claimDataAccess = new Mock<IClaimDataAccess>();
        _systemConfigDataAccess = new Mock<ISystemConfigDataAccess>();
        _sut = new DueCostInteractor(_claimDataAccess.Object, _systemConfigDataAccess.Object);
    }

    private static Claim CreateClaim(
        ClaimStatus status = ClaimStatus.Overdue,
        long amount = 10000,
        long paidAmount = 0,
        long lateFee = 0,
        long accruedInterest = 0,
        DateTime? dueDate = null)
    {
        return new Claim
        {
            Id = "claim-1",
            Status = status,
            Amount = amount,
            PaidAmount = paidAmount,
            LateFee = lateFee,
            AccruedInterest = accruedInterest,
            CurrencyCode = "ISK",
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(-30)
        };
    }

    private void SetupSystemConfig(string lateFee = "950", string interestRate = "12")
    {
        _systemConfigDataAccess.Setup(x => x.GetByKeyAsync("DefaultLateFee"))
            .ReturnsAsync(new SystemConfig { Key = "DefaultLateFee", Value = lateFee });
        _systemConfigDataAccess.Setup(x => x.GetByKeyAsync("DefaultInterestRate"))
            .ReturnsAsync(new SystemConfig { Key = "DefaultInterestRate", Value = interestRate });
    }

    [Fact]
    public async Task Calculate_OverdueClaim_AppliesLateFeeAndInterest()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Overdue, amount: 10000, dueDate: DateTime.UtcNow.AddDays(-30));
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(true);
        SetupSystemConfig(lateFee: "950", interestRate: "12");

        // Act
        var result = await _sut.CalculateAndPersistAsync("claim-1", _caller);

        // Assert
        Assert.Equal(950, result.LateFee);
        Assert.True(result.AccruedInterest > 0);
        _claimDataAccess.Verify(x => x.UpdateAsync(It.IsAny<Claim>(), "*"), Times.Once);
    }

    [Fact]
    public async Task Calculate_LateFeeAlreadyApplied_DoesNotReapply()
    {
        // Arrange: LateFee already set to 950
        var claim = CreateClaim(status: ClaimStatus.Overdue, amount: 10000, lateFee: 950, dueDate: DateTime.UtcNow.AddDays(-30));
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(true);
        SetupSystemConfig(lateFee: "950", interestRate: "12");

        // Act
        var result = await _sut.CalculateAndPersistAsync("claim-1", _caller);

        // Assert: LateFee should remain 950, not be doubled
        Assert.Equal(950, result.LateFee);
        Assert.True(result.AccruedInterest > 0);
    }

    [Fact]
    public async Task Calculate_PaidClaim_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Paid);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CalculateAndPersistAsync("claim-1", _caller));
    }

    [Fact]
    public async Task Calculate_CancelledClaim_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Cancelled);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CalculateAndPersistAsync("claim-1", _caller));
    }

    [Fact]
    public async Task Calculate_NotOverdue_ThrowsBusinessRuleException()
    {
        // Arrange — due date in the future, so not overdue
        var claim = CreateClaim(status: ClaimStatus.Sent, dueDate: DateTime.UtcNow.AddDays(30));
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CalculateAndPersistAsync("claim-1", _caller));
    }

    [Fact]
    public async Task Calculate_InterestFormulaAccuracy()
    {
        // Arrange: 15000 ISK, 12% rate, 30 days overdue
        var dueDate = DateTime.UtcNow.AddDays(-30);
        var claim = CreateClaim(
            status: ClaimStatus.Overdue,
            amount: 15000,
            dueDate: dueDate);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(true);
        SetupSystemConfig(lateFee: "950", interestRate: "12");

        // The interactor uses (DateTime.UtcNow.Date - dueDate).Days
        var expectedDays = (DateTime.UtcNow.Date - dueDate).Days;
        var expectedInterest = (long)Math.Floor(15000 * (12.0 / 100) * ((double)expectedDays / 365));

        // Act
        var result = await _sut.CalculateAndPersistAsync("claim-1", _caller);

        // Assert
        Assert.Equal(expectedInterest, result.AccruedInterest);
    }

    [Fact]
    public async Task Preview_ReturnsCorrectBreakdown_WithoutPersisting()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Overdue, amount: 10000, dueDate: DateTime.UtcNow.AddDays(-30));
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        SetupSystemConfig(lateFee: "950", interestRate: "12");

        // Act
        var preview = await _sut.PreviewAsync("claim-1", _caller);

        // Assert
        Assert.NotNull(preview);
        Assert.Equal(950, preview.LateFee);
        Assert.True(preview.AccruedInterest > 0);

        // Verify no persistence calls were made
        _claimDataAccess.Verify(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>()), Times.Never);
    }
}
