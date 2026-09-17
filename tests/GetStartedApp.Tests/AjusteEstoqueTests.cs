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
using Xunit;

namespace GetStartedApp.Tests;

public class AjusteEstoqueTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public AjusteEstoqueTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Sacola Branca 2k", Preco = 10.00m, Estoque = 20, EstoqueMinimo = 5 },
            new Produto { Id = 2, Nome = "Copo Térmico 200ml", Preco = 25.00m, Estoque = 3, EstoqueMinimo = 5 },
            new Produto { Id = 3, Nome = "Fita Gomada 50mm", Preco = 15.00m, Estoque = 5, EstoqueMinimo = 5 }
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
    public async Task Deve_Registrar_Baixa_Por_Avaria_Com_Sucesso_E_Gravar_Auditoria()
    {
        // Act: Baixa de 4 unidades por avaria
        var ajuste = await _service.RegistrarAjusteEstoqueAsync(
            produtoId: 1,
            quantidadeDiferenca: -4,
            tipoAjuste: "AVARIA",
            motivo: "Fardo rasgado durante empilhamento",
            responsavel: "Estoquista João");

        // Assert
        Assert.NotNull(ajuste);
        Assert.Equal(20, ajuste.EstoqueAnterior);
        Assert.Equal(16, ajuste.EstoqueNovo);
        Assert.Equal(-4, ajuste.QuantidadeDiferenca);
        Assert.Equal("AVARIA", ajuste.TipoAjuste);
        Assert.Equal("Estoquista João", ajuste.Responsavel);

        // Verifica reflexo no produto
        var prodDb = await _db.Produtos.FindAsync(1);
        Assert.Equal(16, prodDb!.Estoque);

        // Verifica consulta de histórico
        var historico = await _service.ObterHistoricoAjustesAsync(1);
        Assert.Single(historico);
        Assert.Equal("Fardo rasgado durante empilhamento", historico[0].Motivo);
    }

    [Fact]
    public async Task Deve_Registrar_Ajuste_De_Balanco_Fisico_Para_Mais()
    {
        // Act: Entrada de +7 unidades encontradas em contagem
        var ajuste = await _service.RegistrarAjusteEstoqueAsync(
            produtoId: 1,
            quantidadeDiferenca: 7,
            tipoAjuste: "BALANCO_FISICO",
            motivo: "Contagem de inventário físico mensal",
            responsavel: "Gerente Marcos");

        Assert.Equal(20, ajuste.EstoqueAnterior);
        Assert.Equal(27, ajuste.EstoqueNovo);

        var prodDb = await _db.Produtos.FindAsync(1);
        Assert.Equal(27, prodDb!.Estoque);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Ajuste_Que_Deixe_Estoque_Negativo()
    {
        // Arrange: Produto #2 tem estoque = 3
        // Act & Assert: tentar subtrair 4 deve falhar
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegistrarAjusteEstoqueAsync(2, -4, "AVARIA", "Caixa molhada")
        );

        Assert.Contains("não pode ficar negativo", ex.Message);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Ajuste_Com_Quantidade_Zero()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarAjusteEstoqueAsync(1, 0, "AVARIA", "Sem alteração")
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Nao_Deve_Permitir_Ajuste_Com_Motivo_Vazio_Ou_Nulo(string? motivoInvalido)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarAjusteEstoqueAsync(1, -1, "AVARIA", motivoInvalido!)
        );
    }

    [Fact]
    public async Task Deve_Filtrar_Produtos_Com_Estoque_Critico_Abaixo_Ou_Igual_Ao_Minimo()
    {
        // Arrange:
        // Produto 1: Estoque 20, Min 5 (OK)
        // Produto 2: Estoque 3, Min 5 (CRÍTICO)
        // Produto 3: Estoque 5, Min 5 (CRÍTICO - no limite)

        // Act
        var criticos = await _service.ObterProdutosComEstoqueCriticoAsync();

        // Assert: Apenas produtos 2 e 3 devem retornar
        Assert.Equal(2, criticos.Count);
        Assert.Contains(criticos, p => p.Id == 2);
        Assert.Contains(criticos, p => p.Id == 3);
        Assert.DoesNotContain(criticos, p => p.Id == 1);
    }

    [Fact]
    public async Task Deve_Atualizar_Estoque_Minimo_Do_Produto_Com_Sucesso()
    {
        await _service.AtualizarEstoqueMinimoProdutoAsync(1, 15);

        var prod = await _db.Produtos.FindAsync(1);
        Assert.Equal(15, prod!.EstoqueMinimo);
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Estoque_Minimo_Negativo()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AtualizarEstoqueMinimoProdutoAsync(1, -1)
        );
    }
}
