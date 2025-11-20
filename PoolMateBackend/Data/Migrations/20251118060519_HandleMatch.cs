using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HandleMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1SourceMatchId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player1SourceType",
                table: "Matches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2SourceMatchId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player2SourceType",
                table: "Matches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1SourceMatchId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player1SourceType",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player2SourceMatchId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player2SourceType",
                table: "Matches");
        }
    }
}
