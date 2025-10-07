using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFKTournamentPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentPlayers_Players_PlayerId",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_TournamentId_PlayerId",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "TournamentPlayers");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "TournamentPlayers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "TournamentPlayers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "TournamentPlayers",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "TournamentPlayers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "TournamentPlayers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "TournamentPlayers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "TournamentPlayers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillLevel",
                table: "TournamentPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SkillLevel",
                table: "Players",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Players",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TournamentId",
                table: "TournamentPlayers",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TournamentId_PlayerId",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true,
                filter: "[PlayerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TournamentId_Seed",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "Seed" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TournamentId_Status",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentPlayers_Players_PlayerId",
                table: "TournamentPlayers",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentPlayers_Players_PlayerId",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_TournamentId",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_TournamentId_PlayerId",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_TournamentId_Seed",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_TournamentId_Status",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "SkillLevel",
                table: "TournamentPlayers");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "TournamentPlayers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "TournamentPlayers",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SkillLevel",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TournamentId_PlayerId",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentPlayers_Players_PlayerId",
                table: "TournamentPlayers",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }
    }
}
