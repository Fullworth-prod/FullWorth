using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FullWorth.Services;

public sealed class BillAlertService
{
    private readonly HttpClient
        _httpClient;

    private readonly AuthenticationService
        _authenticationService;

    public BillAlertService(
        HttpClient httpClient,
        AuthenticationService authenticationService)
    {
        _httpClient =
            httpClient;

        _authenticationService =
            authenticationService;
    }

    public async Task<IReadOnlyList<BillAlertResult>>
        GetAlertsAsync(
            bool includeDismissed = false,
            bool unreadOnly = false,
            int take = 50,
            CancellationToken cancellationToken = default)
    {
        take =
            Math.Clamp(
                take,
                1,
                100);

        var accessToken =
            await _authenticationService
                .GetValidAccessTokenAsync(
                    cancellationToken);

        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/alerts?includeDismissed={includeDismissed.ToString().ToLowerInvariant()}&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}&take={take}",
                accessToken);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();
        if (response.StatusCode != System.Net.HttpStatusCode.OK ||
            !string.Equals(response.Content.Headers.ContentType?.MediaType,
                "application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The alert response was not a confirmed JSON snapshot.");
        }

        var alerts =
            await response.Content
                .ReadFromJsonAsync<List<BillAlertResult>>(
                    cancellationToken:
                        cancellationToken);

        if (alerts is null || alerts.Any(alert => alert is null ||
                alert.Id == Guid.Empty || string.IsNullOrWhiteSpace(alert.AlertType) ||
                string.IsNullOrWhiteSpace(alert.Severity) || string.IsNullOrWhiteSpace(alert.Title) ||
                string.IsNullOrWhiteSpace(alert.Message) || alert.CreatedAtUtc == default ||
                alert.UpdatedAtUtc == default) ||
            alerts.Select(alert => alert.Id).Distinct().Count() != alerts.Count)
        {
            throw new InvalidDataException("The alert response contained invalid records.");
        }
        return alerts;
    }

    public async Task MarkReadAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        if (alertId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Alert ID is required.",
                nameof(alertId));
        }

        await SendMutationAsync(
            $"/api/alerts/{alertId}/read",
            cancellationToken);
    }

    public async Task DismissAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        if (alertId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Alert ID is required.",
                nameof(alertId));
        }

        await SendMutationAsync(
            $"/api/alerts/{alertId}/dismiss",
            cancellationToken);
    }

    private async Task SendMutationAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        var accessToken =
            await _authenticationService
                .GetValidAccessTokenAsync(
                    cancellationToken);

        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                requestUri,
                accessToken);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            throw new InvalidDataException("The alert update could not be confirmed.");
        }

        // Read at most one byte: an unexpected body cannot confirm a 204
        // receipt, and no response content is reflected or replayed.
        using var receipt = await response.Content.ReadAsStreamAsync(cancellationToken);
        var firstByte = new byte[1];
        if (await receipt.ReadAsync(firstByte.AsMemory(), cancellationToken) != 0)
        {
            throw new InvalidDataException("The alert update could not be confirmed.");
        }
    }

    private static HttpRequestMessage
        CreateAuthorizedRequest(
            HttpMethod method,
            string requestUri,
            string accessToken)
    {
        var request =
            new HttpRequestMessage(
                method,
                requestUri);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        return request;
    }
}

public sealed record BillAlertResult(
    Guid Id,
    Guid? BillStreamId,
    Guid? BillChangeId,
    string AlertType,
    string Severity,
    string Title,
    string Message,
    bool IsRead,
    bool IsDismissed,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);