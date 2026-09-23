using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.ViewModels;
using GetStartedApp.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GetStartedApp.Tests;

public class AvaloniaHeadlessTests : IDisposable
{
    private readonly string _dbName;
    private readonly AppDbContext _db;

    public AvaloniaHeadlessTests()
    {
        _dbName = $"test_headless_{Guid.NewGuid():N}.db";
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

    [AvaloniaFact]
    public void Headless_TextBox_DeveReceberTexto_ViaKeyTextInput()
    {
        // Arrange
        var textBox = new TextBox();
        var window = new Window { Content = textBox };
        window.Show();

        // Act
        textBox.Focus();
        window.KeyTextInput("7891234567890");

        // Assert
        Assert.Equal("7891234567890", textBox.Text);
    }

    [AvaloniaFact]
    public void Headless_PdvView_DeveInstanciarEConectarControlesSemErro()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new PdvViewModel(pdvService, NullLogger<PdvViewModel>.Instance);

        var view = new PdvView { DataContext = vm };
        var window = new Window { Content = view };

        // Act
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.NotNull(view);
        Assert.Same(vm, view.DataContext);

        var txtPesquisa = view.FindControl<TextBox>("TxtPesquisa");
        Assert.NotNull(txtPesquisa);
    }

    [AvaloniaFact]
    public void Headless_PdvView_AtalhoF1_DeveAbrirModalCaixa_QuandoCaixaFechado()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new PdvViewModel(pdvService, NullLogger<PdvViewModel>.Instance)
        {
            IsCaixaAberto = false
        };

        var view = new PdvView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var txtPesquisa = view.FindControl<TextBox>("TxtPesquisa");
        txtPesquisa?.Focus();

        // Act: Simula pressionar F1
        window.KeyPress(Key.F1, RawInputModifiers.None, PhysicalKey.F1, null);
        Dispatcher.UIThread.RunJobs();

        // Assert: Modal de abertura de caixa deve estar aberto
        Assert.True(vm.ModalCaixaAberto);
        Assert.Equal("ABERTURA", vm.TipoModalCaixa);

        // Act: Simula ESC para fechar
        view.Focus();
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(vm.ModalCaixaAberto);
    }

    [AvaloniaFact]
    public void Headless_PdvView_AtalhoF12_DeveAbrirEFecharModalPagamento()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new PdvViewModel(pdvService, NullLogger<PdvViewModel>.Instance)
        {
            IsCaixaAberto = true
        };
        var produto = new Produto { Id = 1, Nome = "Produto Teste Headless", Preco = 25.00m, Estoque = 10 };
        vm.AdicionarAoCarrinho(produto, 2);

        var view = new PdvView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var txtPesquisa = view.FindControl<TextBox>("TxtPesquisa");
        txtPesquisa?.Focus();

        // Act: Pressiona F12 para abrir pagamento
        window.KeyPress(Key.F12, RawInputModifiers.None, PhysicalKey.F12, null);
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.True(vm.IsModalAberto);
        Assert.Equal(50.00m, vm.TotalVenda);

        // Act: Pressiona ESC para cancelar e fechar
        view.Focus();
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.False(vm.IsModalAberto);
    }

    [AvaloniaFact]
    public void Headless_PdvView_ModalNfceEmitida_DeveFecharComEnterOuEsc()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new PdvViewModel(pdvService, NullLogger<PdvViewModel>.Instance)
        {
            ModalNfceEmitidaAberto = true
        };

        var view = new PdvView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.Focus();

        // Act: Pressiona ENTER no comprovante
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        // Assert: Modal deve ter sido fechado (ACT-04)
        Assert.False(vm.ModalNfceEmitidaAberto);
    }

    [AvaloniaFact]
    public void Headless_BalcaoView_DeveRenderizarEInicializarControles()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new BalcaoViewModel(pdvService, NullLogger<BalcaoViewModel>.Instance);

        var view = new BalcaoView { DataContext = vm };
        var window = new Window { Content = view };

        // Act
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.NotNull(view);
        Assert.Same(vm, view.DataContext);
        var txtPesquisa = view.FindControl<TextBox>("TxtPesquisa");
        Assert.NotNull(txtPesquisa);
    }

    [AvaloniaFact]
    public async Task Headless_ConfiguracoesView_DeveCarregarHardwareEPersistir()
    {
        // Arrange
        var pdvService = new PdvService(_db, NullLogger<PdvService>.Instance);
        var vm = new ConfiguracoesViewModel(pdvService);

        var view = new ConfiguracoesView { DataContext = vm };
        var window = new Window { Content = view };

        // Act
        window.Show();
        await vm.CarregarDadosIniciaisAsync();
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.NotNull(view);
        Assert.Equal("Terminal Caixa 01", vm.NomeEstacao);

        // Modifica e salva via ViewModel vinculado à View
        vm.NomeEstacao = "Caixa Expresso Headless";
        await vm.SalvarConfiguracoesHardwareAsync();

        var salvo = await _db.ConfiguracoesTerminal.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(salvo);
        Assert.Equal("Caixa Expresso Headless", salvo.NomeEstacao);
    }
}
