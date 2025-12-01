using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "PayoutTemplates",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutTemplates_OwnerUserId",
                table: "PayoutTemplates",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayoutTemplates_AspNetUsers_OwnerUserId",
                table: "PayoutTemplates",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayoutTemplates_AspNetUsers_OwnerUserId",
                table: "PayoutTemplates");

            migrationBuilder.DropIndex(
                name: "IX_PayoutTemplates_OwnerUserId",
                table: "PayoutTemplates");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "PayoutTemplates");
        }
    }
}
