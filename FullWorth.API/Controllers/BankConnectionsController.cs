using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Plaid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Controllers;

[ApiController]
[Route("api/bank-connections")]
[Authorize]
public sealed class BankConnectionsController : ControllerBase
{
    private readonly FullWorthDbContext
        _dbContext;

    private readonly PlaidConnectionDisconnectService
        _disconnectService;

    private readonly UserManager<ApplicationUser>
        _userManager;

    public BankConnectionsController(
        FullWorthDbContext dbContext,
        PlaidConnectionDisconnectService disconnectService,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext =
            dbContext;

        _disconnectService =
            disconnectService;

        _userManager =
            userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetConnections(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(
                out var userId))
        {
            return Unauthorized();
        }

        var connections =
            await _dbContext.BankConnections
                .AsNoTracking()
                .Where(connection =>
                    connection.UserId == userId)
                .OrderByDescending(connection =>
                    connection.CreatedAtUtc)
                .Select(connection =>
                    new BankConnectionResult(
                        connection.Id,
                        connection.InstitutionName,
                        connection.Status,
                        connection.LastSuccessfulSyncAtUtc,
                        connection.CreatedAtUtc,
                        connection.IsArchived && connection.Status == BankConnectionStatus.Disconnected))
                .ToListAsync(
                    cancellationToken);

        return Ok(
            connections);
    }

    [HttpDelete("{connectionId:guid}")]
    public async Task<IActionResult> Disconnect(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(
                out var userId))
        {
            return Unauthorized();
        }

        var disconnected =
            await _disconnectService.DisconnectAsync(
                userId,
                connectionId,
                cancellationToken);

        if (!disconnected)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{connectionId:guid}/archive")]
    public Task<IActionResult> Archive(Guid connectionId, CancellationToken cancellationToken) =>
        SetArchivedAsync(connectionId, true, cancellationToken);

    [HttpPost("{connectionId:guid}/restore")]
    public Task<IActionResult> Restore(Guid connectionId, CancellationToken cancellationToken) =>
        SetArchivedAsync(connectionId, false, cancellationToken);

    private async Task<IActionResult> SetArchivedAsync(
        Guid connectionId, bool archived, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (connectionId == Guid.Empty) return NotFound();

        var connection = await _dbContext.BankConnections.SingleOrDefaultAsync(
            item => item.Id == connectionId && item.UserId == userId, cancellationToken);
        if (connection is null) return NotFound();

        // An ended status alone does not prove that provider revocation completed.
        // Never hide a connection whose provider access still needs to be removed.
        if (connection.Status != BankConnectionStatus.Disconnected ||
            !string.IsNullOrEmpty(connection.ProtectedPlaidAccessToken))
            return Conflict(new { message = "Disconnect this bank successfully before archiving or restoring it." });

        if (connection.IsArchived != archived)
        {
            connection.IsArchived = archived;
            connection.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        // This operation does not change connection status, credentials, accounts or transactions.
        return NoContent();
    }

    private bool TryGetUserId(
        out Guid userId)
    {
        var userIdText =
            _userManager.GetUserId(
                User);

        return Guid.TryParse(
            userIdText,
            out userId) && userId != Guid.Empty;
    }
}

public sealed record BankConnectionResult(
    Guid Id,
    string InstitutionName,
    BankConnectionStatus Status,
    DateTimeOffset? LastSuccessfulSyncAtUtc,
    DateTimeOffset CreatedAtUtc,
    bool IsArchived = false);