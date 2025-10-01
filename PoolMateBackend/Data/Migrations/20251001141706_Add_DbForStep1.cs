using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_DbForStep1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayoutTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MinPlayers = table.Column<int>(type: "int", nullable: false),
                    MaxPlayers = table.Column<int>(type: "int", nullable: false),
                    Places = table.Column<int>(type: "int", nullable: false),
                    PercentJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Venues_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VenueId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlayerType = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    BracketType = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    GameType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WinnersRaceTo = table.Column<int>(type: "int", nullable: true),
                    LosersRaceTo = table.Column<int>(type: "int", nullable: true),
                    FinalsRaceTo = table.Column<int>(type: "int", nullable: true),
                    BracketOrdering = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OnlineRegistrationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    BracketSizeEstimate = table.Column<int>(type: "int", nullable: true),
                    FlyerUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FlyerPublicId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EntryFee = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    AdminFee = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    AddedMoney = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    PayoutMode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    PayoutTemplateId = table.Column<int>(type: "int", nullable: true),
                    TotalPrize = table.Column<decimal>(type: "decimal(14,2)", nullable: true),
                    Rules = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BreakFormat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IsStarted = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tournaments_PayoutTemplates_PayoutTemplateId",
                        column: x => x.PayoutTemplateId,
                        principalTable: "PayoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tournaments_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutTemplates_MinPlayers_MaxPlayers_Places",
                table: "PayoutTemplates",
                columns: new[] { "MinPlayers", "MaxPlayers", "Places" });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_IsPublic",
                table: "Tournaments",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_OwnerUserId",
                table: "Tournaments",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_PayoutTemplateId",
                table: "Tournaments",
                column: "PayoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_StartUtc",
                table: "Tournaments",
                column: "StartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Status",
                table: "Tournaments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_VenueId",
                table: "Tournaments",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_City_Country",
                table: "Venues",
                columns: new[] { "City", "Country" });

            migrationBuilder.CreateIndex(
                name: "IX_Venues_CreatedByUserId",
                table: "Venues",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_Name",
                table: "Venues",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropTable(
                name: "PayoutTemplates");

            migrationBuilder.DropTable(
                name: "Venues");
        }
    }
}
