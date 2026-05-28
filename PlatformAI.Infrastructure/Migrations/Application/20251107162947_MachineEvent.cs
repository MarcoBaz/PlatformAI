using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class MachineEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionEvents_Machines_MachineId",
                table: "ProductionEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductionEvents",
                table: "ProductionEvents");

            migrationBuilder.RenameTable(
                name: "ProductionEvents",
                newName: "MachineEvent");

            migrationBuilder.RenameIndex(
                name: "IX_ProductionEvents_MachineId",
                table: "MachineEvent",
                newName: "IX_MachineEvent_MachineId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MachineEvent",
                table: "MachineEvent",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MachineEvent_Machines_MachineId",
                table: "MachineEvent",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachineEvent_Machines_MachineId",
                table: "MachineEvent");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MachineEvent",
                table: "MachineEvent");

            migrationBuilder.RenameTable(
                name: "MachineEvent",
                newName: "ProductionEvents");

            migrationBuilder.RenameIndex(
                name: "IX_MachineEvent_MachineId",
                table: "ProductionEvents",
                newName: "IX_ProductionEvents_MachineId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductionEvents",
                table: "ProductionEvents",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionEvents_Machines_MachineId",
                table: "ProductionEvents",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
