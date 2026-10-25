namespace FullWorth.API.Data.Entities;

// Monitoring choice and bank recurrence stay separate from subscription assertions.
public sealed class SubscriptionReviewEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid Revision { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string PlanLabel { get; set; } = string.Empty;
    public string BillingParty { get; set; } = string.Empty;
    public Guid? SourceTransactionId { get; set; }
    public Guid? BillStreamId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public BankTransactionEntity? SourceTransaction { get; set; }
    public BillStreamEntity? BillStream { get; set; }
}

public sealed class SubscriptionEvidenceEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ReviewId { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? SourceUploadId { get; set; }
    public string FactsJson { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public SubscriptionReviewEntity Review { get; set; } = null!;
    public BillStatementUploadEntity? SourceUpload { get; set; }
}
