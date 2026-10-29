using FullWorth.API.Services.Contracts;
using FullWorth.API.Services.Statements;
using FullWorth.Core.Formatting;
using FullWorth.Core.Models;

namespace FullWorth.Tests.Services;

public sealed class FinancialCurrencyAsciiBoundaryTests
{
    [Theory]
    [InlineData("uſd")]
    [InlineData("Uſd")]
    [InlineData("uſD")]
    [InlineData("ſek")]
    [InlineData("aſd")]
    [InlineData("cſd")]
    [InlineData("xſu")]
    [InlineData("ＵＳＤ")]
    [InlineData("US")]
    [InlineData("12X")]
    [InlineData("U\u200bSD")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidRawUnit_CannotBecomeAFormattedCurrency(string? raw)
    {
        Assert.False(FinancialAmountFormatter.TryNormalizeCurrency(raw, out var normalized));
        Assert.Equal(string.Empty, normalized);
        Assert.ThrowsAny<ArgumentException>(() => FinancialAmountFormatter.NormalizeCurrency(raw!));
        Assert.ThrowsAny<ArgumentException>(() => FinancialAmountFormatter.Format(25.50m, raw!));
        Assert.Equal("25.50 (Currency unavailable)",
            FinancialAmountFormatter.FormatOrUnspecified(25.50m, raw));
        Assert.Null(FinancialAmountFormatter.FormatTotals([(25.50m, raw)]));
    }

    [Theory]
    [InlineData("usd", "USD", "$25.50")]
    [InlineData(" UsD ", "USD", "$25.50")]
    [InlineData("eur", "EUR", "EUR 25.50")]
    [InlineData("cAd", "CAD", "CAD 25.50")]
    [InlineData("sek", "SEK", "SEK 25.50")]
    [InlineData("XYZ", "XYZ", "XYZ 25.50")]
    [InlineData("\tusd\r\n", "USD", "$25.50")]
    [InlineData("\u00a0eur\u00a0", "EUR", "EUR 25.50")]
    public void AsciiUnits_PreserveExistingCanonicalizationAndAmounts(
        string raw, string expectedCurrency, string expectedDisplay)
    {
        Assert.True(FinancialAmountFormatter.TryNormalizeCurrency(raw, out var normalized));
        Assert.Equal(expectedCurrency, normalized);
        Assert.Equal(expectedCurrency, FinancialAmountFormatter.NormalizeCurrency(raw));
        Assert.Equal(expectedDisplay, FinancialAmountFormatter.Format(25.50m, raw));
    }

    [Fact]
    public void RejectedAlias_CannotAddAFalseDollarBucketToCurrencyTotals()
    {
        Assert.Equal("EUR 10.00", FinancialAmountFormatter.FormatTotals(
            [(10m, "EUR"), (100m, "uſd")]));
    }

    [Theory]
    [InlineData("uſd")]
    [InlineData("UſD")]
    [InlineData("ſek")]
    [InlineData("ＵＳＤ")]
    [InlineData("US")]
    [InlineData("12X")]
    public async Task DueReminder_RejectsRawUnitBeforeAnyReconciliation(string raw)
    {
        var owner = Guid.NewGuid();
        var stream = Guid.NewGuid();
        var alerts = new RecordingAlerts();
        var service = new BillStatementPaymentDueAlertService(new OwnedBill(owner, stream), alerts);
        var today = new DateOnly(2026, 10, 29);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReconcileAsync(
            owner, stream, today.AddDays(5), 100m, raw, today, DateTimeOffset.UnixEpoch));
        Assert.Equal(0, alerts.Calls);
        Assert.Null(alerts.Scopes);
    }

    [Theory]
    [InlineData(" usd ", "$100.00")]
    [InlineData("eur", "EUR 100.00")]
    [InlineData("cAd", "CAD 100.00")]
    public async Task DueReminder_PreservesAsciiCurrencyWithoutConversion(string raw, string amount)
    {
        var owner = Guid.NewGuid();
        var stream = Guid.NewGuid();
        var alerts = new RecordingAlerts();
        var service = new BillStatementPaymentDueAlertService(new OwnedBill(owner, stream), alerts);
        var today = new DateOnly(2026, 10, 29);
        await service.ReconcileAsync(owner, stream, today.AddDays(5), 100m, raw,
            today, DateTimeOffset.UnixEpoch);
        Assert.Equal(1, alerts.Calls);
        var scope = Assert.Single(alerts.Scopes!);
        var alert = Assert.Single(scope.DesiredAlerts);
        Assert.StartsWith(amount + " is due", alert.Message);
    }

    private sealed class OwnedBill(Guid owner, Guid stream) : IBillStreamReadGateway
    {
        public Task<IReadOnlyList<BillStreamReadRecord>> ListOwnedActiveAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BillStreamReadRecord>>([]);
        public Task<BillStreamReadRecord?> GetOwnedAsync(Guid userId, Guid billStreamId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == owner && billStreamId == stream
                ? new BillStreamReadRecord(stream, "Synthetic provider", BillCategory.Other) : null);
    }

    private sealed class RecordingAlerts : IBillAlertReconciliationGateway
    {
        public int Calls { get; private set; }
        public IReadOnlyCollection<BillAlertReconciliationScope>? Scopes { get; private set; }
        public Task StageReconciliationAsync(Guid userId, Guid billStreamId,
            IReadOnlyCollection<BillAlertReconciliationScope> scopes,
            IReadOnlyCollection<Guid> removeBillChangeIds, DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Scopes = scopes;
            return Task.CompletedTask;
        }
    }
}
