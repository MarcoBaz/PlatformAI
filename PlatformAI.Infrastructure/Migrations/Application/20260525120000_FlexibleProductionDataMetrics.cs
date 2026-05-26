using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class FlexibleProductionDataMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rimuove i campi fissi di produzione
            migrationBuilder.DropColumn(name: "QuantityProduced",  table: "ProductionData");
            migrationBuilder.DropColumn(name: "ScrapQuantity",     table: "ProductionData");
            migrationBuilder.DropColumn(name: "CycleTime",         table: "ProductionData");
            migrationBuilder.DropColumn(name: "EnergyConsumption", table: "ProductionData");
            migrationBuilder.DropColumn(name: "Temperature",       table: "ProductionData");

            // Aggiunge la colonna JSON flessibile per le metriche
            migrationBuilder.AddColumn<string>(
                name: "Metrics",
                table: "ProductionData",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Metrics", table: "ProductionData");

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

            migrationBuilder.AddColumn<decimal>(
                name: "Temperature",
                table: "ProductionData",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
