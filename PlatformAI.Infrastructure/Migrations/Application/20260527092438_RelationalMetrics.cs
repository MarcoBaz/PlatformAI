using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class RelationalMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CycleTime",
                table: "ProductionData");

            migrationBuilder.DropColumn(
                name: "EnergyConsumption",
                table: "ProductionData");

            migrationBuilder.DropColumn(
                name: "QuantityProduced",
                table: "ProductionData");

            migrationBuilder.DropColumn(
                name: "ScrapQuantity",
                table: "ProductionData");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "ProductionData");

            migrationBuilder.CreateTable(
                name: "MetricTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserCreate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserModify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionDataMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ProductionDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UserCreate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserModify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionDataMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionDataMetrics_MetricTypes_MetricTypeId",
                        column: x => x.MetricTypeId,
                        principalTable: "MetricTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionDataMetrics_ProductionData_ProductionDataId",
                        column: x => x.ProductionDataId,
                        principalTable: "ProductionData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionDataMetrics_MetricTypeId",
                table: "ProductionDataMetrics",
                column: "MetricTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionDataMetrics_ProductionDataId",
                table: "ProductionDataMetrics",
                column: "ProductionDataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionDataMetrics");

            migrationBuilder.DropTable(
                name: "MetricTypes");

            migrationBuilder.AddColumn<decimal>(
                name: "CycleTime",
                table: "ProductionData",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyConsumption",
                table: "ProductionData",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "QuantityProduced",
                table: "ProductionData",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScrapQuantity",
                table: "ProductionData",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Temperature",
                table: "ProductionData",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
