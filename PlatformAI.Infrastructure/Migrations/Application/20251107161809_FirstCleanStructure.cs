using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class FirstCleanStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machines_Departments_DepartmentId",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "Benefit",
                table: "ProductionEvents");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "ProductionEvents");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ProductionEvents");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "ProductionEvents",
                newName: "EventTime");

            migrationBuilder.RenameColumn(
                name: "OperatorName",
                table: "ProductionEvents",
                newName: "EventType");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "Machines",
                newName: "ProductionLineId");

            migrationBuilder.RenameIndex(
                name: "IX_Machines_DepartmentId",
                table: "Machines",
                newName: "IX_Machines_ProductionLineId");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "ProductionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductionLineId1",
                table: "Machines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Machines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Machines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HourlyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UserCreate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserModify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlannedQuantity = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserCreate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserModify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_ProductionLines_ProductionLineId",
                        column: x => x.ProductionLineId,
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuantityProduced = table.Column<int>(type: "int", nullable: false),
                    ScrapQuantity = table.Column<int>(type: "int", nullable: false),
                    CycleTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EnergyConsumption = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UserCreate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserModify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionData_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionData_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Machines_ProductionLineId1",
                table: "Machines",
                column: "ProductionLineId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionData_MachineId",
                table: "ProductionData",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionData_ProductionOrderId",
                table: "ProductionData",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ProductionLineId",
                table: "ProductionOrders",
                column: "ProductionLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_ProductionLines_ProductionLineId",
                table: "Machines",
                column: "ProductionLineId",
                principalTable: "ProductionLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_ProductionLines_ProductionLineId1",
                table: "Machines",
                column: "ProductionLineId1",
                principalTable: "ProductionLines",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machines_ProductionLines_ProductionLineId",
                table: "Machines");

            migrationBuilder.DropForeignKey(
                name: "FK_Machines_ProductionLines_ProductionLineId1",
                table: "Machines");

            migrationBuilder.DropTable(
                name: "CostCenters");

            migrationBuilder.DropTable(
                name: "ProductionData");

            migrationBuilder.DropTable(
                name: "ProductionOrders");

            migrationBuilder.DropTable(
                name: "ProductionLines");

            migrationBuilder.DropIndex(
                name: "IX_Machines_ProductionLineId1",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "ProductionEvents");

            migrationBuilder.DropColumn(
                name: "ProductionLineId1",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Machines");

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "ProductionEvents",
                newName: "OperatorName");

            migrationBuilder.RenameColumn(
                name: "EventTime",
                table: "ProductionEvents",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "ProductionLineId",
                table: "Machines",
                newName: "DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Machines_ProductionLineId",
                table: "Machines",
                newName: "IX_Machines_DepartmentId");

            migrationBuilder.AddColumn<double>(
                name: "Benefit",
                table: "ProductionEvents",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Cost",
                table: "ProductionEvents",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DurationMinutes",
                table: "ProductionEvents",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_Departments_DepartmentId",
                table: "Machines",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
