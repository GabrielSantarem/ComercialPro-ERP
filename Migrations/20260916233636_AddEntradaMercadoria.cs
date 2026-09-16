using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddEntradaMercadoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntradasMercadoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroNota = table.Column<string>(type: "TEXT", nullable: false),
                    Fornecedor = table.Column<string>(type: "TEXT", nullable: false),
                    DataEntrada = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasMercadoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensEntradaMercadoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntradaMercadoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProdutoId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantidadeEntrada = table.Column<int>(type: "INTEGER", nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensEntradaMercadoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensEntradaMercadoria_EntradasMercadoria_EntradaMercadoriaId",
                        column: x => x.EntradaMercadoriaId,
                        principalTable: "EntradasMercadoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItensEntradaMercadoria_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensEntradaMercadoria_EntradaMercadoriaId",
                table: "ItensEntradaMercadoria",
                column: "EntradaMercadoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensEntradaMercadoria_ProdutoId",
                table: "ItensEntradaMercadoria",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensEntradaMercadoria");

            migrationBuilder.DropTable(
                name: "EntradasMercadoria");
        }
    }
}
