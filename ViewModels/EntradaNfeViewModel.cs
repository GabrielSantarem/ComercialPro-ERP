using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class ItemNotaFiscalVm : ObservableObject
{
    public int NumeroItem { get; set; }
    
    [ObservableProperty]
    public partial string CodigoFornecedor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DescricaoFornecedor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Ncm { get; set; } = "0000.00.00";

    [ObservableProperty]
    public partial string UnidadeFornecedor { get; set; } = "UN";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuantidadeEstoque))]
    [NotifyPropertyChangedFor(nameof(TotalBruto))]
    [NotifyPropertyChangedFor(nameof(CustoRealTotal))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial int QuantidadeFaturada { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuantidadeEstoque))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial int FatorConversao { get; set; } = 1;

    public int QuantidadeEstoque => QuantidadeFaturada * (FatorConversao > 0 ? FatorConversao : 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalBruto))]
    [NotifyPropertyChangedFor(nameof(CustoRealTotal))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial decimal PrecoUnitarioFaturado { get; set; }

    public decimal TotalBruto => QuantidadeFaturada * PrecoUnitarioFaturado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustoRealTotal))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial decimal RateioDespesas { get; set; }

    public decimal CustoRealTotal => TotalBruto + RateioDespesas;

    public decimal CustoUnitarioEstoque => QuantidadeEstoque > 0 ? CustoRealTotal / QuantidadeEstoque : 0;

    [ObservableProperty]
    public partial Produto? ProdutoVinculado { get; set; }
}

public partial class ParcelaFinanceiroVm : ObservableObject
{
    public int Numero { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public string Documento { get; set; } = string.Empty;
}

public partial class EntradaNfeViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly ILogger<EntradaNfeViewModel> _logger;

    // === STATUS DO DOCUMENTO FISCAL ===
    [ObservableProperty]
    public partial string StatusDocumento { get; set; } = "RASCUNHO"; // RASCUNHO, AGUARDANDO CONFERENCIA, LANCADA

    [ObservableProperty]
    public partial string StatusCor { get; set; } = "#F39C12"; // Amarelo

