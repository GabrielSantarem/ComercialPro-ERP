using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Microsoft.Extensions.Logging;
using System;

namespace GetStartedApp.ViewModels;

public class ProdutoItem
{
    public Produto Produto { get; set; } = null!;
    public int Quantidade { get; set; }
    public decimal Total => Quantidade * (Produto?.Preco ?? 0m);
}

public partial class PdvViewModel : ViewModelBase
{
    private readonly PdvService _pdvService;
    private readonly ILogger<PdvViewModel> _logger;

    public ObservableCollection<ProdutoItem> Carrinho { get; } = [];
    public ObservableCollection<Produto> ResultadosPesquisa { get; } = [];
    public ObservableCollection<Vendedor> Vendedores { get; } = [];

    [ObservableProperty]
    private decimal _totalVenda;

    private void AtualizarTotal()
    {
        TotalVenda = Carrinho.Sum(x => x.Produto.Preco * x.Quantidade);
        OnPropertyChanged(nameof(Acrescimo));
        OnPropertyChanged(nameof(TotalComTaxa));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(PodeConfirmarPagamento));
        ValorRecebido = TotalComTaxa;
    }

    // === CONTROLE DE MODAL DE PAGAMENTO ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool IsModalAberto { get; set; }

    [ObservableProperty]
    public partial string ClienteIdentificacao { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Acrescimo))]
    [NotifyPropertyChangedFor(nameof(TotalComTaxa))]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    public partial string FormaPagamentoSelecionada { get; set; } = "Dinheiro";
    public ObservableCollection<string> FormasPagamento { get; } = ["Dinheiro", "PIX", "Débito", "Crédito (+2%)"];

    public decimal Acrescimo => FormaPagamentoSelecionada == "Crédito (+2%)" ? TotalVenda * 0.02m : 0m;
    public decimal TotalComTaxa => TotalVenda + Acrescimo;

    // === CONTROLE DE TURNOS DE CAIXA (ABERTURA, SANGRIA, FECHAMENTO) ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusCaixaTexto))]
    [NotifyPropertyChangedFor(nameof(StatusCaixaCor))]
    [NotifyPropertyChangedFor(nameof(SaldoCaixaDinheiroTexto))]
    public partial CaixaTurno? TurnoAtual { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusCaixaTexto))]
    [NotifyPropertyChangedFor(nameof(StatusCaixaCor))]
    [NotifyPropertyChangedFor(nameof(SaldoCaixaDinheiroTexto))]
    public partial bool IsCaixaAberto { get; set; }

    public string StatusCaixaTexto => IsCaixaAberto ? $"🟢 CAIXA ABERTO (TURNO #{TurnoAtual?.Id})" : "🔴 CAIXA FECHADO";
    public string StatusCaixaCor => IsCaixaAberto ? "#27AE60" : "#C0392B";
    public string SaldoCaixaDinheiroTexto => IsCaixaAberto ? $"Gaveta: R$ {TurnoAtual?.SaldoEsperadoEmDinheiro:N2}" : "Abra o Caixa";

    // === MODAL DE OPERAÇÕES DE CAIXA ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalCaixaAberto { get; set; }

    [ObservableProperty] public partial string TipoModalCaixa { get; set; } = "ABERTURA";
    [ObservableProperty] public partial string TituloModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial string DescricaoModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal ValorModalCaixa { get; set; }
    [ObservableProperty] public partial string MotivoModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial string MensagemCaixaErro { get; set; } = string.Empty;

    public bool IsBloqueadoPorModal => IsModalAberto || ModalCaixaAberto;

    [ObservableProperty]
    private ProdutoItem? _itemSelecionado;

    [ObservableProperty]
    private Produto? _produtoPesquisaSelecionado;

    [ObservableProperty]
    private Vendedor? _vendedorSelecionado;

    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    private decimal _valorRecebido;

    public decimal Troco => ValorRecebido > TotalComTaxa ? ValorRecebido - TotalComTaxa : 0m;
    public bool PodeConfirmarPagamento => ValorRecebido >= TotalComTaxa && TotalComTaxa > 0;

    public PdvViewModel(PdvService pdvService, ILogger<PdvViewModel> logger)
    {
        _pdvService = pdvService;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        _logger.LogInformation("Inicializando PdvViewModel e carregando vendedores...");
        Vendedores.Clear();
        var vendedoresDb = await _pdvService.ObterVendedoresAsync();
        foreach (var v in vendedoresDb) Vendedores.Add(v);

        VendedorSelecionado = Vendedores.FirstOrDefault();
        await AtualizarEstadoTurnoAsync();
    }

    public async Task AtualizarEstadoTurnoAsync()
    {
        TurnoAtual = await _pdvService.ObterTurnoAtualAsync();
        IsCaixaAberto = TurnoAtual != null;
        OnPropertyChanged(nameof(TurnoAtual));
        OnPropertyChanged(nameof(IsCaixaAberto));
        OnPropertyChanged(nameof(StatusCaixaTexto));
        OnPropertyChanged(nameof(StatusCaixaCor));
        OnPropertyChanged(nameof(SaldoCaixaDinheiroTexto));
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
            if (int.TryParse(match.Groups[1].Value, out int qtd) && qtd > 0)
            {
                return (qtd, match.Groups[2].Value.Trim());
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
    private void LancarProduto()
    {
        if (string.IsNullOrWhiteSpace(TextoPesquisa)) return;

        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ O Caixa está FECHADO! Informe o saldo de abertura antes de iniciar vendas.";
            return;
        }

        var (quantidade, termo) = ProcessarMultiplicador(TextoPesquisa);
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

    [RelayCommand]
    private void AdicionarItem(Produto produto)
    {
        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ O Caixa está FECHADO! Informe o saldo de abertura antes de iniciar vendas.";
            return;
        }
        AdicionarAoCarrinho(produto, 1);
    }

    private void AdicionarAoCarrinho(Produto produto, int quantidade)
    {
        var itemExistente = Carrinho.FirstOrDefault(x => x.Produto.Id == produto.Id);
        if (itemExistente != null)
        {
            itemExistente.Quantidade += quantidade;
            var index = Carrinho.IndexOf(itemExistente);
            Carrinho[index] = new ProdutoItem { Produto = produto, Quantidade = itemExistente.Quantidade };
        }
        else
        {
            Carrinho.Add(new ProdutoItem { Produto = produto, Quantidade = quantidade });
        }

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
    private void AbrirModalPagamento()
    {
        if (Carrinho.Count == 0)
        {
            _logger.LogWarning("Tentativa de fechar nota com carrinho vazio.");
            return;
        }

        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ Abra o caixa antes de finalizar qualquer venda!";
            return;
        }

        _logger.LogInformation("Abrindo modal de pagamento. Total: R$ {Total}", TotalVenda);
        IsModalAberto = true;
        ValorRecebido = TotalComTaxa;
    }

    [RelayCommand]
    private void FecharModalPagamento()
    {
        _logger.LogInformation("Fechando modal de pagamento (cancelado pelo usuário via ESC/Botão).");
        IsModalAberto = false;
    }

    [RelayCommand]
    private async Task ConfirmarPagamentoAsync()
    {
        if (!PodeConfirmarPagamento) return;

        _logger.LogInformation("Confirmando Checkout do Carrinho. Itens: {Qtd}, Cliente: {Cliente}", Carrinho.Count, ClienteIdentificacao);
        var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();

        await _pdvService.SalvarPedidoAsync(VendedorSelecionado!.Id, itens, FormaPagamentoSelecionada);

        Carrinho.Clear();
        TextoPesquisa = string.Empty;
        ClienteIdentificacao = string.Empty;
        ResultadosPesquisa.Clear();
        AtualizarTotal();
        IsModalAberto = false;
        await AtualizarEstadoTurnoAsync();
        _logger.LogInformation("Venda processada com sucesso. Modal fechado.");
    }

    [RelayCommand]
    private void LimparCarrinho()
    {
        _logger.LogInformation("Limpando todo o cupom (carrinho limpo).");
        Carrinho.Clear();
        AtualizarTotal();
    }

    // === COMANDOS DE OPERAÇÃO DE CAIXA (MODAL) ===

    [RelayCommand]
    public void AbrirModalAberturaCaixa()
    {
        TipoModalCaixa = "ABERTURA";
        TituloModalCaixa = "🟢 ABERTURA DE TURNO DE CAIXA";
        DescricaoModalCaixa = "Informe o fundo de troco inicial em dinheiro colocado na gaveta:";
        ValorModalCaixa = 100.00m;
        MotivoModalCaixa = "Fundo de troco inicial";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalSuprimento()
    {
        TipoModalCaixa = "SUPRIMENTO";
        TituloModalCaixa = "➕ SUPRIMENTO DE CAIXA (ENTRADA DE TROCO)";
        DescricaoModalCaixa = "Informe o valor em dinheiro que está entrando na gaveta:";
        ValorModalCaixa = 50.00m;
        MotivoModalCaixa = "Troco extra em moedas/cédulas";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalSangria()
    {
        TipoModalCaixa = "SANGRIA";
        TituloModalCaixa = "➖ SANGRIA DE CAIXA (RETIRADA PARA COFRE)";
        DescricaoModalCaixa = $"Saldo disponível na gaveta: R$ {TurnoAtual?.SaldoEsperadoEmDinheiro:N2}. Digite o valor a retirar:";
        ValorModalCaixa = Math.Min(100.00m, TurnoAtual?.SaldoEsperadoEmDinheiro ?? 0m);
        MotivoModalCaixa = "Recolhimento para o cofre pelo gerente";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalFechamentoCaixa()
    {
        TipoModalCaixa = "FECHAMENTO";
        TituloModalCaixa = "🔒 FECHAMENTO CEGO DE TURNO";
        DescricaoModalCaixa = "Conte o dinheiro físico presente na gaveta e informe o valor total apurado:";
        ValorModalCaixa = 0m;
        MotivoModalCaixa = "Fechamento de expediente";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void FecharModalCaixa()
    {
        ModalCaixaAberto = false;
        MensagemCaixaErro = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmarAcaoCaixaAsync()
    {
        try
        {
            MensagemCaixaErro = string.Empty;
            var vendedorId = VendedorSelecionado?.Id ?? 1;

            switch (TipoModalCaixa)
            {
                case "ABERTURA":
                    await _pdvService.AbrirCaixaAsync(vendedorId, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "SUPRIMENTO":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    await _pdvService.RegistrarSuprimentoAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "SANGRIA":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    await _pdvService.RegistrarSangriaAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "FECHAMENTO":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    var turnoFechado = await _pdvService.FecharCaixaAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    _logger.LogInformation("Fechamento concluído. Quebra: R$ {Quebra}", turnoFechado.DiferencaQuebra);
                    break;
            }

            await AtualizarEstadoTurnoAsync();
            ModalCaixaAberto = false;
        }
        catch (Exception ex)
        {
            MensagemCaixaErro = $"❌ {ex.Message}";
            _logger.LogWarning(ex, "Erro na ação de caixa ({Tipo})", TipoModalCaixa);
        }
    }
}
