using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Fiscal;
using GetStartedApp.ViewModels;
using Xunit;

namespace GetStartedApp.Tests;

public class EntradaMercadoriaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public EntradaMercadoriaTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Cadastra vendedor para turnos e vendas
        _db.Vendedores.Add(new Vendedor { Id = 1, Nome = "Operador de Estoque" });

        // Cadastra produtos com estoque inicial
        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Sacola Branca 2k", Preco = 2.50m, Estoque = 10 },
            new Produto { Id = 2, Nome = "Copo Descartável 200ml", Preco = 0.15m, Estoque = 50 }
        );
        _db.SaveChanges();

        _service = new PdvService(_db, NullLogger<PdvService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Deve_Registrar_Entrada_De_Mercadoria_E_Incrementar_Estoque()
    {
        // Arrange: 100 sacolas a R$ 1.20 cada
        var p1 = await _db.Produtos.FindAsync(1);
        int estoqueAntes = p1!.Estoque; // 10

        var itens = new List<(int ProdutoId, int Quantidade, decimal CustoUnitario)>
        {
            (1, 100, 1.20m)
        };

        // Act
        var entrada = await _service.RegistrarEntradaMercadoriaAsync(
            numeroNota: "NF-00123",
            fornecedor: "Distribuidora ABC",
            observacao: "Carga regular",
            itens: itens
        );

        // Assert
        Assert.NotNull(entrada);
        Assert.Equal("NF-00123", entrada.NumeroNota);
        Assert.Equal("Distribuidora ABC", entrada.Fornecedor);
        Assert.Equal(120.00m, entrada.ValorTotal); // 100 * 1.20

        // Verifica que o estoque do produto aumentou
        var p1Depois = await _db.Produtos.FindAsync(1);
        Assert.Equal(estoqueAntes + 100, p1Depois!.Estoque);
        Assert.Equal(1.20m, p1Depois.CustoUltimaCompra);
    }

    [Fact]
    public async Task Deve_Registrar_Entrada_Com_Multiplos_Itens_E_Atualizar_Custos()
    {
        // Arrange: Sacola e Copo
        var itens = new List<(int ProdutoId, int Quantidade, decimal CustoUnitario)>
        {
            (1, 50, 1.30m),  // 50 * 1.30 = 65.00
            (2, 200, 0.08m)  // 200 * 0.08 = 16.00
        };

        // Act
        var entrada = await _service.RegistrarEntradaMercadoriaAsync(
            numeroNota: "NF-00999",
            fornecedor: "Atacado Central",
            observacao: "Diversos descartáveis",
            itens: itens
        );

        // Assert
        Assert.Equal(81.00m, entrada.ValorTotal); // 65 + 16
        Assert.Equal(2, entrada.Itens.Count);

        var p1 = await _db.Produtos.FindAsync(1);
        var p2 = await _db.Produtos.FindAsync(2);

        Assert.Equal(60, p1!.Estoque);  // 10 + 50
        Assert.Equal(1.30m, p1.CustoUltimaCompra);

        Assert.Equal(250, p2!.Estoque); // 50 + 200
        Assert.Equal(0.08m, p2.CustoUltimaCompra);
    }

    [Fact]
    public async Task Nao_Deve_Alterar_Estoque_Se_Lista_De_Itens_For_Vazia()
    {
        // Arrange
        var p1 = await _db.Produtos.FindAsync(1);
        int estoqueOriginal = p1!.Estoque;

        // Act: Envia lista vazia
        await _service.RegistrarEntradaMercadoriaAsync(
            numeroNota: "NF-VAZIA",
            fornecedor: "Nenhum",
            observacao: "",
            itens: []
        );

        // Assert: Estoque inalterado e nenhuma entrada registrada
        var p1Apos = await _db.Produtos.FindAsync(1);
        Assert.Equal(estoqueOriginal, p1Apos!.Estoque);

        var historico = await _service.ObterHistoricoEntradasAsync();
        Assert.Empty(historico);
    }

    [Fact]
    public async Task Deve_Gerar_Levantamento_De_Inventario_Correto()
    {
        // Arrange: Realiza vendas com o turno aberto
        await _service.AbrirCaixaAsync(1, 100m);
        var p1 = await _db.Produtos.FindAsync(1); // Sacola
        var p2 = await _db.Produtos.FindAsync(2); // Copo

        await _service.SalvarPedidoAsync(1, [(p1!, 3)], "Dinheiro");
        await _service.SalvarPedidoAsync(1, [(p2!, 10)], "PIX");

        // Act: Gera relatório do dia de hoje
        var relatorio = await _service.GerarLevantamentoInventarioAsync(DateTime.Today);

        // Assert
        Assert.NotNull(relatorio);
        Assert.Equal(2, relatorio.Count);

        // Produto mais vendido (Copo com 10) deve estar no topo
        var itemCopo = relatorio.First(r => r.ProdutoId == 2);
        Assert.Equal(10, itemCopo.QuantidadeVendida);

        var itemSacola = relatorio.First(r => r.ProdutoId == 1);
        Assert.Equal(3, itemSacola.QuantidadeVendida);
    }

    [Fact]
    public void ItemNotaFiscalVm_DeveCalcular_PrecoSugerido_ComBaseEmMarkup()
    {
        // Arrange: Custo unitário faturado R$ 10,00, sem frete, markup padrão 50%
        var item = new ItemNotaFiscalVm
        {
            NumeroItem = 1,
            QuantidadeFaturada = 1,
            FatorConversao = 1,
            PrecoUnitarioFaturado = 10.00m,
            RateioDespesas = 0m,
            MarkupPercentual = 50.0m
        };

        // Assert: Custo unitário R$ 10,00 -> Preço Sugerido = 10 * (1 + 0.50) = R$ 15,00
        Assert.Equal(10.00m, item.CustoUnitarioEstoque);
        Assert.Equal(15.00m, item.PrecoVendaSugerido);
        Assert.Equal(15.00m, item.PrecoVendaFinal);

        // Act: Altera markup para 100%
        item.MarkupPercentual = 100.0m;

        // Assert: Preço sugerido sobe para R$ 20,00
        Assert.Equal(20.00m, item.PrecoVendaSugerido);
        Assert.Equal(20.00m, item.PrecoVendaFinal);
    }

    [Fact]
    public async Task EntradaNfeViewModel_DeveBloquearProcessamento_SeHouverPrecoVendaZero()
    {
        // Arrange
        var parser = new NfeXmlParserService(NullLogger<NfeXmlParserService>.Instance);
        var vm = new EntradaNfeViewModel(_service, parser, NullLogger<EntradaNfeViewModel>.Instance);
        await vm.CarregarCatalogoAsync();

        vm.ItensNota.Clear();
        var item = new ItemNotaFiscalVm
        {
            NumeroItem = 1,
            DescricaoFornecedor = "PRODUTO TESTE COM ERRO",
            QuantidadeFaturada = 1,
            PrecoUnitarioFaturado = 10m
        };
        // Usuário força o preço de venda para zero
        item.PrecoVendaFinal = 0m;
        vm.ItensNota.Add(item);

        // Act
        await vm.ProcessarEntradaFiscalCommand.ExecuteAsync(null);

        // Assert: Processamento barrado com aviso
        Assert.Contains("está com preço de venda zerado", vm.MensagemFeedback);
        Assert.NotEqual("LANÇADA NO ESTOQUE & INTEGRADA AO FINANCEIRO", vm.StatusDocumento);
    }

    [Fact]
    public async Task EntradaNfeViewModel_DeveAtualizarPrecoVendaECusto_NoCatalogoAoProcessar()
    {
        // Arrange: Produto existente no ERP com preço R$ 2,50
        var parser = new NfeXmlParserService(NullLogger<NfeXmlParserService>.Instance);
        var vm = new EntradaNfeViewModel(_service, parser, NullLogger<EntradaNfeViewModel>.Instance);
        await vm.CarregarCatalogoAsync();

        var produtoExistente = vm.ProdutosDisponiveis.First(p => p.Id == 1); // Sacola Branca 2k
        Assert.Equal(2.50m, produtoExistente.Preco);

        vm.ItensNota.Clear();
        var itemNota = new ItemNotaFiscalVm
        {
            NumeroItem = 1,
            DescricaoFornecedor = "SACOLA BRANCA 2K",
            QuantidadeFaturada = 20,
            FatorConversao = 1,
            PrecoUnitarioFaturado = 1.50m,
            ProdutoVinculado = produtoExistente,
            PrecoVendaFinal = 3.99m // Usuário definiu novo preço R$ 3,99
        };
        vm.ItensNota.Add(itemNota);

        // Act
        await vm.ProcessarEntradaFiscalCommand.ExecuteAsync(null);

        // Assert: Nota fiscal lançada com sucesso
        Assert.Equal("LANÇADA NO ESTOQUE & INTEGRADA AO FINANCEIRO", vm.StatusDocumento);

        // Recarrega do banco de dados para conferência
        var produtoAtualizado = await _db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1);
        Assert.NotNull(produtoAtualizado);
        Assert.Equal(3.99m, produtoAtualizado.Preco);
        Assert.Equal(1.50m, produtoAtualizado.CustoUltimaCompra);
        Assert.Equal(30, produtoAtualizado.Estoque); // 10 original + 20 faturadas
    }
}
