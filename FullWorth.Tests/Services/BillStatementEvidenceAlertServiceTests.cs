using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class BillStatementEvidenceAlertServiceTests
{
    [Fact]
    public void ForeignChange_IsRejected()
    {
        var userId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var change =
            new BillChangeEntity
            {
                UserId =
                    Guid.NewGuid(),

                BillStreamId =
                    billStreamId,

                CurrentStatementId =
                    Guid.NewGuid(),

                ChangeType =
                    BillChangeType.TotalIncrease
            };

        var service =
            new BillStatementEvidenceAlertService();

        Assert.Throws<InvalidOperationException>(
            () =>
                service.BuildDesiredAlerts(
                    userId,
                    billStreamId,
                    "Example Provider",
                    change,
                    [],
                    []));
    }

    [Fact]
    public void OwnedChange_WithoutEvidence_ReturnsNoAlerts()
    {
        var userId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var change =
            new BillChangeEntity
            {
                UserId =
                    userId,

                BillStreamId =
                    billStreamId,

                CurrentStatementId =
                    Guid.NewGuid(),

                ChangeType =
                    BillChangeType.TotalIncrease
            };

        var service =
            new BillStatementEvidenceAlertService();

        var results =
            service.BuildDesiredAlerts(
                userId,
                billStreamId,
                "Example Provider",
                change,
                [],
                []);

        Assert.Empty(
            results);
    }

    [Fact]
    public void NewFee_ReturnsWarningDesiredAlert()
    {
        var userId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var previousStatementId =
            Guid.NewGuid();

        var currentStatementId =
            Guid.NewGuid();

        var change =
            new BillChangeEntity
            {
                UserId =
                    userId,

                BillStreamId =
                    billStreamId,

                PreviousStatementId =
                    previousStatementId,

                CurrentStatementId =
                    currentStatementId,

                ChangeType =
                    BillChangeType.TotalIncrease
            };

        var currentFee =
            new BillLineItemEntity
            {
                UserId =
                    userId,

                BillStatementId =
                    currentStatementId,

                Description =
                    "Late fee",

                Amount =
                    7.50m,

                Category =
                    "Fee"
            };

        var service =
            new BillStatementEvidenceAlertService();

        var alert =
            Assert.Single(
                service.BuildDesiredAlerts(
                    userId,
                    billStreamId,
                    "Example Provider",
                    change,
                    [],
                    [currentFee]));

        Assert.Equal(
            BillAlertContractType.NewFee,
            alert.AlertType);

        Assert.Equal(
            BillAlertContractSeverity.Warning,
            alert.Severity);

        Assert.Contains(
            "new fee",
            alert.Title,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "Late fee",
            alert.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ForeignLineItem_IsRejected()
    {
        var userId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var currentStatementId =
            Guid.NewGuid();

        var change =
            new BillChangeEntity
            {
                UserId =
                    userId,

                BillStreamId =
                    billStreamId,

                CurrentStatementId =
                    currentStatementId,

                ChangeType =
                    BillChangeType.TotalIncrease
            };

        var foreignLineItem =
            new BillLineItemEntity
            {
                UserId =
                    Guid.NewGuid(),

                BillStatementId =
                    currentStatementId,

                Description =
                    "Fee",

                Amount =
                    5m,

                Category =
                    "Fee"
            };

        var service =
            new BillStatementEvidenceAlertService();

        Assert.Throws<InvalidOperationException>(
            () =>
                service.BuildDesiredAlerts(
                    userId,
                    billStreamId,
                    "Example Provider",
                    change,
                    [],
                    [foreignLineItem]));
    }

    [Theory]
    [InlineData("Tax", false)]
    [InlineData("Tax", true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData(" ", true)]
    public void MixedCategoryDescription_DoesNotInventTypedAlerts(string? otherCategory, bool reverse)
    {
        var owner = Guid.NewGuid();
        var stream = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var change = OwnedChange(owner, stream, previousId, currentId);
        var service = new BillStatementEvidenceAlertService();
        BillLineItemEntity[] fees =
        [
            Line(owner, currentId, " Shared charge ", 5m, "Fee"),
            Line(owner, currentId, "shared charge", 3m, otherCategory),
            Line(owner, currentId, "SHARED CHARGE", 2m, "Fee")
        ];
        BillLineItemEntity[] discounts =
        [
            Line(owner, previousId, " Shared credit ", -5m, "Discount"),
            Line(owner, previousId, "shared credit", -3m, otherCategory),
            Line(owner, previousId, "SHARED CREDIT", -2m, "Discount")
        ];
        if (reverse)
        {
            Array.Reverse(fees);
            Array.Reverse(discounts);
        }

        Assert.Empty(service.BuildDesiredAlerts(owner, stream, "Provider", change, [], fees));
        Assert.Empty(service.BuildDesiredAlerts(owner, stream, "Provider", change, discounts, []));
    }

    [Fact]
    public void OffsetMixedCategories_DoNotProveFeeIsNewOrDiscountAbsent()
    {
        var owner = Guid.NewGuid();
        var stream = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var change = OwnedChange(owner, stream, previousId, currentId);
        var service = new BillStatementEvidenceAlertService();
        BillLineItemEntity[] previous =
        [
            Line(owner, previousId, "Account adjustment", 5m, "Fee"),
            Line(owner, previousId, "Account adjustment", -5m, "Discount"),
            Line(owner, previousId, "Loyalty adjustment", -5m, "Discount")
        ];
        BillLineItemEntity[] current =
        [
            Line(owner, currentId, "Account adjustment", 5m, "Fee"),
            Line(owner, currentId, "Loyalty adjustment", -5m, "Discount"),
            Line(owner, currentId, "Loyalty adjustment", 5m, "Fee")
        ];

        Assert.Empty(service.BuildDesiredAlerts(owner, stream, "Provider", change, previous, current));
    }

    [Fact]
    public void HomogeneousCategories_KeepAccurateFeeAndRemovedDiscountTotals()
    {
        var owner = Guid.NewGuid();
        var stream = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var change = OwnedChange(owner, stream, previousId, currentId);
        var alerts = new BillStatementEvidenceAlertService().BuildDesiredAlerts(
            owner, stream, "Provider", change,
            [
                Line(owner, previousId, " Loyalty ", -3m, " Discount "),
                Line(owner, previousId, "loyalty", -2m, "discount")
            ],
            [
                Line(owner, currentId, " Account fee ", 3m, " Fee "),
                Line(owner, currentId, "account fee", 2m, "fee")
            ]);
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, alert => alert.AlertType == BillAlertContractType.NewFee
            && alert.Message.Contains("$5.00", StringComparison.Ordinal));
        Assert.Contains(alerts, alert => alert.AlertType == BillAlertContractType.RemovedDiscount
            && alert.Message.Contains("$5.00", StringComparison.Ordinal));
    }

    private static BillChangeEntity OwnedChange(Guid owner, Guid stream, Guid previous, Guid current) =>
        new()
        {
            UserId = owner,
            BillStreamId = stream,
            PreviousStatementId = previous,
            CurrentStatementId = current,
            ChangeType = BillChangeType.TotalIncrease
        };

    private static BillLineItemEntity Line(Guid owner, Guid statement, string description, decimal amount, string? category) =>
        new()
        {
            UserId = owner,
            BillStatementId = statement,
            Description = description,
            Amount = amount,
            Category = category
        };

}
