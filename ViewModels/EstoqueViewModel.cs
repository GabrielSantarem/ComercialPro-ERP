using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;

namespace GetStartedApp.ViewModels;

public partial class EstoqueViewModel : ViewModelBase
{
    private readonly PdvService _service;

    [ObservableProperty]
    public partial DateTime? DataFiltro { get; set; } = DateTime.Today;

    public ObservableCollection<Produto> ProdutosLista { get; } = [];
    public ObservableCollection<RelatorioInventarioDto> Relatorio { get; } = [];

    // === FILTRO DE ESTOQUE MÍNIMO ===
    [ObservableProperty]
    public partial bool SomenteEstoqueCritico { get; set; }

    [ObservableProperty]
    public partial int TotalProdutosCriticos { get; set; }

    // === CADASTRO RÁPIDO ===
    [ObservableProperty] private string _novoNome = string.Empty;
    [ObservableProperty] private decimal _novoPreco;
    [ObservableProperty] private int _novoEstoque;
    [ObservableProperty] private int _novoEstoqueMinimo = 5;

    // === ABA: ENTRADA MANUAL DE NOTAS / MERCADORIAS ===
    [ObservableProperty] public partial string EntradaNumeroNota { get; set; } = string.Empty;
    [ObservableProperty] public partial string EntradaFornecedor { get; set; } = string.Empty;
    [ObservableProperty] public partial string EntradaObservacao { get; set; } = string.Empty;

    [ObservableProperty] public partial Produto? EntradaProdutoSelecionado { get; set; }
    [ObservableProperty] public partial int EntradaQtdItem { get; set; } = 1;
    [ObservableProperty] public partial decimal EntradaCustoItem { get; set; } = 0m;

    public ObservableCollection<ItemEntradaTemp> ItensEntrada { get; } = [];
    public ObservableCollection<EntradaMercadoria> HistoricoEntradas { get; } = [];

    public decimal TotalNotaEntrada => ItensEntrada.Sum(x => x.CustoTotal);

    // === ABA: PERDAS, AVARIAS E AJUSTES DE INVENTÁRIO ===
    [ObservableProperty] public partial Produto? AjusteProdutoSelecionado { get; set; }
    [ObservableProperty] public partial string AjusteTipoSelecionado { get; set; } = "AVARIA";
    public ObservableCollection<string> TiposAjuste { get; } = ["AVARIA", "VENCIMENTO", "BALANCO_FISICO", "OUTROS"];

    [ObservableProperty] public partial int AjusteQuantidade { get; set; } = 1;
    [ObservableProperty] public partial string AjusteMotivo { get; set; } = string.Empty;
    [ObservableProperty] public partial string AjusteResponsavel { get; set; } = "Operador Padrão";

    public ObservableCollection<AjusteEstoque> HistoricoAjustes { get; } = [];

    [ObservableProperty]
    private string _mensagemAviso = string.Empty;

    public EstoqueViewModel(PdvService service)
    {
        _service = service;
        _ = CarregarProdutosAsync();
        _ = BuscarGiroEstoqueAsync();
        _ = CarregarHistoricoEntradasAsync();
        _ = CarregarHistoricoAjustesAsync();
        _ = CarregarAuditoriaAsync();
        _ = CalcularSugestaoComprasAsync();
    }

    partial void OnSomenteEstoqueCriticoChanged(bool value)
    {
        _ = CarregarProdutosAsync();
    }

    [RelayCommand]
    public async Task CarregarProdutosAsync()
    {
        ProdutosLista.Clear();
        var lista = await _service.ObterTodosProdutosAsync();
        
        TotalProdutosCriticos = lista.Count(p => p.EstaAbaixoDoMinimo);

        if (SomenteEstoqueCritico)
        {
            lista = lista.Where(p => p.EstaAbaixoDoMinimo).ToList();
        }

        foreach (var p in lista) ProdutosLista.Add(p);
    }

