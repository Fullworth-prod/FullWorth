using FullWorth.Core.Formatting;
using FullWorth.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FullWorth.ViewModels;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private readonly BillStreamService
        _billStreamService;

    private readonly BillAlertService
        _billAlertService;

    public MainPageViewModel(
        BillStreamService billStreamService,
        BillAlertService billAlertService)
    {
        _billStreamService =
            billStreamService;

        _billAlertService =
            billAlertService;

        OpenActivityCommand =
            new Command(
                async () =>
                    await Shell.Current.GoToAsync(
                        "//Activity"));

        OpenBillsCommand =
            new Command(
                async () =>
                    await Shell.Current.GoToAsync(
                        "//Bills"));

        RefreshCommand =
            new Command(
                async () =>
                    await LoadAsync());
    }

    public int BillsMonitored
    {
        get;
        private set;
    }

    public string TotalMonthlyBills { get; private set; } = "Amount unavailable";

    public string TotalAnnualBills { get; private set; } = "Amount unavailable";

    public int ChangesDetected
    {
        get;
        private set;
    }

    public string AddedAnnualCost { get; private set; } = "Amount unavailable";

    public string ReducedAnnualCost { get; private set; } = "Amount unavailable";

    public int UnreadAlerts
    {
        get;
        private set;
    }

    public int AttentionCount
    {
        get;
        private set;
    }

    public string PrimaryAlertType
    {
        get;
        private set;
    } =
        string.Empty;

    public string PrimaryAlertTitle
    {
        get;
        private set;
    } =
        string.Empty;

    public string PrimaryAlertMessage
    {
        get;
        private set;
    } =
        string.Empty;

    public bool IsLoading
    {
        get;
        private set;
    }

    public string ErrorMessage
    {
        get;
        private set;
    } =
        string.Empty;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public bool HasContent =>
        !IsLoading &&
        !HasError;

    public bool HasBills =>
        BillsMonitored >
        0;

    public bool HasNoBills =>
        !HasBills;

    public bool HasAttention =>
        AttentionCount >
        0;

    public bool HasNoAttention =>
        !HasAttention;

    public string MonthlyBillsText =>
        TotalMonthlyBills;

    public string AnnualBillsText =>
        _hasAnnualBillProjection
            ? TotalAnnualBills + " per year at current monthly amounts"
            : TotalAnnualBills;

    public string AnnualImpactHeadline
    {
        get
        {
            if (_hasAnnualIncrease)
            {
                return
                    AddedAnnualCost + "/year";
            }

            if (_hasAnnualDecrease)
            {
                return
                    ReducedAnnualCost + "/year less";
            }

            return
                "Yearly estimate unavailable";
        }
    }

    public string AnnualImpactCaption
    {
        get
        {
            if (_hasAnnualIncrease)
            {
                return
                    "Estimated yearly increase";
            }

            if (_hasAnnualDecrease)
            {
                return
                    "Estimated yearly reduction";
            }

            return ChangesDetected > 0
                ? "Change detected; yearly estimate unavailable"
                : "No meaningful cost change detected";
        }
    }

    public string MonitoringSummary =>
        BillsMonitored switch
        {
            0 =>
                "No recurring bills monitored yet",

            1 =>
                "1 recurring bill monitored",

            _ =>
                $"{BillsMonitored} recurring bills monitored"
        };

    public string ChangeSummary =>
        ChangesDetected switch
        {
            0 =>
                "No meaningful changes",

            1 =>
                "1 meaningful change",

            _ =>
                $"{ChangesDetected} meaningful changes"
        };

    public string AlertSummary =>
        UnreadAlerts switch
        {
            0 =>
                "No unread alerts",

            1 =>
                "1 unread alert",

            _ =>
                $"{UnreadAlerts} unread alerts"
        };

    public string AttentionHeadline =>
        AttentionCount switch
        {
            0 =>
                "Nothing needs your attention",

            1 =>
                "1 item needs attention",

            _ =>
                $"{AttentionCount} items need attention"
        };

    public string StatusHeadline =>
        HasBills
            ? "Watching"
            : "Ready to watch";

    public string StatusDescription
    {
        get
        {
            if (!HasBills)
            {
                return
                    "Connect a bank or add bill history to start automatic monitoring.";
            }

            if (HasAttention)
            {
                return
                    $"FullWorth is monitoring {BillsMonitored} {(BillsMonitored == 1 ? "bill" : "bills")} and found {AttentionCount} {(AttentionCount == 1 ? "item" : "items")} that need attention.";
            }

            return
                $"FullWorth is monitoring {BillsMonitored} {(BillsMonitored == 1 ? "bill" : "bills")} and nothing currently needs attention.";
        }
    }

    public ICommand OpenActivityCommand
    {
        get;
    }

    public ICommand OpenBillsCommand
    {
        get;
    }

    public ICommand RefreshCommand
    {
        get;
    }

    public async Task LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading =
                true;

            ErrorMessage =
                string.Empty;

            NotifyAll();

            /*
             * These requests are independent, so run them together.
             * Home should feel immediate rather than serially waiting
             * for bill data and then alert data.
             */
            var billStreamsTask =
                _billStreamService
                    .GetBillStreamsAsync(
                        cancellationToken);

            var alertsTask =
                _billAlertService
                    .GetAlertsAsync(
                        includeDismissed:
                            false,

                        unreadOnly:
                            false,

                        take:
                            100,

                        cancellationToken:
                            cancellationToken);

            await Task.WhenAll(
                billStreamsTask,
                alertsTask);

            var billStreams =
                await billStreamsTask;

            var alerts =
                await alertsTask;

            ApplyBillSummary(
                billStreams);

            ApplyAlertSummary(
                alerts);
        }
        catch (SessionExpiredException)
        {
            ErrorMessage =
                "Your FullWorth session expired. Please sign in again.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
                "FullWorth couldn't load your dashboard right now.";
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            ErrorMessage =
                "Something went wrong while loading your dashboard.";
        }
        finally
        {
            IsLoading =
                false;

            NotifyAll();
        }
    }

    private bool _hasAnnualBillProjection;
    private bool _hasAnnualIncrease;
    private bool _hasAnnualDecrease;

    private void ApplyBillSummary(
        IReadOnlyList<BillStreamResult> billStreams)
    {
        var active = billStreams.Where(stream => stream.IsActive).ToArray();
        BillsMonitored = active.Length;

        // These API amounts describe posted debits. A missing/default/credit
        // value or unknown unit is not evidence of a recurring obligation.
        var known = active.Where(stream => stream.HasTransactionEvidence &&
            stream.CurrentAmount > 0m &&
            FinancialAmountFormatter.TryNormalizeCurrency(stream.CurrencyCode, out _)).ToArray();

        // Legacy binding name retained; MainPage labels this as known bill
        // amounts, never a monthly total inferred from an unknown cadence.
        TotalMonthlyBills = FinancialAmountFormatter.FormatTotals(
            known.Select(stream => (stream.CurrentAmount, stream.CurrencyCode))) ?? "Amount unavailable";

        _hasAnnualBillProjection = active.Length > 0 && known.Length == active.Length &&
            known.All(stream => stream.HasMonthlyCadenceEvidence);
        TotalAnnualBills = _hasAnnualBillProjection
            ? FinancialAmountFormatter.FormatTotals(
                known.Select(stream => (stream.CurrentAmount, stream.CurrencyCode)), 12m)
                ?? "Yearly estimate unavailable"
            : "Yearly estimate unavailable";

        // Keep existing deterministic materiality, but never turn missing
        // evidence into a zero balance or an unsupported decrease.
        var meaningfulChanges = known
            .Where(stream => stream.PreviousAverage > 0m)
            .Select(stream => new DashboardBillChange(
                decimal.Round(stream.CurrentAmount - stream.PreviousAverage, 2,
                    MidpointRounding.AwayFromZero), stream.PreviousAverage)
            {
                CurrencyCode = stream.CurrencyCode,
                HasMonthlyCadenceEvidence = stream.HasMonthlyCadenceEvidence
            })
            .Where(change => Math.Abs(change.MonthlyChange) >= 5m &&
                Math.Abs(change.MonthlyChange / change.PreviousAmount) >= 0.10m)
            .ToArray();
        ChangesDetected = meaningfulChanges.Length;

        // Do not silently drop unprojectable changes to manufacture a partial
        // annual headline, including one in the opposite direction.
        var canAnnualizeChanges = meaningfulChanges.Length > 0 &&
            meaningfulChanges.All(change => change.HasMonthlyCadenceEvidence);
        var increases = meaningfulChanges.Where(change => change.MonthlyChange > 0m).ToArray();
        var decreases = meaningfulChanges.Where(change => change.MonthlyChange < 0m).ToArray();
        var added = canAnnualizeChanges && increases.Length > 0
            ? FinancialAmountFormatter.FormatTotals(
                increases.Select(change => (change.MonthlyChange, change.CurrencyCode)), 12m)
            : null;
        var reduced = canAnnualizeChanges && decreases.Length > 0
            ? FinancialAmountFormatter.FormatTotals(
                decreases.Select(change => (Math.Abs(change.MonthlyChange), change.CurrencyCode)), 12m)
            : null;
        AddedAnnualCost = added ?? "Yearly estimate unavailable";
        ReducedAnnualCost = reduced ?? "Yearly estimate unavailable";
        _hasAnnualIncrease = added is not null;
        _hasAnnualDecrease = reduced is not null;
    }

    private void ApplyAlertSummary(
        IReadOnlyList<BillAlertResult> alerts)
    {
        UnreadAlerts =
            alerts.Count(
                alert =>
                    !alert.IsRead);

        var attentionAlerts =
            alerts
                .Where(
                    alert =>
                        IsAttentionSeverity(
                            alert.Severity))
                .OrderBy(
                    alert =>
                        alert.IsRead)
                .ThenByDescending(
                    alert =>
                        IsCritical(
                            alert.Severity))
                .ThenByDescending(
                    alert =>
                        alert.CreatedAtUtc)
                .ToList();

        AttentionCount =
            attentionAlerts.Count;

        var primaryAlert =
            attentionAlerts
                .FirstOrDefault();

        if (primaryAlert is
            null)
        {
            PrimaryAlertType =
                string.Empty;

            PrimaryAlertTitle =
                string.Empty;

            PrimaryAlertMessage =
                string.Empty;

            return;
        }

        PrimaryAlertType =
            FormatAlertType(
                primaryAlert.AlertType);

        PrimaryAlertTitle =
            primaryAlert.Title;

        PrimaryAlertMessage =
            primaryAlert.Message;
    }

    private static bool IsAttentionSeverity(
        string severity)
    {
        return
            string.Equals(
                severity,
                "Warning",
                StringComparison.Ordinal) ||
            string.Equals(
                severity,
                "Critical",
                StringComparison.Ordinal);
    }

    private static bool IsCritical(
        string severity)
    {
        return string.Equals(
            severity,
            "Critical",
            StringComparison.Ordinal);
    }

    private static string FormatAlertType(
        string alertType)
    {
        return alertType switch
        {
            "BillIncrease" =>
                "BILL INCREASE",

            "BillDecrease" =>
                "BILL DECREASE",

            "NewFee" =>
                "NEW FEE",

            "RemovedDiscount" =>
                "DISCOUNT REMOVED",

            "PaymentDue" =>
                "PAYMENT DUE",

            "ConnectionIssue" =>
                "CONNECTION ISSUE",

            _ =>
                "NEEDS ATTENTION"
        };
    }

    private void NotifyAll()
    {
        OnPropertyChanged(
            string.Empty);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }

    private sealed record DashboardBillChange(
        decimal MonthlyChange,
        decimal PreviousAmount)
    {
        public string? CurrencyCode { get; init; }
        public bool HasMonthlyCadenceEvidence { get; init; }
    }
}