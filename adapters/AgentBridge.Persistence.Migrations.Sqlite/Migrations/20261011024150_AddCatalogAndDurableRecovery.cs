using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBridge.Persistence.Migrations.Sqlite.Migrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeJson",
                table: "Dialogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecoveryRevision",
                table: "DialogContexts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SavedAtUtc",
                table: "CanonicalItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DialogCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "BINARY"),
                    IdSortKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, collation: "BINARY"),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<string>(type: "text", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    IncarnationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    LastMessageAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    SortTimeUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstQuestionPosition = table.Column<string>(type: "TEXT", nullable: true),
                    LastMessagePosition = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SearchKey = table.Column<string>(type: "TEXT", nullable: false),
                    Snippet = table.Column<string>(type: "TEXT", nullable: false),
                    ProfileJson = table.Column<string>(type: "text", nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Readiness = table.Column<int>(type: "INTEGER", nullable: false),
                    Epoch = table.Column<long>(type: "INTEGER", nullable: false),
                    RecoveryRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    LastTurnId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastTurnStatus = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialogCatalogChanges",
                columns: table => new
                {
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "BINARY"),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogCatalogChanges", x => new { x.ScopeKey, x.Sequence });
                });

            migrationBuilder.CreateTable(
                name: "DialogCatalogClocks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "BINARY"),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogCatalogClocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialogRecoveryOperations",
                columns: table => new
                {
                    DialogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IncarnationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "BINARY"),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    PairsJson = table.Column<string>(type: "text", nullable: false)
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
