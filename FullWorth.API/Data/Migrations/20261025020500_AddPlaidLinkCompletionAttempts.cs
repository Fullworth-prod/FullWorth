using FullWorth.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FullWorth.API.Data.Migrations;

[DbContext(typeof(FullWorthDbContext))]
[Migration("20261025020500_AddPlaidLinkCompletionAttempts")]
public sealed class AddPlaidLinkCompletionAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_PlaidLinkSessions_Id_UserId",
            table: "PlaidLinkSessions", columns: new[] { "Id", "UserId" });

        migrationBuilder.CreateTable(
            name: "PlaidLinkCompletionAttempts",
            columns: table => new
            {
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                Stage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlaidLinkCompletionAttempts", x => new { x.SessionId, x.UserId });
                table.ForeignKey(
                    name: "FK_PlaidCompletionAttempts_LinkSession_Owner",
                    columns: x => new { x.SessionId, x.UserId },
                    principalTable: "PlaidLinkSessions",
                    principalColumns: new[] { "Id", "UserId" },
                    onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(
            name: "IX_PlaidLinkCompletionAttempts_AttemptId",
            table: "PlaidLinkCompletionAttempts", column: "AttemptId", unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PlaidLinkCompletionAttempts_UserId",
            table: "PlaidLinkCompletionAttempts", column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlaidLinkCompletionAttempts");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_PlaidLinkSessions_Id_UserId", table: "PlaidLinkSessions");
    }
}
