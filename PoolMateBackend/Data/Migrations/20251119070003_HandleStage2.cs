using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HandleStage2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "TournamentStages",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TournamentStages");
        }
    }
}
