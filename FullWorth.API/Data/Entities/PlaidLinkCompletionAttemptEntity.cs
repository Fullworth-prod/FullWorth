namespace FullWorth.API.Data.Entities;

public enum PlaidLinkCompletionStage
{
    Polling,
    Exchanging,
    Completed,
    Interrupted
}

// One durable, owner-bound permit per Hosted Link session. Never store a
// provider token here or remove an exchange permit to make a retry succeed.
public sealed class PlaidLinkCompletionAttemptEntity
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid AttemptId { get; set; } = Guid.NewGuid();
    public PlaidLinkCompletionStage Stage { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public PlaidLinkSessionEntity Session { get; set; } = null!;
}
