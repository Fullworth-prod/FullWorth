using FullWorth.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FullWorth.API.Data.Migrations;

[DbContext(typeof(FullWorthDbContext))]
[Migration("20261025210000_AddSubscriptionVerification")]
public sealed class AddSubscriptionVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint("AK_BankTransactions_Id_UserId", "BankTransactions", new[] { "Id", "UserId" });
        migrationBuilder.CreateTable(
            name: "SubscriptionReviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Revision = table.Column<Guid>(type: "uuid", nullable: false),
                MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PlanLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                BillingParty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                SourceTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                BillStreamId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubscriptionReviews", x => x.Id);
                table.UniqueConstraint("AK_SubscriptionReviews_Id_UserId", x => new { x.Id, x.UserId });
                table.ForeignKey("FK_SubscriptionReviews_AspNetUsers_UserId", x => x.UserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SubscriptionReviews_Bill_Owner", x => new { x.BillStreamId, x.UserId },
                    "BillStreams", new[] { "Id", "UserId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SubscriptionReviews_Transaction_Owner", x => new { x.SourceTransactionId, x.UserId },
                    "BankTransactions", new[] { "Id", "UserId" }, onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateTable(
            name: "SubscriptionEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                SourceUploadId = table.Column<Guid>(type: "uuid", nullable: true),
                FactsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubscriptionEvidence", x => x.Id);
                table.ForeignKey("FK_SubscriptionEvidence_SubscriptionReviews_ReviewId_UserId",
                    x => new { x.ReviewId, x.UserId }, "SubscriptionReviews", new[] { "Id", "UserId" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SubscriptionEvidence_Upload_Owner",
                    x => new { x.SourceUploadId, x.UserId }, "BillStatementUploads", new[] { "Id", "UserId" },
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_SubscriptionReviews_UserId_CreatedAtUtc", "SubscriptionReviews",
            new[] { "UserId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_SubscriptionReviews_BillStreamId_UserId", "SubscriptionReviews",
            new[] { "BillStreamId", "UserId" });
        migrationBuilder.CreateIndex("IX_SubscriptionReviews_SourceTransactionId_UserId", "SubscriptionReviews",
            new[] { "SourceTransactionId", "UserId" });
        migrationBuilder.CreateIndex("IX_SubscriptionEvidence_ReviewId_UserId", "SubscriptionEvidence",
            new[] { "ReviewId", "UserId" });
        migrationBuilder.CreateIndex("IX_SubscriptionEvidence_SourceUploadId_UserId", "SubscriptionEvidence",
            new[] { "SourceUploadId", "UserId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SubscriptionEvidence");
        migrationBuilder.DropTable("SubscriptionReviews");
        migrationBuilder.DropUniqueConstraint("AK_BankTransactions_Id_UserId", "BankTransactions");
    }
}
