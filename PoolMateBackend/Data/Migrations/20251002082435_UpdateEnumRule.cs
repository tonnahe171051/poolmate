using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEnumRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Tournaments");

            migrationBuilder.AddColumn<int>(
                name: "Rule",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rule",
                table: "Tournaments");

            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "Tournaments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
