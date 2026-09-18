using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Models.Fiscal;
using GetStartedApp.Services.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class FechamentoFiscalTests : IDisposable
{
    private readonly string _dbName;
    private readonly string _tempOutputDir;
    private readonly AppDbContext _db;
    private readonly ConfiguracaoFiscalEmpresa _fiscalConfig;
    private readonly FechamentoFiscalService _service;
    private readonly int _vendedorId;

    public FechamentoFiscalTests()
    {
        _dbName = $"test_fechamento_{Guid.NewGuid():N}.db";
        _tempOutputDir = Path.Combine(Path.GetTempPath(), $"fechamento_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempOutputDir);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var vendedor = new Vendedor { Nome = "Operador Fiscal" };
        _db.Vendedores.Add(vendedor);
        _db.SaveChanges();
        _vendedorId = vendedor.Id;

        _fiscalConfig = new ConfiguracaoFiscalEmpresa
        {
            Cnpj = "12.345.678/0001-95",
            RazaoSocial = "Comercial Pro Testes Ltda",
            Ambiente = 2
        };

        _service = new FechamentoFiscalService(_db, _fiscalConfig, NullLogger<FechamentoFiscalService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
        if (Directory.Exists(_tempOutputDir)) Directory.Delete(_tempOutputDir, true);
    }

    [Fact]
    public async Task GerarPacoteMensalAsync_ComVendasAutorizadasECanceladas_DeveGerarZipComPastasECsv()
    {
        // Arrange
        var dataRef = new DateTime(2026, 9, 15, 14, 30, 0);

        // Venda 1: Autorizada
        var v1 = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = dataRef,
            ValorTotal = 150m,
            Status = "CONCLUIDA",
            ChaveAcessoNfce = "35260912345678000195650010000000011234567801",
            NumeroNfce = 1,
            SerieNfce = 1,
            XmlNfce = "<nfeProc versao=\"4.00\"><NFe><infNFe Id=\"NFe35260912345678000195650010000000011234567801\"><total><ICMSTot><vNF>150.00</vNF></ICMSTot></total></infNFe></NFe></nfeProc>"
        };

        // Venda 2: Cancelada
        var v2 = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = dataRef.AddHours(1),
            ValorTotal = 50m,
            Status = "CANCELADA",
            DataHoraCancelamento = dataRef.AddHours(1).AddMinutes(10),
            ChaveAcessoNfce = "35260912345678000195650010000000021234567802",
            NumeroNfce = 2,
            SerieNfce = 1,
            XmlCancelamento = "<procEventoNFe versao=\"1.00\"><evento><infEvento><chNFe>35260912345678000195650010000000021234567802</chNFe></infEvento></evento></procEventoNFe>"
        };

        _db.Vendas.AddRange(v1, v2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resumo = await _service.GerarPacoteMensalAsync(2026, 9, _tempOutputDir);

        // Assert
        Assert.NotNull(resumo);
        Assert.Equal(1, resumo.TotalAutorizadas);
        Assert.Equal(1, resumo.TotalCanceladas);
        Assert.Equal(150m, resumo.FaturamentoTotal);
        Assert.True(File.Exists(resumo.CaminhoArquivoZip));

        // Inspeciona estrutura interna do ZIP
        using var archive = ZipFile.OpenRead(resumo.CaminhoArquivoZip);
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        // Subpasta Autorizadas/
        Assert.Contains(entries, e => e.StartsWith("Autorizadas/") && e.EndsWith(".xml"));
        // Subpasta Canceladas/
        Assert.Contains(entries, e => e.StartsWith("Canceladas/") && e.EndsWith(".xml"));
        // Resumo CSV
        Assert.Contains(entries, e => e.Equals("Resumo_Fiscal_2026_09.csv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GerarPacoteMensalAsync_SemVendasNoPeriodo_DeveLancarInvalidOperationException()
    {
        // Act & Assert (Nenhuma venda inserida no banco para o mês 08/2026)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GerarPacoteMensalAsync(2026, 8, _tempOutputDir));

        Assert.Contains("Nenhuma nota fiscal encontrada", ex.Message);
    }

    [Fact]
    public async Task GerarPacoteMensalAsync_MesInvalido_DeveLancarArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.GerarPacoteMensalAsync(2026, 13, _tempOutputDir));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.GerarPacoteMensalAsync(2026, 0, _tempOutputDir));
    }

    [Fact]
    public async Task GerarPacoteMensalAsync_CsvResumo_DeveConterCabecalhoEEstruturaCorreta()
    {
        // Arrange
        var venda = new Venda
        {
            VendedorId = _vendedorId,
            DataHora = new DateTime(2026, 5, 10, 10, 0, 0),
            ValorTotal = 200m,
            Status = "CONCLUIDA",
            ChaveAcessoNfce = "35260512345678000195650010000000031234567803",
            NumeroNfce = 3,
            SerieNfce = 1
        };
        _db.Vendas.Add(venda);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resumo = await _service.GerarPacoteMensalAsync(2026, 5, _tempOutputDir);

        // Assert
        using var archive = ZipFile.OpenRead(resumo.CaminhoArquivoZip);
        var csvEntry = archive.GetEntry("Resumo_Fiscal_2026_05.csv");
        Assert.NotNull(csvEntry);

        using var reader = new StreamReader(csvEntry.Open());
        var conteudoCsv = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Numero;Serie;DataEmissao;ChaveAcesso;ValorTotal;Status;ValorTributosAprox", conteudoCsv);
        Assert.Contains("3;1;", conteudoCsv);
        Assert.Contains("200.00", conteudoCsv);
        Assert.Contains("Autorizada", conteudoCsv);
    }
}
