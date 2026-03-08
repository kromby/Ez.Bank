using Ez.Bank.Claims.Entities;
using Ez.Bank.Claims.UseCases;
using Ez.Bank.Claims.UseCases.Ports;
using Ez.Bank.Core;
using Ez.Bank.Core.Exceptions;
using Moq;
using Xunit;

namespace Ez.Bank.UnitTests.Claims;

public class PaymentInteractorTests
{
    private readonly Mock<IClaimDataAccess> _claimDataAccess;
    private readonly Mock<IPaymentDataAccess> _paymentDataAccess;
    private readonly PaymentInteractor _sut;
    private readonly CallerContext _caller = new("user-1", "bank-1", "Admin");

    public PaymentInteractorTests()
    {
        _claimDataAccess = new Mock<IClaimDataAccess>();
        _paymentDataAccess = new Mock<IPaymentDataAccess>();
        _sut = new PaymentInteractor(_claimDataAccess.Object, _paymentDataAccess.Object);
    }

    private static Claim CreateClaim(
        ClaimStatus status = ClaimStatus.Sent,
        long amount = 10000,
        long paidAmount = 0,
        string currencyCode = "ISK",
        long lateFee = 0,
        long accruedInterest = 0)
    {
        return new Claim
        {
            Id = "claim-1",
            Status = status,
            Amount = amount,
            PaidAmount = paidAmount,
            CurrencyCode = currencyCode,
            LateFee = lateFee,
            AccruedInterest = accruedInterest
        };
    }

    [Fact]
    public async Task RecordPayment_ValidPayment_RecordsAndUpdates()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent, amount: 10000, paidAmount: 0);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var (payment, updatedClaim) = await _sut.RecordPaymentAsync(
            "claim-1", 5000, "ISK", "bank-1", "ref-1", _caller);

        // Assert
        Assert.NotNull(payment);
        Assert.Equal(5000, payment.Amount);
        Assert.Equal(ClaimStatus.PartiallyPaid, updatedClaim.Status);
        _paymentDataAccess.Verify(x => x.InsertAsync(It.IsAny<Payment>()), Times.Once);
        _claimDataAccess.Verify(x => x.UpdateAsync(It.IsAny<Claim>(), "*"), Times.Once);
    }

    [Fact]
    public async Task RecordPayment_FullPayment_StatusBecomesPaid()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent, amount: 10000, paidAmount: 0);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var (payment, updatedClaim) = await _sut.RecordPaymentAsync(
            "claim-1", 10000, "ISK", "bank-1", "ref-1", _caller);

        // Assert
        Assert.Equal(ClaimStatus.Paid, updatedClaim.Status);
        Assert.Equal(10000, updatedClaim.PaidAmount);
        _claimDataAccess.Verify(x => x.InsertStatusHistoryAsync(It.IsAny<ClaimStatusHistory>()), Times.Once);
    }

    [Fact]
    public async Task RecordPayment_Overpayment_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent, amount: 10000, paidAmount: 0);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.RecordPaymentAsync("claim-1", 10001, "ISK", "bank-1", "ref-1", _caller));
    }

    [Fact]
    public async Task RecordPayment_CurrencyMismatch_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent, amount: 10000, currencyCode: "ISK");
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.RecordPaymentAsync("claim-1", 5000, "EUR", "bank-1", "ref-1", _caller));
    }

    [Fact]
    public async Task RecordPayment_CancelledClaim_ThrowsConflictException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Cancelled, amount: 10000);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RecordPaymentAsync("claim-1", 5000, "ISK", "bank-1", "ref-1", _caller));
    }

    [Fact]
    public async Task RecordPayment_ConcurrencyConflict_ThrowsConflictException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent, amount: 10000);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RecordPaymentAsync("claim-1", 5000, "ISK", "bank-1", "ref-1", _caller));
    }

    [Fact]
    public async Task RecordPayment_ZeroAmount_ThrowsBusinessRuleException()
    {
        // Arrange
        var claim = CreateClaim(status: ClaimStatus.Sent, amount: 10000);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.RecordPaymentAsync("claim-1", 0, "ISK", "bank-1", "ref-1", _caller));
    }

    [Fact]
    public async Task RecordPayment_WithDueCosts_UsesTotalDue()
    {
        // Arrange: Amount=10000, LateFee=950, AccruedInterest=148 => TotalDue=11098
        var claim = CreateClaim(
            status: ClaimStatus.Overdue,
            amount: 10000,
            paidAmount: 0,
            lateFee: 950,
            accruedInterest: 148);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-1")).ReturnsAsync(claim);
        _claimDataAccess.Setup(x => x.UpdateAsync(It.IsAny<Claim>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act — pay the full total due
        var (payment, updatedClaim) = await _sut.RecordPaymentAsync(
            "claim-1", 11098, "ISK", "bank-1", "ref-1", _caller);

        // Assert
        Assert.Equal(ClaimStatus.Paid, updatedClaim.Status);
        Assert.Equal(11098, payment.Amount);

        // Verify overpayment beyond totalDue is rejected
        var claim2 = CreateClaim(
            status: ClaimStatus.Overdue,
            amount: 10000,
            paidAmount: 0,
            lateFee: 950,
            accruedInterest: 148);
        _claimDataAccess.Setup(x => x.GetByIdAsync("claim-2")).ReturnsAsync(claim2);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.RecordPaymentAsync("claim-2", 11099, "ISK", "bank-1", "ref-1", _caller));
    }
}
