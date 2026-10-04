using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBridge.Persistence.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                table: "DialogTurns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedModel",
                table: "DialogContexts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DialogSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Effort = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogSettings", x => x.Id);
                    table.CheckConstraint("CK_Settings_Effort", "length(\"Effort\") > 0");
                    table.CheckConstraint("CK_Settings_Model", "length(\"Model\") > 0");
                    table.CheckConstraint("CK_Settings_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_DialogSettings_Dialogs_Id",
                        column: x => x.Id,
                        principalTable: "Dialogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogSettings");

            migrationBuilder.DropColumn(
                name: "SettingsJson",
                table: "DialogTurns");

            migrationBuilder.DropColumn(
                name: "SelectedModel",
                table: "DialogContexts");
        }
    }
}
