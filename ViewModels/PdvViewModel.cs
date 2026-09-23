using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Models.Fiscal;
using GetStartedApp.Services;
using GetStartedApp.Services.Clientes;
using GetStartedApp.Services.Comercial;
using GetStartedApp.Services.Fiscal;
using GetStartedApp.Services.Hardware;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class PdvViewModel : ViewModelBase
{
    private readonly PdvService _pdvService;
    private readonly ILogger<PdvViewModel> _logger;
    private readonly NfceEmissaoService? _nfceService;
    private readonly ConfiguracaoFiscalEmpresa? _fiscalConfig;
    private readonly DanfeA4PdfService? _danfePdfService;
    private readonly INfceCancelamentoService? _cancelamentoService;
    private readonly ITrocaDevolucaoService? _trocaService;
    private readonly IClienteService? _clienteService;
    private readonly IBalancaEtiquetaParserService _balancaParserService;
    private readonly IBalancaCheckoutService _balancaCheckoutService;
    private readonly IGavetaDinheiroService _gavetaService;

    public ObservableCollection<ProdutoItem> Carrinho { get; } = [];
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = [];
    public ObservableCollection<Vendedor> Vendedores { get; } = [];

    [ObservableProperty]
    private decimal _totalVenda;

    [ObservableProperty]
    private ProdutoItem? _itemSelecionado;

    [ObservableProperty]
    private Produto? _produtoPesquisaSelecionado;

    [ObservableProperty]
    private Vendedor? _vendedorSelecionado;

    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    [ObservableProperty]
    private string _mensagemStatusBalanca = string.Empty;

    public ConfiguracaoTerminal? ConfigTerminal { get; private set; }

    public bool IsBloqueadoPorModal => IsModalAberto || ModalCaixaAberto || ModalFilaBalcaoAberto || ModalNfceEmitidaAberto || ModalCancelamentoAberto || ModalTrocasAberto;

    public PdvViewModel(
        PdvService pdvService, 
        ILogger<PdvViewModel> logger,
        NfceEmissaoService? nfceService = null,
        ConfiguracaoFiscalEmpresa? fiscalConfig = null,
        DanfeA4PdfService? danfePdfService = null,
        INfceCancelamentoService? cancelamentoService = null,
        ITrocaDevolucaoService? trocaService = null,
        IClienteService? clienteService = null,
        IBalancaEtiquetaParserService? balancaParserService = null,
        IBalancaCheckoutService? balancaCheckoutService = null,
        IGavetaDinheiroService? gavetaService = null)
    {
        _pdvService = pdvService;
        _logger = logger;
        _nfceService = nfceService;
        _fiscalConfig = fiscalConfig;
        _danfePdfService = danfePdfService;
        _cancelamentoService = cancelamentoService;
        _trocaService = trocaService;
        _clienteService = clienteService;
        _balancaParserService = balancaParserService ?? new BalancaEtiquetaParserService();
        _balancaCheckoutService = balancaCheckoutService ?? new BalancaMockService();
        _gavetaService = gavetaService ?? new GavetaDinheiroService();
    }

    public async Task InicializarAsync()
    {
        _logger.LogInformation("Inicializando PdvViewModel e carregando configurações...");
        ConfigTerminal = await _pdvService.ObterConfiguracaoTerminalAsync();

        Vendedores.Clear();
        var vendedoresDb = await _pdvService.ObterVendedoresAsync();
        foreach (var v in vendedoresDb) Vendedores.Add(v);

        VendedorSelecionado = Vendedores.FirstOrDefault();
        await AtualizarEstadoTurnoAsync();
        await AtualizarFilaPedidosAsync();
    }

    private void AtualizarTotal()
    {
        TotalVenda = Carrinho.Sum(x => x.Total);
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(TotalComTaxa));
        OnPropertyChanged(nameof(PodeConfirmarPagamento));
        ValorRecebido = TotalComTaxa;
    }

    async partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            ProdutoPesquisaSelecionado = null;
            return;
        }

        var termo = ExtrairTermoBusca(value);
        var filtrados = await _pdvService.PesquisarProdutosAsync(termo);
        foreach (var f in filtrados)
        {
            ResultadosPesquisa.Add(f);
        }

        ProdutoPesquisaSelecionado = ResultadosPesquisa.FirstOrDefault();
    }

    public static (int Quantidade, string Termo) ProcessarMultiplicador(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (1, string.Empty);

        var match = Regex.Match(input.Trim(), @"^(\d+)\s*\*\s*(.*)$");
        if (match.Success)
        {
            var termo = match.Groups[2].Value.Trim();
            if (int.TryParse(match.Groups[1].Value, out int qtd))
            {
                if (qtd <= 0)
                {
                    return (1, termo);
                }
                if (qtd > 99999) qtd = 99999;
                return (qtd, termo);
            }
            else
            {
                return (99999, termo);
            }
        }

        return (1, input.Trim());
    }

    public static string ExtrairTermoBusca(string input)
    {
        var (_, termo) = ProcessarMultiplicador(input);
        return termo;
    }

    [RelayCommand]
    public async Task LancarProdutoAsync()
    {
        if (string.IsNullOrWhiteSpace(TextoPesquisa)) return;

        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ O Caixa está FECHADO! Abra o caixa antes de iniciar vendas.";
            return;
        }

        var (quantidade, termo) = ProcessarMultiplicador(TextoPesquisa);

        // REV-004: Detecção e decodificação automática de código de balança de retaguarda (Prefixo 2)
        if (termo.Length is 12 or 13 && termo.StartsWith('2') && termo.All(char.IsDigit))
        {
            if (ConfigTerminal == null)
            {
                ConfigTerminal = await _pdvService.ObterConfiguracaoTerminalAsync();
            }

            var tamanhoCodigo = ConfigTerminal?.TamanhoCodigoBalanca ?? 4;
            var modoBalanca = (ConfigTerminal?.ModoBalancaEtiqueta?.Equals("PesoLiquido", StringComparison.OrdinalIgnoreCase) ?? false)
                ? ModoCodigoBalanca.PesoLiquido
                : ModoCodigoBalanca.ValorTotal;

            // Busca produto por ID ou código de barras
            var codExtraido = termo.Substring(1, Math.Min(tamanhoCodigo, termo.Length - 1));
            var todosProdutos = await _pdvService.ObterTodosProdutosAsync();
            var produtoBalanca = todosProdutos.FirstOrDefault(p => 
                p.Id.ToString() == codExtraido || 
                p.Id.ToString() == codExtraido.TrimStart('0') ||
                p.CodigoBarras == termo ||
                p.CodigoBarras == codExtraido);

            if (produtoBalanca != null)
            {
                var resultado = _balancaParserService.DecodificarCodigo(
                    termo, 
                    produtoBalanca.Preco, 
                    modoBalanca, 
                    tamanhoCodigo);

                if (resultado.IsCodigoBalanca && string.IsNullOrEmpty(resultado.MensagemErro))
                {
                    _logger.LogInformation("Produto de balança lançado via leitor: '{Nome}', Qtd/Peso: {Qtd}, Total: R$ {Total:N2}",
                        produtoBalanca.Nome, resultado.QuantidadeOuPeso, resultado.ValorTotalCalculado);

                    AdicionarAoCarrinho(produtoBalanca, resultado.QuantidadeOuPeso, resultado.ValorTotalCalculado);
                    TextoPesquisa = string.Empty;
                    ResultadosPesquisa.Clear();
                    ProdutoPesquisaSelecionado = null;
                    return;
                }
                else if (!string.IsNullOrEmpty(resultado.MensagemErro))
                {
                    _logger.LogWarning("Erro ao decodificar etiqueta de balança: {Msg}", resultado.MensagemErro);
                    MensagemCaixaErro = $"⚠️ Erro na etiqueta da balança: {resultado.MensagemErro}";
                    return;
                }
            }
        }

        // Fluxo padrão caso não seja código de balança
        var produtoAlvo = ProdutoPesquisaSelecionado ?? ResultadosPesquisa.FirstOrDefault();

        if (produtoAlvo != null)
        {
            _logger.LogInformation("Lançando produto no cupom: '{Nome}' x {Qtd}", produtoAlvo.Nome, quantidade);
            AdicionarAoCarrinho(produtoAlvo, quantidade);
            TextoPesquisa = string.Empty;
            ResultadosPesquisa.Clear();
            ProdutoPesquisaSelecionado = null;
        }
        else
        {
            _logger.LogWarning("Nenhum produto encontrado para o termo '{Termo}' com quantidade {Qtd}", termo, quantidade);
        }
    }

    public void LancarProduto()
    {
        _ = LancarProdutoAsync();
    }

    [RelayCommand]
    public async Task CapturarPesoBalancaAsync()
    {
        try
        {
            _logger.LogInformation("Capturando peso da balança de checkout...");
            var peso = await _balancaCheckoutService.LerPesoAsync();
            MensagemStatusBalanca = $"⚖️ Balança: {peso:N3} kg capturado!";

            if (ItemSelecionado != null)
            {
                ItemSelecionado.Quantidade = peso;
                ItemSelecionado.TotalCustomizado = null;
                AtualizarTotal();
                _logger.LogInformation("Peso {Peso} kg aplicado ao item selecionado '{Nome}'", peso, ItemSelecionado.Produto.Nome);
            }
            else if (ProdutoPesquisaSelecionado != null)
            {
                AdicionarAoCarrinho(ProdutoPesquisaSelecionado, peso);
                TextoPesquisa = string.Empty;
                ResultadosPesquisa.Clear();
                ProdutoPesquisaSelecionado = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler peso da balança de checkout");
            MensagemStatusBalanca = "⚠️ Falha ao comunicar com a balança.";
        }
    }

    [RelayCommand]
    private void AdicionarItem(Produto produto)
    {
        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ O Caixa está FECHADO! Abra o caixa antes de iniciar vendas.";
            return;
        }
        AdicionarAoCarrinho(produto, 1);
    }

    public void AdicionarAoCarrinho(Produto produto, int quantidade)
    {
        AdicionarAoCarrinho(produto, (decimal)quantidade, null);
    }

    public void AdicionarAoCarrinho(Produto produto, decimal quantidade, decimal? valorTotalCalculado = null)
    {
        if (produto == null || quantidade <= 0) return;

        var itemExistente = Carrinho.FirstOrDefault(x => x.Produto.Id == produto.Id && x.TotalCustomizado == null);
        if (itemExistente != null && !valorTotalCalculado.HasValue)
        {
            itemExistente.Quantidade += quantidade;
            var index = Carrinho.IndexOf(itemExistente);
            Carrinho[index] = new ProdutoItem { Produto = produto, Quantidade = itemExistente.Quantidade };
        }
        else
        {
            Carrinho.Add(new ProdutoItem 
            { 
                Produto = produto, 
                Quantidade = quantidade, 
                TotalCustomizado = valorTotalCalculado 
            });
        }

        AtualizarTotal();
    }

    [RelayCommand]
    private void IncrementarItem(ProdutoItem item)
    {
        if (item == null) return;
        item.Quantidade++;
        AtualizarTotal();
    }

    [RelayCommand]
    private void DecrementarItem(ProdutoItem item)
    {
        if (item == null) return;
        if (item.Quantidade > 1)
        {
            item.Quantidade--;
        }
        else
        {
            Carrinho.Remove(item);
        }
        AtualizarTotal();
    }

    [RelayCommand]
    private void RemoverItem(ProdutoItem item)
    {
        if (item == null) return;
        Carrinho.Remove(item);
        AtualizarTotal();
    }

    [RelayCommand]
    private void CancelarItemSelecionado()
    {
        if (ItemSelecionado != null)
        {
            _logger.LogInformation("Cancelando item do cupom: {Nome}", ItemSelecionado.Produto.Nome);
            Carrinho.Remove(ItemSelecionado);
            AtualizarTotal();
        }
    }

    [RelayCommand]
    private void LimparCarrinho()
    {
        _logger.LogInformation("Limpando todo o cupom (carrinho limpo).");
        Carrinho.Clear();
        ClienteIdentificacao = string.Empty;
        PedidoBalcaoEmAtendimento = null;
        AtualizarTotal();
    }
}
