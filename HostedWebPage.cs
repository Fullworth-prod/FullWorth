using FullWorth.Core.Configuration;
using Microsoft.Maui.ApplicationModel;

namespace FullWorth;

/// <summary>
/// Hosts FullWorth.Web rather than recreating its pages, forms or authentication.
/// No script bridge, token injection, cookie export or local copy of the site.
/// </summary>
public sealed class HostedWebPage : ContentPage
{
    private readonly WebView _webView;
    private readonly Label _message;
    private readonly Button _retry;
    private readonly Button _openExternal;
    private readonly VerticalStackLayout _notice;
    private readonly ActivityIndicator _loading;
    private CancellationTokenSource? _loadTimeout;
    private Uri? _externalUri;
    private bool _started;

    public HostedWebPage()
    {
        Title = "FullWorth";
        BackgroundColor = Color.FromArgb("#060910");
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;

        _webView = new WebView
        {
            BackgroundColor = Color.FromArgb("#060910"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _message = new Label { TextColor = Colors.White, FontSize = 14 };
        _retry = new Button { Text = "Try again", IsVisible = false };
        _retry.Clicked += (_, _) => LoadHome();
        _openExternal = new Button { Text = "Open link in browser", IsVisible = false };
        _openExternal.Clicked += OnOpenExternal;
        _notice = new VerticalStackLayout
        {
            Padding = new Thickness(16, 8),
            Spacing = 6,
            IsVisible = false,
            Children = { _message, _retry, _openExternal }
        };
        _loading = new ActivityIndicator
        {
            Color = Color.FromArgb("#A9B8FF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 12)
        };

        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        layout.Add(_notice, 0, 0);
        layout.Add(_webView, 0, 1);
        layout.Add(_loading, 0, 1);
        Content = layout;

        _webView.Navigating += OnNavigating;
        _webView.Navigated += OnNavigated;
        _webView.ProcessTerminated += (_, _) =>
            ShowFailure("FullWorth's web view stopped. Try again to reopen the website.");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_started)
        {
            _started = true;
            LoadHome();
        }
        else if (_loading.IsRunning)
        {
            StartLoadTimeout();
        }
    }

    protected override void OnDisappearing()
    {
        StopLoadTimeout();
        base.OnDisappearing();
    }

    private void LoadHome()
    {
        ClearNotice();
        SetLoading(true);
        // Always issue a fresh GET. Never automatically replay a form POST.
        _webView.Source = new UrlWebViewSource { Url = FullWorthWebNavigation.EntryPoint.AbsoluteUri };
        StartLoadTimeout();
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        switch (FullWorthWebNavigation.Classify(e.Url))
        {
            case FullWorthWebDestination.InApp:
                ClearNotice();
                SetLoading(true);
                StartLoadTimeout();
                return;
            case FullWorthWebDestination.ExternalBrowser:
                e.Cancel = true;
                StopLoadTimeout();
                SetLoading(false);
                _externalUri = new Uri(e.Url);
                _message.Text = "This link leaves FullWorth. It can be opened in your browser; browser sign-in is separate from this app.";
                _notice.IsVisible = true;
                _retry.IsVisible = false;
                _openExternal.IsVisible = true;
                return;
            default:
                e.Cancel = true;
                ShowFailure("FullWorth blocked an unsupported link. Return to the website to continue.");
                return;
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // Ignore completions from cancelled/external navigations.
        if (FullWorthWebNavigation.Classify(e.Url) != FullWorthWebDestination.InApp)
            return;

        StopLoadTimeout();
        SetLoading(false);
        if (e.Result != WebNavigationResult.Success)
            ShowFailure("FullWorth could not load. Check your connection and try again.");
    }

    private async void OnOpenExternal(object? sender, EventArgs e)
    {
        var uri = _externalUri;
        if (uri is null ||
            FullWorthWebNavigation.Classify(uri.AbsoluteUri) != FullWorthWebDestination.ExternalBrowser)
            return;

        _openExternal.IsEnabled = false;
        try
        {
            if (!await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External))
                ShowFailure("The browser could not open this link.");
        }
        catch
        {
            // Never display URLs, query strings, session data or exception details.
            ShowFailure("The browser could not open this link.");
        }
        finally
        {
            _openExternal.IsEnabled = true;
        }
    }

    private void ClearNotice()
    {
        _externalUri = null;
        _notice.IsVisible = false;
        _openExternal.IsVisible = false;
        _retry.IsVisible = false;
    }

    private void ShowFailure(string message)
    {
        StopLoadTimeout();
        SetLoading(false);
        _externalUri = null;
        _message.Text = message;
        _openExternal.IsVisible = false;
        _retry.IsVisible = true;
        _notice.IsVisible = true;
    }

    private void SetLoading(bool loading)
    {
        _loading.IsRunning = loading;
        _loading.IsVisible = loading;
    }

    private void StopLoadTimeout()
    {
        _loadTimeout?.Cancel();
        _loadTimeout?.Dispose();
        _loadTimeout = null;
    }

    private void StartLoadTimeout()
    {
        StopLoadTimeout();
        _loadTimeout = new CancellationTokenSource();
        _ = WatchLoadAsync(_loadTimeout.Token);
    }

    private async Task WatchLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                ShowFailure("FullWorth is taking longer than expected. You can wait, or try again to reopen the website.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_webView.CanGoBack) return base.OnBackButtonPressed();
        _webView.GoBack();
        return true;
    }
}
