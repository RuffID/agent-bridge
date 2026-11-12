using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBridge.Persistence.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndDurableRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CatalogRegistered",
                table: "Dialogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeJson",
                table: "Dialogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecoveryRevision",
                table: "DialogContexts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SavedAtUtc",
                table: "CanonicalItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DialogCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    IdSortKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    OwnerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncarnationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RootRevision = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "bigint", nullable: true),
                    LastMessageAtUtc = table.Column<long>(type: "bigint", nullable: true),
                    SortTimeUtc = table.Column<long>(type: "bigint", nullable: false),
                    FirstQuestionPosition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastMessagePosition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Snippet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    Readiness = table.Column<int>(type: "int", nullable: false),
                    Epoch = table.Column<long>(type: "bigint", nullable: false),
                    RecoveryRevision = table.Column<long>(type: "bigint", nullable: false),
                    LastTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastTurnStatus = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialogCatalogChanges",
                columns: table => new
                {
                    ScopeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogCatalogChanges", x => new { x.ScopeKey, x.Sequence });
                });

            migrationBuilder.CreateTable(
                name: "DialogCatalogClocks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogCatalogClocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialogRecoveryOperations",
                columns: table => new
                {
                    DialogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncarnationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PairsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogRecoveryOperations", x => new { x.DialogId, x.Id });
                    table.ForeignKey(
                        name: "FK_DialogRecoveryOperations_Dialogs_DialogId",
                        column: x => x.DialogId,
                        principalTable: "Dialogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogCatalog_ScopeKey_SortTimeUtc_IdSortKey",
                table: "DialogCatalog",
                columns: new[] { "ScopeKey", "SortTimeUtc", "IdSortKey" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_DialogRecoveryOperations_DialogId_Revision",
                table: "DialogRecoveryOperations",
                columns: new[] { "DialogId", "Revision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogCatalog");

            migrationBuilder.DropTable(
                name: "DialogCatalogChanges");

            migrationBuilder.DropTable(
                name: "DialogCatalogClocks");

            migrationBuilder.DropTable(
                name: "DialogRecoveryOperations");

            migrationBuilder.DropColumn(
                name: "CatalogRegistered",
                table: "Dialogs");

            migrationBuilder.DropColumn(
                name: "RuntimeJson",
                table: "Dialogs");

            migrationBuilder.DropColumn(
                name: "RecoveryRevision",
                table: "DialogContexts");

            migrationBuilder.DropColumn(
                name: "SavedAtUtc",
                table: "CanonicalItems");
        }
    }
}
