using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class Rev001CorrecoesTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;

    public Rev001CorrecoesTests()
    {
        _dbName = $"test_rev001_{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
    }

    [Fact]
    public async Task ACT03_Dashboard_DeveCalcularPatrimonioEstoque_UsandoCustoUltimaCompra()
    {
        // Arrange
        var p1 = new Produto { Nome = "Produto A", Preco = 100m, CustoUltimaCompra = 40m, Estoque = 10 }; // Custo: 40 * 10 = 400
        var p2 = new Produto { Nome = "Produto B", Preco = 50m, CustoUltimaCompra = 0m, Estoque = 4 };    // Fallback 60%: 30 * 4 = 120
        _db.Produtos.AddRange(p1, p2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new DashboardViewModel(pdvService, _db);

        // Act
        await vm.CarregarMetricasAsync();

        // Assert
        Assert.Equal(14, vm.TotalProdutosEstoque);
        Assert.Equal(520m, vm.ValorEstoque); // 400 + 120 = 520
    }

    [Fact]
    public void ACT03_ItemCurvaAbcDto_CoresBadges_DevemRetornarCoresApropriadasPorClasse()
    {
        // Arrange
        var itemA = new ItemCurvaAbcDto { ClasseAbc = "A" };
        var itemB = new ItemCurvaAbcDto { ClasseAbc = "B" };
        var itemC = new ItemCurvaAbcDto { ClasseAbc = "C" };

        // Act & Assert
        Assert.Equal("#E8F8F5", itemA.CorFundoBadge);
        Assert.Equal("#27AE60", itemA.CorTextoBadge);

        Assert.Equal("#FEF9E7", itemB.CorFundoBadge);
        Assert.Equal("#D35400", itemB.CorTextoBadge);

        Assert.Equal("#EBEDEF", itemC.CorFundoBadge);
        Assert.Equal("#7F8C8D", itemC.CorTextoBadge);
    }

    [Fact]
    public void ACT01_PdvViewModel_AoAdicionarAoCarrinho_PreservaDadosParaEmissaoFiscal()
    {
        // Arrange
        var prod = new Produto 
        { 
            Id = 42, 
            Nome = "Refrigerante 2L", 
            Preco = 10m, 
            Estoque = 100, 
            CodigoBarras = "7891234567890", 
            Ncm = "22021000", 
            UnidadeMedida = "UN" 
        };

        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new PdvViewModel(pdvService, NullLogger<PdvViewModel>.Instance);

        // Act
        vm.AdicionarAoCarrinho(prod, 2);

        // Assert
        Assert.Single(vm.Carrinho);
        Assert.Equal(2, vm.Carrinho[0].Quantidade);
        Assert.Equal("22021000", vm.Carrinho[0].Produto.Ncm);
        Assert.Equal("UN", vm.Carrinho[0].Produto.UnidadeMedida);
    }

    [Fact]
    public void ACT05_PdvViewModel_SplitPayment_DeveAdicionarRemoverERatearValores()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new PdvViewModel(pdvService, NullLogger<PdvViewModel>.Instance);
        vm.IsCaixaAberto = true; // Simula turno em aberto

        var prod = new Produto { Id = 1, Nome = "Fone Bluetooth", Preco = 100m, Estoque = 10 };
        vm.AdicionarAoCarrinho(prod, 1);
        Assert.Equal(100m, vm.TotalVenda);

        // Act: Abre checkout
        vm.AbrirModalPagamentoCommand.Execute(null);
        Assert.True(vm.IsModalAberto);
        Assert.Equal(100m, vm.ValorRecebido);
        Assert.Equal(100m, vm.SaldoRestante);
        Assert.True(vm.PodeConfirmarPagamento); // ValorRecebido padrão cobre o total se for pagamento único

        // Adiciona R$ 40 em Dinheiro para iniciar Split Payment
        vm.FormaPagamentoSelecionada = "Dinheiro";
        vm.ValorRecebido = 40m;
        vm.AdicionarParcelaPagamentoCommand.Execute(null);

        // Assert parcial
        Assert.Single(vm.PagamentosAdicionados);
        Assert.Equal(60m, vm.SaldoRestante);
        Assert.Equal(40m, vm.TotalPago);
        Assert.False(vm.PodeConfirmarPagamento); // Falta R$ 60
        Assert.Equal(60m, vm.ValorRecebido);     // Sugere o restante automaticamente

        // Adiciona restante R$ 60 em PIX
        vm.FormaPagamentoSelecionada = "PIX";
        vm.AdicionarParcelaPagamentoCommand.Execute(null);

        // Assert completo
        Assert.Equal(2, vm.PagamentosAdicionados.Count);
        Assert.Equal(0m, vm.SaldoRestante);
        Assert.Equal(100m, vm.TotalPago);
        Assert.Equal(0m, vm.Troco);
        Assert.True(vm.PodeConfirmarPagamento);
    }

    [Fact]
    public async Task ACT05_PdvService_FinalizarVenda_ComPagamentosMultiplos_DeveRatearDinheiroEOutrosNoCaixa()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vendedor = new Vendedor { Nome = "Caixa 01" };
        _db.Vendedores.Add(vendedor);
        var produto = new Produto { Nome = "Teclado Mecânico", Preco = 200m, Estoque = 10 };
        _db.Produtos.Add(produto);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await pdvService.AbrirCaixaAsync(vendedor.Id, 100m); // 100 de suprimento inicial

        var pagamentos = new List<(string Forma, decimal Valor)>
        {
            ("Dinheiro", 50m),
            ("Cartão de Crédito", 150m)
        };

        // Act
        await pdvService.SalvarPedidoAsync(
            vendedorId: vendedor.Id,
            carrinho: [(produto, 1)],
            formaPagamento: "Multiplas (Dinheiro, Cartão de Crédito)",
            pagamentosDetalhados: pagamentos
        );

        // Assert
        var caixaAtual = await pdvService.ObterTurnoAtualAsync();
        Assert.NotNull(caixaAtual);
        Assert.Equal(50m, caixaAtual.TotalVendasDinheiro);
        Assert.Equal(150m, caixaAtual.TotalVendasOutros);
        Assert.Equal(150m, caixaAtual.SaldoEsperadoEmDinheiro); // 100 suprimento + 50 dinheiro
    }
}
