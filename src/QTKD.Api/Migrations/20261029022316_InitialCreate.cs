using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QTKD.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceGroup = table.Column<string>(type: "text", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceGroup = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceItems",
                columns: table => new
                {
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceItems", x => new { x.PriceListId, x.ServiceCode });
                    table.ForeignKey(
                        name: "FK_PriceItems_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "Name", "Status" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Công ty Minh Phát", "Active" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Công ty Hải Long", "Active" }
                });

            migrationBuilder.InsertData(
                table: "PriceLists",
                columns: new[] { "Id", "CustomerId", "EffectiveFrom", "EffectiveTo", "Name", "ServiceGroup", "Status" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("11111111-1111-1111-1111-111111111111"), new DateOnly(2026, 10, 1), new DateOnly(2027, 9, 30), "Bảng giá dịch vụ cảng 2026", "Dịch vụ cảng", "Draft" });

            migrationBuilder.InsertData(
                table: "Contracts",
                columns: new[] { "Id", "Content", "ContractNo", "CustomerId", "EndDate", "ServiceGroup", "StartDate", "Status" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), null, "HD-2026-018", new Guid("11111111-1111-1111-1111-111111111111"), new DateOnly(2027, 9, 30), "Dịch vụ cảng", new DateOnly(2026, 10, 1), "Draft" });

            migrationBuilder.InsertData(
                table: "PriceItems",
                columns: new[] { "PriceListId", "ServiceCode", "UnitPrice" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444444"), "CANG-001", 150000m },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "CANG-002", 220000m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNo",
                table: "Contracts",
                column: "ContractNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CustomerId",
                table: "Contracts",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "PriceItems");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "PriceLists");
        }
    }
}
