using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Master
{
    /// <inheritdoc />
    public partial class strutturaTabelleRiviste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_BTCCountries_CountryId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_TenantCompany_TenantCompanyId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "UserFunctionTenantTuple");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantCompanyId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BTCCountries",
                table: "BTCCountries");

            migrationBuilder.DropColumn(
                name: "TenantCompanyId",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "BTCCountries",
                newName: "Countries");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Countries",
                table: "Countries",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "TenantCompanyUserRoleFunctionTuple",
                columns: table => new
                {
                    TenantCompaniesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserRoleFunctionTuplesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCompanyUserRoleFunctionTuple", x => new { x.TenantCompaniesId, x.UserRoleFunctionTuplesId });
                    table.ForeignKey(
                        name: "FK_TenantCompanyUserRoleFunctionTuple_TenantCompany_TenantCompaniesId",
                        column: x => x.TenantCompaniesId,
                        principalTable: "TenantCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantCompanyUserRoleFunctionTuple_UserRoleFunctionTuple_UserRoleFunctionTuplesId",
                        column: x => x.UserRoleFunctionTuplesId,
                        principalTable: "UserRoleFunctionTuple",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantCompanyUserRoleFunctionTuple_UserRoleFunctionTuplesId",
                table: "TenantCompanyUserRoleFunctionTuple",
                column: "UserRoleFunctionTuplesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Countries_CountryId",
                table: "Tenants",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Countries_CountryId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "TenantCompanyUserRoleFunctionTuple");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Countries",
                table: "Countries");

            migrationBuilder.RenameTable(
                name: "Countries",
                newName: "BTCCountries");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantCompanyId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_BTCCountries",
                table: "BTCCountries",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "UserFunctionTenantTuple",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserRoleFunctionTupleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFunctionTenantTuple", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFunctionTenantTuple_TenantCompany_TenantCompanyId",
                        column: x => x.TenantCompanyId,
                        principalTable: "TenantCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFunctionTenantTuple_UserRoleFunctionTuple_UserRoleFunctionTupleId",
                        column: x => x.UserRoleFunctionTupleId,
                        principalTable: "UserRoleFunctionTuple",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantCompanyId",
                table: "Users",
                column: "TenantCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFunctionTenantTuple_TenantCompanyId",
                table: "UserFunctionTenantTuple",
                column: "TenantCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFunctionTenantTuple_UserRoleFunctionTupleId",
                table: "UserFunctionTenantTuple",
                column: "UserRoleFunctionTupleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_BTCCountries_CountryId",
                table: "Tenants",
                column: "CountryId",
                principalTable: "BTCCountries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_TenantCompany_TenantCompanyId",
                table: "Users",
                column: "TenantCompanyId",
                principalTable: "TenantCompany",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }
    }
}
