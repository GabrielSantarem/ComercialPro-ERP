using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Fiscal;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class EntradaNfeViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly NfeXmlParserService _xmlParser;
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

    public EntradaNfeViewModel(PdvService service, NfeXmlParserService xmlParser, ILogger<EntradaNfeViewModel> logger)
    {
        _service = service;
        _xmlParser = xmlParser;
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
            FatorConversao = 500,
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

    // === PROCESSAMENTO REAL DE ARQUIVO XML DA SEFAZ ===
    public async Task CarregarXmlAsync(Stream stream)
    {
        try
        {
            var nfe = _xmlParser.ParseFromStream(stream);
            await AplicarNfeParseadaAsync(nfe);
            MensagemFeedback = $"✅ NF-e {NumeroNota} ({FornecedorRazao}) importada do XML com sucesso!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar arquivo XML");
            MensagemFeedback = $"❌ Erro ao ler XML: {ex.Message}";
        }
    }

    public async Task CarregarXmlStringAsync(string conteudoXml)
    {
        try
        {
            var nfe = _xmlParser.ParseFromString(conteudoXml);
            await AplicarNfeParseadaAsync(nfe);
            MensagemFeedback = $"✅ NF-e {NumeroNota} ({FornecedorRazao}) importada do XML com sucesso!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao interpretar string XML");
            MensagemFeedback = $"❌ Erro ao ler XML: {ex.Message}";
        }
    }

    private async Task AplicarNfeParseadaAsync(NfeParsedDto nfe)
    {
        NumeroNota = nfe.NumeroNota;
        SerieNota = nfe.Serie;
        ChaveAcesso = nfe.ChaveAcesso;
        DataEmissao = nfe.DataEmissao ?? DateTime.Today;
        NaturezaOperacao = nfe.NaturezaOperacao;
        FornecedorCnpj = nfe.EmitenteCnpj;
        FornecedorRazao = nfe.EmitenteRazaoSocial;
        FornecedorUf = nfe.EmitenteUf;
        FornecedorIe = nfe.EmitenteInscricaoEstadual;

        ValorFrete = nfe.ValorFrete;
        OutrasDespesas = nfe.OutrasDespesas + nfe.ValorSeguro;
        DescontoComercial = nfe.ValorDesconto;

        // Atualiza catálogo de produtos para correlação
        await CarregarCatalogoAsync();

        ItensNota.Clear();

        // Rateio proporcional de frete/outras despesas se os itens não trouxerem rateio explícito
        var totalItensBruto = nfe.Itens.Sum(i => i.ValorTotalBruto);
        var despesasGlobais = ValorFrete + OutrasDespesas;

        foreach (var itemXml in nfe.Itens)
        {
            // Tenta localizar produto já cadastrado por EAN ou por Descrição
            Produto? correspondente = null;
            if (!string.IsNullOrWhiteSpace(itemXml.CodigoEan))
            {
                correspondente = ProdutosDisponiveis.FirstOrDefault(p => p.CodigoBarras == itemXml.CodigoEan);
            }

            if (correspondente == null)
            {
                correspondente = ProdutosDisponiveis.FirstOrDefault(p => p.Nome.Trim().ToLower() == itemXml.Descricao.Trim().ToLower());
            }

            // Rateio de despesas do item
            decimal rateio = itemXml.ValorFreteRateado + itemXml.OutrasDespesasRateadas;
            if (rateio == 0 && totalItensBruto > 0 && despesasGlobais > 0)
            {
                rateio = Math.Round((itemXml.ValorTotalBruto / totalItensBruto) * despesasGlobais, 2);
            }

            var itemVm = new ItemNotaFiscalVm
            {
                NumeroItem = itemXml.NumeroItem,
                CodigoFornecedor = itemXml.CodigoProduto,
                CodigoEan = itemXml.CodigoEan,
                DescricaoFornecedor = itemXml.Descricao,
                Ncm = itemXml.Ncm,
                Cfop = itemXml.Cfop,
                UnidadeFornecedor = itemXml.UnidadeComercial,
                QuantidadeFaturada = (int)Math.Max(1, Math.Round(itemXml.QuantidadeComercial)),
                FatorConversao = 1,
                PrecoUnitarioFaturado = itemXml.ValorUnitario,
                RateioDespesas = rateio,
                ProdutoVinculado = correspondente
            };

            ItensNota.Add(itemVm);
        }

        // Se o XML possuir duplicatas/cobrança no cabeçalho, carrega direto
        if (nfe.Duplicatas.Count > 0)
        {
            Parcelas.Clear();
            int seq = 1;
            foreach (var d in nfe.Duplicatas)
            {
                Parcelas.Add(new ParcelaFinanceiroVm
                {
                    Numero = seq++,
                    Documento = string.IsNullOrWhiteSpace(d.Numero) ? $"{NumeroNota}/{seq:D2}" : d.Numero,
                    Vencimento = d.Vencimento ?? DateTime.Today.AddDays(30 * seq),
                    Valor = d.Valor
                });
            }
        }
        else
        {
            RecalcularFinanceiro();
        }

        StatusDocumento = "XML IMPORTADO (AGUARDANDO CONFERÊNCIA)";
        StatusCor = "#2980B9";
        AtualizarTotais();
    }

    [RelayCommand]
    private async Task ProcessarEntradaFiscalAsync()
    {
        if (ItensNota.Count == 0)
        {
            MensagemFeedback = "❌ Impossível processar: A nota fiscal não possui itens!";
            return;
        }

        var listaRegistro = new System.Collections.Generic.List<(int ProdutoId, int Quantidade, decimal CustoUnitario)>();

        // Para cada item da nota, garante que o produto existe ou cria no catálogo
        foreach (var item in ItensNota)
        {
            var prod = item.ProdutoVinculado;
            if (prod == null)
            {
                // Criação automática no catálogo via metadados do XML
                prod = await _service.ObterOuCriarProdutoPorXmlAsync(
                    item.DescricaoFornecedor,
                    item.CodigoEan,
                    item.Ncm,
                    item.UnidadeFornecedor,
                    precoVendaSugerido: 0,
                    custoUnitario: item.CustoUnitarioEstoque);

                item.ProdutoVinculado = prod;
            }

            listaRegistro.Add((prod.Id, item.QuantidadeEstoque, item.CustoUnitarioEstoque));
        }

        // Prepara lista de parcelas financeiras a pagar
        var listaParcelas = Parcelas
            .Select(p => ($"{p.Numero:D2}", p.Vencimento, p.Valor))
            .ToList();

        await _service.RegistrarEntradaMercadoriaAsync(
            $"{NumeroNota} (Série {SerieNota})",
            FornecedorRazao,
            $"Chave: {ChaveAcesso} | Frete: R$ {ValorFrete:N2}",
            listaRegistro,
            listaParcelas,
            FornecedorCnpj);

        await CarregarCatalogoAsync();

        StatusDocumento = "LANÇADA NO ESTOQUE & INTEGRADA AO FINANCEIRO";
        StatusCor = "#27AE60";
        MensagemFeedback = $"🚀 NOTA FISCAL PROCESSADA COM SUCESSO! Estoque alimentado e {listaParcelas.Count} títulos gerados no Contas a Pagar.";
        _logger.LogInformation("NF-e {Numero} do fornecedor {Fornecedor} processada com sucesso com {Titulos} títulos financeiros.",
            NumeroNota, FornecedorRazao, listaParcelas.Count);
    }
}
