using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBridge.Persistence.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialAgentBridgeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dialogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IncarnationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    LastChangedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dialogs", x => x.Id);
                    table.CheckConstraint("CK_Dialog_ContentBytes", "\"ContentBytes\" >= 0");
                    table.CheckConstraint("CK_Dialog_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Dialog_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000' AND \"IncarnationId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Dialog_LastChanged", "\"LastChangedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Dialog_Owner", "length(\"OwnerId\") > 0");
                    table.CheckConstraint("CK_Dialog_Revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "DialogContexts",
                columns: table => new
                {
                    DialogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    ThroughTurnSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ResponseContinuationJson = table.Column<string>(type: "text", nullable: true),
                    ResponseEnvelopeJson = table.Column<string>(type: "text", nullable: true),
                    ResponseErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ResponseErrorType = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseFormatVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseOutputJson = table.Column<string>(type: "text", nullable: false),
                    ResponseStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogContexts", x => new { x.DialogId, x.Version });
                    table.CheckConstraint("CK_Context_Completed", "\"ResponseStatus\" = 0");
                    table.CheckConstraint("CK_Context_Prefix", "\"ThroughTurnSequence\" >= 0");
                    table.CheckConstraint("CK_Context_Version", "\"Version\" > 0");
                    table.CheckConstraint("CK_DialogContextRecord_ResponseError", "(\"ResponseStatus\" = 2 AND \"ResponseErrorType\" IS NOT NULL AND \"ResponseErrorType\" BETWEEN 0 AND 8 AND \"ResponseErrorMessage\" IS NOT NULL AND length(\"ResponseErrorMessage\") > 0) OR (\"ResponseStatus\" <> 2 AND \"ResponseErrorType\" IS NULL AND \"ResponseErrorMessage\" IS NULL)");
                    table.CheckConstraint("CK_DialogContextRecord_ResponseStatus", "\"ResponseStatus\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_DialogContextRecord_ResponseVersion", "\"ResponseFormatVersion\" = 1");
                    table.ForeignKey(
                        name: "FK_DialogContexts_Dialogs_DialogId",
                        column: x => x.DialogId,
                        principalTable: "Dialogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DialogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogTurns", x => new { x.DialogId, x.Id });
                    table.CheckConstraint("CK_Turn_Finished", "(\"Status\" = 0 AND \"FinishedAtUtc\" IS NULL) OR (\"Status\" <> 0 AND \"FinishedAtUtc\" IS NOT NULL AND \"FinishedAtUtc\" >= \"StartedAtUtc\")");
                    table.CheckConstraint("CK_Turn_Sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("CK_Turn_Status", "\"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_DialogTurns_Dialogs_DialogId",
                        column: x => x.DialogId,
                        principalTable: "Dialogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalItems",
                columns: table => new
                {
                    DialogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalItems", x => new { x.DialogId, x.TurnId, x.Sequence });
                    table.CheckConstraint("CK_Item_Sequence", "\"Sequence\" > 0");
                    table.ForeignKey(
                        name: "FK_CanonicalItems_DialogTurns_DialogId_TurnId",
                        columns: x => new { x.DialogId, x.TurnId },
                        principalTable: "DialogTurns",
                        principalColumns: new[] { "DialogId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DialogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    ResponseContinuationJson = table.Column<string>(type: "text", nullable: true),
                    ResponseEnvelopeJson = table.Column<string>(type: "text", nullable: true),
                    ResponseErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ResponseErrorType = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseFormatVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseOutputJson = table.Column<string>(type: "text", nullable: false),
                    ResponseStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelSteps", x => new { x.DialogId, x.TurnId, x.Id });
                    table.CheckConstraint("CK_ModelStepRecord_ResponseError", "(\"ResponseStatus\" = 2 AND \"ResponseErrorType\" IS NOT NULL AND \"ResponseErrorType\" BETWEEN 0 AND 8 AND \"ResponseErrorMessage\" IS NOT NULL AND length(\"ResponseErrorMessage\") > 0) OR (\"ResponseStatus\" <> 2 AND \"ResponseErrorType\" IS NULL AND \"ResponseErrorMessage\" IS NULL)");
                    table.CheckConstraint("CK_ModelStepRecord_ResponseStatus", "\"ResponseStatus\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ModelStepRecord_ResponseVersion", "\"ResponseFormatVersion\" = 1");
                    table.CheckConstraint("CK_Step_Sequence", "\"Sequence\" > 0");
                    table.ForeignKey(
                        name: "FK_ModelSteps_DialogTurns_DialogId_TurnId",
                        columns: x => new { x.DialogId, x.TurnId },
                        principalTable: "DialogTurns",
                        principalColumns: new[] { "DialogId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dialogs_ExpiresAtUtc_Id",
                table: "Dialogs",
                columns: new[] { "ExpiresAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogTurns_DialogId_Sequence",
                table: "DialogTurns",
                columns: new[] { "DialogId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelSteps_DialogId_TurnId_Sequence",
                table: "ModelSteps",
                columns: new[] { "DialogId", "TurnId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanonicalItems");

            migrationBuilder.DropTable(
                name: "DialogContexts");

            migrationBuilder.DropTable(
                name: "ModelSteps");

            migrationBuilder.DropTable(
                name: "DialogTurns");

            migrationBuilder.DropTable(
                name: "Dialogs");
        }
    }
}
