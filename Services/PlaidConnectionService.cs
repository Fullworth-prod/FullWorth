namespace FullWorth.Services;

public sealed class PlaidConnectionService
{
    private readonly FullWorthApiClient
        _apiClient;

    private readonly AuthenticationService
        _authenticationService;

    public PlaidConnectionService(
        FullWorthApiClient apiClient,
        AuthenticationService authenticationService)
    {
        _apiClient =
            apiClient;

        _authenticationService =
            authenticationService;
    }

    public async Task<IReadOnlyList<BankConnectionResult>>
        GetConnectionsAsync(
            CancellationToken cancellationToken = default)
    {
        var accessToken =
            await GetRequiredAccessTokenAsync(
                cancellationToken);

        return await _apiClient
            .GetBankConnectionsAsync(
                accessToken,
                cancellationToken);
    }

    public async Task DisconnectAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Bank connection ID is required.",
                nameof(connectionId));
        }

        var accessToken =
            await GetRequiredAccessTokenAsync(
                cancellationToken);

        await _apiClient
            .DisconnectBankConnectionAsync(
                accessToken,
                connectionId,
                cancellationToken);
    }

    public async Task<PlaidHostedLinkResult>
        CreateLinkSessionAsync(
            CancellationToken cancellationToken = default)
    {
        var accessToken =
            await GetRequiredAccessTokenAsync(
                cancellationToken);

        return await _apiClient
            .CreatePlaidLinkSessionAsync(
                accessToken,
                cancellationToken);
    }

    public async Task<PlaidHostedLinkResult>
        CreateUpdateLinkSessionAsync(
            Guid connectionId,
            CancellationToken cancellationToken = default)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Bank connection ID is required.",
                nameof(connectionId));
        }

        var accessToken =
            await GetRequiredAccessTokenAsync(
                cancellationToken);

        return await _apiClient
            .CreatePlaidUpdateLinkSessionAsync(
                accessToken,
                connectionId,
                cancellationToken);
    }

    public async Task<PlaidHostedLinkCompletionResult>
        CompleteLinkSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Plaid Link session ID is required.",
                nameof(sessionId));
        }

        var accessToken =
            await GetRequiredAccessTokenAsync(
                cancellationToken);

        var result = await _apiClient
            .CompletePlaidLinkSessionAsync(
                accessToken,
                sessionId,
                cancellationToken);

        // A terminal Link session alone is not proof of a usable owned bank.
        // Never show a success message for a different session or a missing,
        // inactive or malformed bank. No automatic provider/session retry.
        if (result.SessionId != sessionId ||
            (string.Equals(result.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
             (result.Connection is null || result.Connection.Id == Guid.Empty ||
              !string.Equals(result.Connection.Status, "Active", StringComparison.Ordinal))))
        {
            return new PlaidHostedLinkCompletionResult(sessionId, "RecoveryRequired", null);
        }

        return result;
    }

    public async Task<PlaidConnectionResult>
        ExchangePublicTokenAsync(
            string publicToken,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                publicToken))
        {
            throw new ArgumentException(
                "Plaid public token is required.",
                nameof(publicToken));
        }

        var accessToken =
            await GetRequiredAccessTokenAsync(
                cancellationToken);

        return await _apiClient
            .ExchangePlaidPublicTokenAsync(
                accessToken,
                publicToken,
                cancellationToken);
    }

    private async Task<string>
        GetRequiredAccessTokenAsync(
            CancellationToken cancellationToken)
    {
        return await _authenticationService
            .GetValidAccessTokenAsync(
                cancellationToken);
    }
}
