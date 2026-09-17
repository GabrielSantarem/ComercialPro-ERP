using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoBalcaoPreVenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PedidosBalcao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroComanda = table.Column<string>(type: "TEXT", nullable: false),
                    VendedorId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteNome = table.Column<string>(type: "TEXT", nullable: false),
                    ClienteCpf = table.Column<string>(type: "TEXT", nullable: false),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    VendaId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosBalcao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosBalcao_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PedidosBalcao_Vendedores_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Vendedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedidoBalcao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoBalcaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProdutoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantidade = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedidoBalcao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPedidoBalcao_PedidosBalcao_PedidoBalcaoId",
                        column: x => x.PedidoBalcaoId,
                        principalTable: "PedidosBalcao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItensPedidoBalcao_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedidoBalcao_PedidoBalcaoId",
                table: "ItensPedidoBalcao",
                column: "PedidoBalcaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedidoBalcao_ProdutoId",
                table: "ItensPedidoBalcao",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosBalcao_VendaId",
                table: "PedidosBalcao",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosBalcao_VendedorId",
                table: "PedidosBalcao",
                column: "VendedorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensPedidoBalcao");

            migrationBuilder.DropTable(
                name: "PedidosBalcao");
        }
    }
}