    // === CABEÇALHO FISCAL ===
    [ObservableProperty] public partial string NumeroNota { get; set; } = "000.148.920";
    [ObservableProperty] public partial string SerieNota { get; set; } = "1";
    [ObservableProperty] public partial string ChaveAcesso { get; set; } = "3526 0912 3456 7800 0190 5500 1000 1489 2018 9283 7461";
    [ObservableProperty] public partial DateTime? DataEmissao { get; set; } = DateTime.Today;
    [ObservableProperty] public partial DateTime? DataEntrada { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string NaturezaOperacao { get; set; } = "1.102 - Compra para Comercialização";

    // === DADOS DO FORNECEDOR ===
    [ObservableProperty] public partial string FornecedorCnpj { get; set; } = "12.345.678/0001-90";
    [ObservableProperty] public partial string FornecedorRazao { get; set; } = "DISTRIBUIDORA NACIONAL DE EMBALAGENS S/A";
    [ObservableProperty] public partial string FornecedorUf { get; set; } = "SP";
    [ObservableProperty] public partial string FornecedorIe { get; set; } = "110.293.847.112";

    // === LOGÍSTICA & DESPESAS RATEADAS ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalProdutos))]
    [NotifyPropertyChangedFor(nameof(ValorTotalNota))]
    public partial decimal ValorFrete { get; set; } = 0m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalProdutos))]
    [NotifyPropertyChangedFor(nameof(ValorTotalNota))]
    public partial decimal OutrasDespesas { get; set; } = 0m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalProdutos))]
    [NotifyPropertyChangedFor(nameof(ValorTotalNota))]
    public partial decimal DescontoComercial { get; set; } = 0m;

    // === ITENS & CATÁLOGO ===
    public ObservableCollection<ItemNotaFiscalVm> ItensNota { get; } = [];
    public ObservableCollection<Produto> ProdutosDisponiveis { get; } = [];
    public ObservableCollection<ParcelaFinanceiroVm> Parcelas { get; } = [];

    // === SELEÇÃO PARA INCLUSÃO RÁPIDA ===
    [ObservableProperty] public partial Produto? ItemSelecionadoCatalogo { get; set; }
    [ObservableProperty] public partial string ItemDescricaoFornecedor { get; set; } = string.Empty;
    [ObservableProperty] public partial string ItemUnidade { get; set; } = "UN";
    [ObservableProperty] public partial int ItemQtdFaturada { get; set; } = 10;
    [ObservableProperty] public partial int ItemFatorConversao { get; set; } = 1;
    [ObservableProperty] public partial decimal ItemPrecoFaturado { get; set; } = 5.50m;

    // === CONDIÇÃO DE PAGAMENTO ===
    [ObservableProperty] public partial string CondicaoPagamentoSelecionada { get; set; } = "Boleto 30/60 Dias";
    public ObservableCollection<string> CondicoesPagamento { get; } = 
    [
        "À Vista (Dinheiro/PIX)",
        "Boleto 30 Dias",
        "Boleto 30/60 Dias",
        "Boleto 30/60/90 Dias",
        "Sem Cobrança Financeira (Bonificação)"
    ];

    // === TOTAIS CALCULADOS ===
    public decimal TotalProdutos => ItensNota.Sum(x => x.TotalBruto);
    public decimal ValorTotalNota => TotalProdutos + ValorFrete + OutrasDespesas - DescontoComercial;

    [ObservableProperty]
    public partial string MensagemFeedback { get; set; } = string.Empty;

    public EntradaNfeViewModel(PdvService service, ILogger<EntradaNfeViewModel> logger)
    {
        _service = service;
        _logger = logger;
        _ = CarregarCatalogoAsync();
        CarregarExemploPadrao();
        RecalcularFinanceiro();
    }

    public async Task CarregarCatalogoAsync()
    {
        ProdutosDisponiveis.Clear();
        var lista = await _service.ObterTodosProdutosAsync();
        foreach (var p in lista) ProdutosDisponiveis.Add(p);
    }

    private void CarregarExemploPadrao()
    {
        ItensNota.Clear();
        ItensNota.Add(new ItemNotaFiscalVm
        {
            NumeroItem = 1,
            CodigoFornecedor = "EMB-201",
            DescricaoFornecedor = "SACOLA BRANCA REFORÇADA 2K (FDO C/500)",
            Ncm = "3923.21.90",
            UnidadeFornecedor = "FD",
            QuantidadeFaturada = 2,
            FatorConversao = 500, // 2 fardos de 500 = 1000 unidades
            PrecoUnitarioFaturado = 65.00m,
            RateioDespesas = 5.00m
        });

        ItensNota.Add(new ItemNotaFiscalVm
        {
            NumeroItem = 2,
            CodigoFornecedor = "DESC-55",
            DescricaoFornecedor = "COPO DESCARTÁVEL 200ML CRISTAL (CX C/2500)",
            Ncm = "3924.10.00",
            UnidadeFornecedor = "CX",
            QuantidadeFaturada = 1,
            FatorConversao = 2500,
            PrecoUnitarioFaturado = 120.00m,
            RateioDespesas = 10.00m
        });

        AtualizarTotais();
    }

    partial void OnCondicaoPagamentoSelecionadaChanged(string value)
    {
        RecalcularFinanceiro();
    }

    partial void OnValorFreteChanged(decimal value) => AtualizarTotais();
    partial void OnOutrasDespesasChanged(decimal value) => AtualizarTotais();
    partial void OnDescontoComercialChanged(decimal value) => AtualizarTotais();

    private void AtualizarTotais()
    {
        OnPropertyChanged(nameof(TotalProdutos));
        OnPropertyChanged(nameof(ValorTotalNota));
        RecalcularFinanceiro();
    }

    private void RecalcularFinanceiro()
    {
        Parcelas.Clear();
        var total = ValorTotalNota;
        if (total <= 0) return;

        var baseDoc = string.IsNullOrWhiteSpace(NumeroNota) ? "DOC" : NumeroNota;

        if (CondicaoPagamentoSelecionada.Contains("À Vista"))
        {
            Parcelas.Add(new ParcelaFinanceiroVm
            {
                Numero = 1,
                Vencimento = DateTime.Today,
                Valor = total,
                Documento = $"{baseDoc}/01"
            });
        }
        else if (CondicaoPagamentoSelecionada.Contains("30/60/90"))
        {
            var valorParcela = Math.Round(total / 3, 2);
            Parcelas.Add(new ParcelaFinanceiroVm { Numero = 1, Vencimento = DateTime.Today.AddDays(30), Valor = valorParcela, Documento = $"{baseDoc}/01" });
            Parcelas.Add(new ParcelaFinanceiroVm { Numero = 2, Vencimento = DateTime.Today.AddDays(60), Valor = valorParcela, Documento = $"{baseDoc}/02" });
            Parcelas.Add(new ParcelaFinanceiroVm { Numero = 3, Vencimento = DateTime.Today.AddDays(90), Valor = total - (valorParcela * 2), Documento = $"{baseDoc}/03" });
        }
        else if (CondicaoPagamentoSelecionada.Contains("30/60"))
        {
            var valorParcela = Math.Round(total / 2, 2);
            Parcelas.Add(new ParcelaFinanceiroVm { Numero = 1, Vencimento = DateTime.Today.AddDays(30), Valor = valorParcela, Documento = $"{baseDoc}/01" });
            Parcelas.Add(new ParcelaFinanceiroVm { Numero = 2, Vencimento = DateTime.Today.AddDays(60), Valor = total - valorParcela, Documento = $"{baseDoc}/02" });
        }
        else if (CondicaoPagamentoSelecionada.Contains("30 Dias"))
        {
            Parcelas.Add(new ParcelaFinanceiroVm { Numero = 1, Vencimento = DateTime.Today.AddDays(30), Valor = total, Documento = $"{baseDoc}/01" });
        }
    }

    [RelayCommand]
    private void AdicionarItemManual()
    {
        if (string.IsNullOrWhiteSpace(ItemDescricaoFornecedor))
        {
            ItemDescricaoFornecedor = ItemSelecionadoCatalogo?.Nome ?? "PRODUTO DIVERSO";
        }

        var novoItem = new ItemNotaFiscalVm
        {
            NumeroItem = ItensNota.Count + 1,
            CodigoFornecedor = $"FORN-{ItensNota.Count + 1:D3}",
            DescricaoFornecedor = ItemDescricaoFornecedor,
            UnidadeFornecedor = ItemUnidade,
            QuantidadeFaturada = ItemQtdFaturada > 0 ? ItemQtdFaturada : 1,
            FatorConversao = ItemFatorConversao > 0 ? ItemFatorConversao : 1,
            PrecoUnitarioFaturado = ItemPrecoFaturado,
            ProdutoVinculado = ItemSelecionadoCatalogo
        };

        ItensNota.Add(novoItem);
        AtualizarTotais();

        // Reseta form do item
        ItemDescricaoFornecedor = string.Empty;
        ItemPrecoFaturado = 0m;
        MensagemFeedback = "Item adicionado à grade da NF-e!";
    }

    [RelayCommand]
    private void RemoverItem(ItemNotaFiscalVm item)
    {
        ItensNota.Remove(item);
        AtualizarTotais();
    }

    [RelayCommand]
    private void SimularImportacaoXml()
    {
        // Simula a leitura de um XML oficial da SEFAZ
        NumeroNota = "000.582.114";
        SerieNota = "2";
        ChaveAcesso = "4126 0900 1122 3300 0144 5500 2000 5821 1419 8271 9283";
        FornecedorCnpj = "00.112.233/0001-44";
        FornecedorRazao = "BRASIL ATACADISTA E DISTRIBUIDOR DE ALIMENTOS LTDA";
        FornecedorUf = "PR";
        NaturezaOperacao = "1.102 - Compra para Comercialização";
        ValorFrete = 35.00m;

        ItensNota.Clear();
        ItensNota.Add(new ItemNotaFiscalVm
        {
            NumeroItem = 1,
            CodigoFornecedor = "SKOL-LATA-12",
            DescricaoFornecedor = "CERVEJA SKOL LATA 350ML (PACK C/12)",
            Ncm = "2203.00.00",
            UnidadeFornecedor = "PK",
            QuantidadeFaturada = 10,
            FatorConversao = 12, // 10 packs x 12 latas = 120 latas
            PrecoUnitarioFaturado = 38.40m,
            RateioDespesas = 15.00m
        });

        ItensNota.Add(new ItemNotaFiscalVm
        {
            NumeroItem = 2,
            CodigoFornecedor = "GUAR-2L-6",
            DescricaoFornecedor = "REFRIGERANTE GUARANA 2L (FARDO C/6)",
            Ncm = "2202.10.00",
            UnidadeFornecedor = "FD",
            QuantidadeFaturada = 8,
            FatorConversao = 6, // 8 fardos x 6 pets = 48 unidades
            PrecoUnitarioFaturado = 42.00m,
            RateioDespesas = 20.00m
        });

        StatusDocumento = "XML IMPORTADO (AGUARDANDO CONFERÊNCIA)";
        StatusCor = "#2980B9"; // Azul
        AtualizarTotais();
        MensagemFeedback = "✅ Arquivo XML processado com sucesso! Grade preenchida.";
    }

    [RelayCommand]
    private async Task ProcessarEntradaFiscalAsync()
    {
        if (ItensNota.Count == 0)
        {
            MensagemFeedback = "❌ Impossível processar: A nota fiscal não possui itens!";
            return;
        }

        // Prepara lista para registrar a entrada de estoque
        var listaRegistro = ItensNota
            .Where(i => i.ProdutoVinculado != null)
            .Select(i => (i.ProdutoVinculado!.Id, i.QuantidadeEstoque, i.CustoUnitarioEstoque))
            .ToList();

        if (listaRegistro.Count == 0 && ProdutosDisponiveis.Count > 0)
        {
            // Se nenhum item foi vinculado manualmente, vincula com os produtos existentes por ordem
            for (int i = 0; i < ItensNota.Count; i++)
            {
                var p = ProdutosDisponiveis[i % ProdutosDisponiveis.Count];
                listaRegistro.Add((p.Id, ItensNota[i].QuantidadeEstoque, ItensNota[i].CustoUnitarioEstoque));
            }
        }

        await _service.RegistrarEntradaMercadoriaAsync(
            $"{NumeroNota} (Série {SerieNota})",
            FornecedorRazao,
            $"Chave: {ChaveAcesso} | Frete: R$ {ValorFrete:N2}",
            listaRegistro);

        StatusDocumento = "LANÇADA NO ESTOQUE & INTEGRADA AO FINANCEIRO";
        StatusCor = "#27AE60"; // Verde
        MensagemFeedback = "🚀 NOTA FISCAL PROCESSADA COM SUCESSO! Estoque alimentado e duplicatas geradas.";
        _logger.LogInformation("NF-e {Numero} do fornecedor {Fornecedor} processada.", NumeroNota, FornecedorRazao);
    }
}
