using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class MetricsTypeReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionDataMetrics");

            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "MetricTypes");

            migrationBuilder.DropColumn(
                name: "LastModifiedDate",
                table: "MetricTypes");

            migrationBuilder.DropColumn(
                name: "LogMessage",
                table: "MetricTypes");

            migrationBuilder.DropColumn(
                name: "UserCreate",
                table: "MetricTypes");

            migrationBuilder.DropColumn(
                name: "UserModify",
                table: "MetricTypes");

            migrationBuilder.DropColumn(
                name: "ValidityDate",
                table: "MetricTypes");

            migrationBuilder.AddColumn<Guid>(
                name: "MetricTypeId",
                table: "ProductionData",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "ProductionData",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionData_MetricTypeId",
                table: "ProductionData",
                column: "MetricTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionData_MetricTypes_MetricTypeId",
                table: "ProductionData",
                column: "MetricTypeId",
                principalTable: "MetricTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionData_MetricTypes_MetricTypeId",
                table: "ProductionData");

            migrationBuilder.DropIndex(
                name: "IX_ProductionData_MetricTypeId",
                table: "ProductionData");

            migrationBuilder.DropColumn(
                name: "MetricTypeId",
                table: "ProductionData");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ProductionData");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "MetricTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedDate",
                table: "MetricTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "LogMessage",
                table: "MetricTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserCreate",
                table: "MetricTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserModify",
                table: "MetricTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidityDate",
                table: "MetricTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductionDataMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MetricTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserCreate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserModify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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
    }
}