    [RelayCommand]
    public async Task CarregarHistoricoEntradasAsync()
    {
        HistoricoEntradas.Clear();
        var lista = await _service.ObterHistoricoEntradasAsync();
        foreach (var e in lista) HistoricoEntradas.Add(e);
    }

    [RelayCommand]
    public async Task CarregarHistoricoAjustesAsync()
    {
        HistoricoAjustes.Clear();
        var lista = await _service.ObterHistoricoAjustesAsync();
        foreach (var a in lista) HistoricoAjustes.Add(a);
    }

    [RelayCommand]
    private async Task SalvarNovoProdutoAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoNome)) return;

        var pro = new Produto
        {
            Nome = NovoNome,
            Preco = NovoPreco,
            Estoque = NovoEstoque,
            EstoqueMinimo = NovoEstoqueMinimo >= 0 ? NovoEstoqueMinimo : 5
        };
        await _service.SalvarProdutoAsync(pro);

        NovoNome = string.Empty;
        NovoPreco = 0;
        NovoEstoque = 0;
        NovoEstoqueMinimo = 5;

        await CarregarProdutosAsync();
        MensagemAviso = "✅ Produto salvo com sucesso!";
        _ = LimparAvisoDepoisAsync();
    }

    [RelayCommand]
    private async Task BuscarGiroEstoqueAsync()
    {
        if (!DataFiltro.HasValue) return;
        Relatorio.Clear();
        var dataBusca = DataFiltro.Value.Date;
        var itens = await _service.GerarLevantamentoInventarioAsync(dataBusca);
        foreach (var i in itens)
        {
            Relatorio.Add(i);
        }
        MensagemAviso = $"Encontrados {itens.Count} produtos com movimentação na data {dataBusca:dd/MM/yyyy}.";
        _ = LimparAvisoDepoisAsync();
    }

    // === COMANDOS DA ENTRADA DE NOTA ===
    [RelayCommand]
    private void AdicionarItemNaNota()
    {
        if (EntradaProdutoSelecionado == null || EntradaQtdItem <= 0 || EntradaCustoItem < 0) return;

        var existente = ItensEntrada.FirstOrDefault(x => x.Produto.Id == EntradaProdutoSelecionado.Id);
        if (existente != null)
        {
            existente.Quantidade += EntradaQtdItem;
            existente.CustoUnitario = EntradaCustoItem;
        }
        else
        {
            ItensEntrada.Add(new ItemEntradaTemp
            {
                Produto = EntradaProdutoSelecionado,
                Quantidade = EntradaQtdItem,
                CustoUnitario = EntradaCustoItem
            });
        }

        OnPropertyChanged(nameof(TotalNotaEntrada));

        EntradaQtdItem = 1;
        EntradaCustoItem = 0m;
    }

    [RelayCommand]
    private void RemoverItemDaNota(ItemEntradaTemp item)
    {
        ItensEntrada.Remove(item);
        OnPropertyChanged(nameof(TotalNotaEntrada));
    }

    [RelayCommand]
    private async Task ConfirmarEntradaNotaAsync()
    {
        if (string.IsNullOrWhiteSpace(EntradaNumeroNota))
        {
            MensagemAviso = "⚠️ Informe o Número da Nota ou Pedido!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        if (ItensEntrada.Count == 0)
        {
            MensagemAviso = "⚠️ Adicione pelo menos 1 item na nota para dar entrada!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        var lista = ItensEntrada.Select(i => (i.Produto.Id, i.Quantidade, i.CustoUnitario)).ToList();

        await _service.RegistrarEntradaMercadoriaAsync(
            EntradaNumeroNota,
            string.IsNullOrWhiteSpace(EntradaFornecedor) ? "Fornecedor Padrão" : EntradaFornecedor,
            EntradaObservacao,
            lista);

        EntradaNumeroNota = string.Empty;
        EntradaFornecedor = string.Empty;
        EntradaObservacao = string.Empty;
        ItensEntrada.Clear();
        OnPropertyChanged(nameof(TotalNotaEntrada));

        await CarregarProdutosAsync();
        await CarregarHistoricoEntradasAsync();

        MensagemAviso = "✅ Entrada de mercadoria registrada e estoque alimentado com sucesso!";
        _ = LimparAvisoDepoisAsync();
    }

    // === COMANDOS DE BAIXA / AJUSTE POR AVARIA / BALANÇO ===
    [RelayCommand]
    private async Task RegistrarAjusteEstoqueAsync()
    {
        if (AjusteProdutoSelecionado == null)
        {
            MensagemAviso = "⚠️ Selecione o produto para realizar o ajuste!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        if (AjusteQuantidade <= 0)
        {
            MensagemAviso = "⚠️ A quantidade a ajustar deve ser maior que zero!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(AjusteMotivo))
        {
            MensagemAviso = "⚠️ Informe o motivo ou justificativa do ajuste!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        // Para perdas (Avaria ou Vencimento), a diferença é negativa (-Qtd)
        int diferenca = (AjusteTipoSelecionado == "AVARIA" || AjusteTipoSelecionado == "VENCIMENTO")
            ? -AjusteQuantidade
            : AjusteQuantidade;

        try
        {
            var ajuste = await _service.RegistrarAjusteEstoqueAsync(
                AjusteProdutoSelecionado.Id,
                diferenca,
                AjusteTipoSelecionado,
                AjusteMotivo,
                AjusteResponsavel);

            MensagemAviso = $"✅ Ajuste de estoque #{ajuste.Id} registrado com sucesso!";
            
            AjusteMotivo = string.Empty;
            AjusteQuantidade = 1;

            await CarregarProdutosAsync();
            await CarregarHistoricoAjustesAsync();
        }
        catch (Exception ex)
        {
            MensagemAviso = $"❌ Erro ao ajustar: {ex.Message}";
        }

        _ = LimparAvisoDepoisAsync();
    }

    private async Task LimparAvisoDepoisAsync()
    {
        await Task.Delay(4000);
        MensagemAviso = string.Empty;
    }

    // === ABA: AUDITORIA & INVENTÁRIO FÍSICO ===
    public ObservableCollection<ItemAuditoriaEstoqueDto> ItensAuditoria { get; } = [];
    [ObservableProperty] public partial ResumoAuditoriaDto ResumoAuditoria { get; set; } = new();
    [ObservableProperty] public partial string TermoBuscaAuditoria { get; set; } = string.Empty;
    [ObservableProperty] public partial string ResponsavelInventario { get; set; } = "Gerente de Loja";
    [ObservableProperty] public partial string ObservacaoInventario { get; set; } = "Inventário Físico Periódico";
    [ObservableProperty] public partial bool ExibirFolhaContagem { get; set; } = false;
    [ObservableProperty] public partial string FolhaContagemTexto { get; set; } = string.Empty;

    [ObservableProperty] public partial string EntradaCodigoOuNomeContagem { get; set; } = string.Empty;
    [ObservableProperty] public partial int EntradaQtdFisicaContada { get; set; } = 1;

    [RelayCommand]
    public async Task CarregarAuditoriaAsync()
    {
        ItensAuditoria.Clear();
        var lista = await _service.ObterItensParaAuditoriaAsync();
        foreach (var item in lista)
        {
            ItensAuditoria.Add(item);
        }
        RecalcularResumoAuditoria();
    }

    public void RecalcularResumoAuditoria()
    {
        ResumoAuditoria = _service.CalcularResumoAuditoria(ItensAuditoria);
    }

    [RelayCommand]
    public void LancarContagemRapida()
    {
        if (string.IsNullOrWhiteSpace(EntradaCodigoOuNomeContagem)) return;

        var termo = EntradaCodigoOuNomeContagem.Trim().ToLower();
        var item = ItensAuditoria.FirstOrDefault(i => 
            i.CodigoBarras.Equals(termo, StringComparison.OrdinalIgnoreCase) ||
            i.ProdutoId.ToString() == termo ||
            i.ProdutoNome.ToLower().Contains(termo));

        if (item != null)
        {
            item.SaldoFisicoContado = EntradaQtdFisicaContada;
            RecalcularResumoAuditoria();
            MensagemAviso = $"Contagem de '{item.ProdutoNome}' definida para {EntradaQtdFisicaContada} {item.UnidadeMedida}.";
            EntradaCodigoOuNomeContagem = string.Empty;
            EntradaQtdFisicaContada = 1;
        }
        else
        {
            MensagemAviso = "⚠️ Produto não encontrado na lista de inventário!";
        }

        _ = LimparAvisoDepoisAsync();
    }

    [RelayCommand]
    public void GerarFolhaContagem()
    {
        FolhaContagemTexto = _service.GerarTextoFolhaContagemCega(ItensAuditoria);
        ExibirFolhaContagem = true;
    }

    [RelayCommand]
    public void FecharFolhaContagem()
    {
        ExibirFolhaContagem = false;
    }

    [RelayCommand]
    public async Task EfetivarInventarioAsync()
    {
        var contados = ItensAuditoria.Where(i => i.SaldoFisicoContado.HasValue).ToList();
        if (contados.Count == 0)
        {
            MensagemAviso = "⚠️ Nenhum item teve contagem física informada ainda!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(ResponsavelInventario))
        {
            MensagemAviso = "⚠️ Informe o nome do responsável pela conferência do inventário!";
            _ = LimparAvisoDepoisAsync();
            return;
        }

        try
        {
            var pares = contados.Select(i => (i.ProdutoId, i.SaldoFisicoContado!.Value)).ToList();
            int ajustados = await _service.EfetivarInventarioFisicoAsync(pares, ResponsavelInventario, ObservacaoInventario);

            MensagemAviso = $"✅ Inventário efetivado com sucesso! {ajustados} produtos com divergência foram corrigidos no estoque.";

            await CarregarProdutosAsync();
            await CarregarHistoricoAjustesAsync();
            await CarregarAuditoriaAsync();
        }
        catch (Exception ex)
        {
            MensagemAviso = $"❌ Erro ao efetivar inventário: {ex.Message}";
        }

        _ = LimparAvisoDepoisAsync();
    }


    // === ABA: PONTO DE PEDIDO & SUGESTÃO DE COMPRAS ===
    public ObservableCollection<ItemSugestaoCompraDto> ItensSugestaoCompra { get; } = [];
    [ObservableProperty] public partial ResumoSugestaoComprasDto ResumoCompras { get; set; } = new();

    [ObservableProperty] public partial int ComprasDiasHistorico { get; set; } = 30;
    [ObservableProperty] public partial int ComprasLeadTime { get; set; } = 7;
    [ObservableProperty] public partial int ComprasCobertura { get; set; } = 15;
    [ObservableProperty] public partial string ComprasFornecedorCotacao { get; set; } = string.Empty;

    [ObservableProperty] public partial bool ExibirModalCotacao { get; set; } = false;
    [ObservableProperty] public partial string FolhaCotacaoTexto { get; set; } = string.Empty;

    [RelayCommand]
    public async Task CalcularSugestaoComprasAsync()
    {
        ItensSugestaoCompra.Clear();
        var resumo = await _service.CalcularSugestaoComprasAsync(ComprasDiasHistorico, ComprasLeadTime, ComprasCobertura);
        ResumoCompras = resumo;

        foreach (var item in resumo.Itens)
        {
            ItensSugestaoCompra.Add(item);
        }

        MensagemAviso = $"Sugestão de compras recalculada: {resumo.TotalItensParaComprar} produtos com necessidade de reposição.";
        _ = LimparAvisoDepoisAsync();
    }

    [RelayCommand]
    public void GerarFolhaCotacao()
    {
        FolhaCotacaoTexto = _service.GerarTextoCotacaoFornecedor(ItensSugestaoCompra, ComprasFornecedorCotacao);
        ExibirModalCotacao = true;
    }

    [RelayCommand]
    public void FecharModalCotacao()
    {
        ExibirModalCotacao = false;
    }

}
