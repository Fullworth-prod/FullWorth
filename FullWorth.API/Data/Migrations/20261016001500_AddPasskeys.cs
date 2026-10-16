using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace FullWorth.API.Data.Migrations;

public partial class AddPasskeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetUserPasskeys",
            columns: table => new
            {
                CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Data = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserPasskeys", x => x.CredentialId);
                table.ForeignKey("FK_AspNetUserPasskeys_AspNetUsers_UserId",
                    x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_AspNetUserPasskeys_UserId", "AspNetUserPasskeys", "UserId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("AspNetUserPasskeys");
}
