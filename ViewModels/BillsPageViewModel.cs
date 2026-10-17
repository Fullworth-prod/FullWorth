using FullWorth.Core.Formatting;
using FullWorth.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FullWorth.ViewModels;

public sealed class BillsPageViewModel : INotifyPropertyChanged
{
    private readonly BillStreamService _billStreamService;

    private int _billsMonitored;
    private string _monthlyTotal = "Amount unavailable";
    private IReadOnlyList<BillListItem> _bills = [];
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public BillsPageViewModel(
        BillStreamService billStreamService)
    {
        _billStreamService = billStreamService;
    }

    public int BillsMonitored
    {
        get => _billsMonitored;
        private set
        {
            if (_billsMonitored == value)
            {
                return;
            }

            _billsMonitored = value;
            OnPropertyChanged();
        }
    }

    public string MonthlyTotal
    {
        get => _monthlyTotal;
        private set
        {
            if (_monthlyTotal == value)
            {
                return;
            }

            _monthlyTotal = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BillListItem> Bills
    {
        get => _bills;
        private set
        {
            if (ReferenceEquals(_bills, value))
            {
                return;
            }

            _bills = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var billStreams =
                await _billStreamService.GetBillStreamsAsync(
                    cancellationToken);

            Bills = billStreams
                .Select(stream =>
                {
                    var monthlyChange =
                        stream.PreviousAverage <= 0m ? 0m : decimal.Round(
                            stream.CurrentAmount -
                            stream.PreviousAverage,
                            2,
                            MidpointRounding.AwayFromZero);

                    var annualImpact =
                        !stream.HasMonthlyCadenceEvidence ? 0m : decimal.Round(
                            monthlyChange * 12m,
                            2,
                            MidpointRounding.AwayFromZero);

                    var hasMeaningfulChange =
                        stream.PreviousAverage > 0m &&
                        Math.Abs(monthlyChange) >= 5m &&
                        Math.Abs(
                            monthlyChange /
                            stream.PreviousAverage) >= 0.10m;

                    return new BillListItem(
                        Id: stream.Id,
                        ProviderName: stream.ProviderName,
                        Category: FormatCategory(stream.Category),
                        CurrentAmount: stream.CurrentAmount,
                        PreviousAverage: stream.PreviousAverage,
                        MonthlyChange: monthlyChange,
                        AnnualImpact: annualImpact,
                        HasMeaningfulChange: hasMeaningfulChange,
                        Status: hasMeaningfulChange
                            ? "Changed"
                            : stream.IsActive
                                ? "Watching"
                                : "Inactive")
                    {
                        CurrencyCode = stream.CurrencyCode,
                        HasTransactionEvidence = stream.HasTransactionEvidence,
                        HasMonthlyCadenceEvidence = stream.HasMonthlyCadenceEvidence
                    };
                })
                .OrderBy(
                    bill => bill.ProviderName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

            BillsMonitored = Bills.Count;

            MonthlyTotal = FinancialAmountFormatter.FormatTotals(Bills
                .Where(bill => bill.HasTransactionEvidence)
                .Select(bill => (bill.CurrentAmount, bill.CurrencyCode))) ?? "Amount unavailable";
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
                "Unable to load your bills from FullWorth.";
        }
        catch (Exception)
        {
            ErrorMessage =
                "Something went wrong while loading your bills.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatCategory(
        string category)
    {
        return category switch
        {
            "MobilePhone" => "Mobile phone",
            "NaturalGas" => "Natural gas",
            _ => category
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record BillListItem(
    Guid Id,
    string ProviderName,
    string Category,
    decimal CurrentAmount,
    decimal PreviousAverage,
    decimal MonthlyChange,
    decimal AnnualImpact,
    bool HasMeaningfulChange,
    string Status)
{
    public string? CurrencyCode { get; init; }
    public bool HasTransactionEvidence { get; init; }
    public bool HasMonthlyCadenceEvidence { get; init; }
    public string CurrentAmountText => HasTransactionEvidence
        ? FinancialAmountFormatter.FormatOrUnspecified(CurrentAmount, CurrencyCode) : "Amount unavailable";
    public string PreviousAverageText => PreviousAverage > 0m
        ? "Previous average " + FinancialAmountFormatter.FormatOrUnspecified(PreviousAverage, CurrencyCode)
        : "Building historical average";
    public string ChangeText => FinancialAmountFormatter.FormatOrUnspecified(MonthlyChange, CurrencyCode) + " per cycle";
    public string AnnualImpactText => HasMonthlyCadenceEvidence &&
        FinancialAmountFormatter.TryNormalizeCurrency(CurrencyCode, out _)
            ? FinancialAmountFormatter.FormatOrUnspecified(AnnualImpact, CurrencyCode) + "/year"
            : "Yearly estimate unavailable";
}