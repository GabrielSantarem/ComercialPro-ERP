using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Models.Fiscal;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class PdvViewModel
{
    // === CONTROLE DE MODAL DE PAGAMENTO & SPLIT PAYMENT ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool IsModalAberto { get; set; }

    [ObservableProperty]
    public partial string ClienteIdentificacao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PedidoBalcao? PedidoBalcaoEmAtendimento { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Acrescimo))]
    [NotifyPropertyChangedFor(nameof(TotalComTaxa))]
    [NotifyPropertyChangedFor(nameof(SaldoRestante))]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    public partial string FormaPagamentoSelecionada { get; set; } = "Dinheiro";
    public ObservableCollection<string> FormasPagamento { get; } = ["Dinheiro", "PIX", "Débito", "Crédito (+2%)", "Vale-Crédito", "Crediário (Fiado)"];

    public decimal Acrescimo => FormaPagamentoSelecionada == "Crédito (+2%)" ? TotalVenda * 0.02m : 0m;
    public decimal TotalComTaxa => TotalVenda + Acrescimo;

    // Lista de múltiplas parcelas / formas de pagamento (Split Payment)
    public ObservableCollection<ItemPagamentoCheckout> PagamentosAdicionados { get; } = [];

    public decimal TotalPago => PagamentosAdicionados.Sum(p => p.Valor);
    
    public decimal SaldoRestante => Math.Max(0, TotalComTaxa - TotalPago);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    private decimal _valorRecebido;

    public decimal Troco => PagamentosAdicionados.Count > 0
        ? (TotalPago > TotalComTaxa ? TotalPago - TotalComTaxa : 0m)
        : (ValorRecebido > TotalComTaxa ? ValorRecebido - TotalComTaxa : 0m);

    public bool PodeConfirmarPagamento => PagamentosAdicionados.Count > 0
        ? TotalPago >= TotalComTaxa && TotalComTaxa > 0
        : ValorRecebido >= TotalComTaxa && TotalComTaxa > 0;

    // === VALE-CRÉDITO (REV-003) ===
    [ObservableProperty]
    public partial string CodigoValeDigitado { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MensagemStatusVale { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal SaldoValeConsultado { get; set; }

    // === CREDIÁRIO / FIADO (REV-003) ===
    public ObservableCollection<Cliente> ClientesCrediario { get; } = [];

    [ObservableProperty]
    public partial Cliente? ClienteCrediarioSelecionado { get; set; }

    [ObservableProperty]
    public partial string MensagemStatusCrediario { get; set; } = string.Empty;

    // === CONTROLE FISCAL NFC-e (ZEUS) & DANFE A4 ===
    [ObservableProperty]
    public partial bool EmitirNfceAoFinalizar { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalNfceEmitidaAberto { get; set; }

    [ObservableProperty]
    public partial string? UltimaNfceDanfeTexto { get; set; }

    [ObservableProperty]
    public partial string? UltimaNfceChaveAcesso { get; set; }

    [ObservableProperty]
    public partial string? UltimaNfceMensagemStatus { get; set; }

    [ObservableProperty]
    public partial string? UltimoCaminhoPdfA4 { get; set; }

    private DadosEmissaoNfce? _ultimosDadosNfce;
    private RetornoEmissaoNfce? _ultimoRetornoNfce;

    [RelayCommand]
    public void AbrirModalPagamento()
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

        _logger.LogInformation("Abrindo modal de pagamento. Total Venda: {Total}", TotalVenda);
        ClienteIdentificacao = string.Empty;
        FormaPagamentoSelecionada = "Dinheiro";
        PagamentosAdicionados.Clear();
        CodigoValeDigitado = string.Empty;
        MensagemStatusVale = string.Empty;
        MensagemStatusCrediario = string.Empty;
        ClienteCrediarioSelecionado = null;

        _ = CarregarClientesCrediarioAsync();

        IsModalAberto = true;
        ValorRecebido = TotalComTaxa;
        NotificarValoresPagamento();
    }

    private async Task CarregarClientesCrediarioAsync()
    {
        if (_clienteService != null)
        {
            ClientesCrediario.Clear();
            var clientes = await _clienteService.PesquisarClientesAsync(string.Empty);
            foreach (var c in clientes) ClientesCrediario.Add(c);
        }
    }

    public void AbrirModalPagamentoParaPedidoBalcao(PedidoBalcao pedido)
    {
        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ Abra o caixa antes de receber pedidos do balcão!";
            return;
        }

        _logger.LogInformation("Recebendo Pedido Balcão #{Comanda} ({Cliente}) para faturamento.", 
            pedido.NumeroComanda, pedido.ClienteNome);

        PedidoBalcaoEmAtendimento = pedido;
        ClienteIdentificacao = string.IsNullOrWhiteSpace(pedido.ClienteCpf) 
            ? pedido.ClienteNome 
            : $"{pedido.ClienteNome} (CPF: {pedido.ClienteCpf})";

        Carrinho.Clear();
        foreach (var item in pedido.Itens)
        {
            Carrinho.Add(new ProdutoItem { Produto = item.Produto, Quantidade = item.Quantidade });
        }
        AtualizarTotal();

        FormaPagamentoSelecionada = "Dinheiro";
        PagamentosAdicionados.Clear();
        CodigoValeDigitado = string.Empty;
        MensagemStatusVale = string.Empty;
        MensagemStatusCrediario = string.Empty;
        ClienteCrediarioSelecionado = null;

        _ = CarregarClientesCrediarioAsync();

        IsModalAberto = true;
        ValorRecebido = TotalComTaxa;
        NotificarValoresPagamento();
    }

    [RelayCommand]
    public async Task ConsultarEAplicarValeAsync()
    {
        if (_trocaService == null)
        {
            MensagemStatusVale = "Serviço de vale não disponível.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CodigoValeDigitado))
        {
            MensagemStatusVale = "Informe o código do Vale-Crédito.";
            return;
        }

        var vale = await _trocaService.ConsultarValeAsync(CodigoValeDigitado);
        if (vale == null)
        {
            MensagemStatusVale = "Vale-Crédito não localizado.";
            return;
        }

        if (vale.Status != "ATIVO" || vale.SaldoDisponivel <= 0)
        {
            MensagemStatusVale = $"Vale indisponível (Status: {vale.Status}, Saldo: R$ {vale.SaldoDisponivel:N2}).";
            return;
        }

        SaldoValeConsultado = vale.SaldoDisponivel;
        var valorNecessario = SaldoRestante > 0 ? SaldoRestante : TotalComTaxa;
        var valorAbater = Math.Min(vale.SaldoDisponivel, valorNecessario);

        PagamentosAdicionados.Add(new ItemPagamentoCheckout
        {
            Forma = $"Vale-Crédito ({vale.Codigo})",
            Valor = valorAbater,
            MeioPagamentoCodigo = "99"
        });

        NotificarValoresPagamento();
        MensagemStatusVale = $"✅ Vale {vale.Codigo} aplicado: R$ {valorAbater:N2} abatidos!";
        ValorRecebido = SaldoRestante;
    }

    [RelayCommand]
    public async Task AvaliarEAplicarCrediarioAsync()
    {
        if (_clienteService == null)
        {
            MensagemStatusCrediario = "Serviço de clientes não disponível.";
            return;
        }

        if (ClienteCrediarioSelecionado == null)
        {
            MensagemStatusCrediario = "Selecione um cliente para prosseguir no crediário.";
            return;
        }

        var valorNecessario = SaldoRestante > 0 ? SaldoRestante : TotalComTaxa;
        var avaliacao = await _clienteService.AvaliarCreditoAsync(ClienteCrediarioSelecionado.Id, valorNecessario);

        if (!avaliacao.AptoParaCrediario)
        {
            MensagemStatusCrediario = $"❌ Crediário Recusado: {avaliacao.MotivoRestricao}";
            return;
        }

        PagamentosAdicionados.Add(new ItemPagamentoCheckout
        {
            Forma = $"Crediário ({ClienteCrediarioSelecionado.Nome})",
            Valor = valorNecessario,
            MeioPagamentoCodigo = "05"
        });

        NotificarValoresPagamento();
        MensagemStatusCrediario = $"✅ Crediário Aprovado: R$ {valorNecessario:N2} (Saldo Disp. Restante: R$ {(avaliacao.LimiteDisponivel - valorNecessario):N2})";
        ValorRecebido = SaldoRestante;
    }

    [RelayCommand]
    private void FecharModalPagamento()
    {
        _logger.LogInformation("Fechando modal de pagamento (cancelado pelo usuário via ESC/Botão).");
        IsModalAberto = false;
        PedidoBalcaoEmAtendimento = null;
        PagamentosAdicionados.Clear();
    }

    [RelayCommand]
    public void AdicionarParcelaPagamento()
    {
        var valorParaAdicionar = ValorRecebido > 0 ? ValorRecebido : SaldoRestante;
        if (valorParaAdicionar <= 0) return;

        var codigoSefaz = FormaPagamentoSelecionada switch
        {
            "Dinheiro" => "01",
            "PIX" => "17",
            "Débito" => "04",
            "Crédito (+2%)" => "03",
            "Vale-Crédito" => "99",
            "Crediário (Fiado)" => "05",
            _ => "99"
        };

        PagamentosAdicionados.Add(new ItemPagamentoCheckout
        {
            Forma = FormaPagamentoSelecionada,
            Valor = valorParaAdicionar,
            MeioPagamentoCodigo = codigoSefaz
        });

        NotificarValoresPagamento();
        ValorRecebido = SaldoRestante;

        _logger.LogInformation("Parcela adicionada: {Forma} R$ {Valor:N2}. Saldo restante: R$ {Saldo:N2}",
            FormaPagamentoSelecionada, valorParaAdicionar, SaldoRestante);
    }

    [RelayCommand]
    public void RemoverParcelaPagamento(ItemPagamentoCheckout? parcela)
    {
        if (parcela == null) return;
        PagamentosAdicionados.Remove(parcela);

        NotificarValoresPagamento();
        ValorRecebido = SaldoRestante > 0 ? SaldoRestante : TotalComTaxa;

        _logger.LogInformation("Parcela removida. Novo saldo restante: R$ {Saldo:N2}", SaldoRestante);
    }

    private void NotificarValoresPagamento()
    {
        OnPropertyChanged(nameof(TotalPago));
        OnPropertyChanged(nameof(SaldoRestante));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(PodeConfirmarPagamento));
    }

    [RelayCommand]
    public void FecharModalNfceEmitida()
    {
        ModalNfceEmitidaAberto = false;
    }

    [RelayCommand]
    public void VisualizarDanfeA4Pdf()
    {
        if (_danfePdfService == null || _fiscalConfig == null || _ultimosDadosNfce == null || _ultimoRetornoNfce == null)
        {
            _logger.LogWarning("Não há dados fiscais para gerar o DANFE A4 PDF.");
            return;
        }

        try
        {
            _logger.LogInformation("Gerando DANFE A4 em PDF para a chave {Chave}...", _ultimoRetornoNfce.ChaveAcesso);
            var pdfBytes = _danfePdfService.GerarDanfeA4Pdf(_ultimosDadosNfce, _ultimoRetornoNfce, _fiscalConfig);
            var caminho = _danfePdfService.SalvarPdf(pdfBytes, _ultimoRetornoNfce.ChaveAcesso);
            UltimoCaminhoPdfA4 = caminho;

            _logger.LogInformation("Abrindo visualizador nativo e tela de impressão para: {Caminho}", caminho);
            _danfePdfService.AbrirVisualizacaoEImpressao(caminho);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gerar ou abrir DANFE A4 em PDF.");
        }
    }

    [RelayCommand]
    private async Task ConfirmarPagamentoAsync()
    {
        if (!PodeConfirmarPagamento) return;

        _logger.LogInformation("Confirmando Checkout do Carrinho. Itens: {Qtd}, Cliente: {Cliente}", Carrinho.Count, ClienteIdentificacao);

        // Copiar itens para emissão fiscal utilizando dados cadastrais reais do produto
        var itensParaNfce = Carrinho.Select((i, idx) => new ItemEmissaoNfceDto
        {
            ItemNumero = idx + 1,
            CodigoProduto = i.Produto.Id.ToString(),
            CodigoBarrasEan = string.IsNullOrWhiteSpace(i.Produto.CodigoBarras) ? "SEM GTIN" : i.Produto.CodigoBarras,
            DescricaoProduto = i.Produto.Nome,
            Ncm = !string.IsNullOrWhiteSpace(i.Produto.Ncm) ? i.Produto.Ncm.Trim() : "22021000",
            Cfop = 5102,
            UnidadeComercial = !string.IsNullOrWhiteSpace(i.Produto.UnidadeMedida) ? i.Produto.UnidadeMedida.Trim().ToUpper() : "UN",
            Quantidade = i.Quantidade,
            ValorUnitario = i.Produto.Preco,
            Csosn = "102",
            AliquotaTributosAproximadosPercentual = 15.00m
        }).ToList();

        List<PagamentoEmissaoNfceDto> pagamentosNfce;
        List<(string Forma, decimal Valor)> parcelasDetalhadas;
        string formaDescricao;

        if (PagamentosAdicionados.Count > 0)
        {
            pagamentosNfce = PagamentosAdicionados.Select(p => new PagamentoEmissaoNfceDto
            {
                MeioPagamento = p.MeioPagamentoCodigo,
                Valor = p.Valor
            }).ToList();
            parcelasDetalhadas = PagamentosAdicionados.Select(p => (p.Forma, p.Valor)).ToList();
            formaDescricao = string.Join(" + ", PagamentosAdicionados.Select(p => p.Forma).Distinct());
        }
        else
        {
            var formaCod = FormaPagamentoSelecionada switch
            {
                "Dinheiro" => "01",
                "PIX" => "17",
                "Débito" => "04",
                "Crédito (+2%)" => "03",
                "Vale-Crédito" => "99",
                "Crediário (Fiado)" => "05",
                _ => "99"
            };

            pagamentosNfce = [new PagamentoEmissaoNfceDto { MeioPagamento = formaCod, Valor = ValorRecebido }];
            parcelasDetalhadas = [(FormaPagamentoSelecionada, TotalComTaxa)];
            formaDescricao = FormaPagamentoSelecionada;
        }

        Venda? vendaCriada = null;
        if (PedidoBalcaoEmAtendimento != null)
        {
            // Fatura o pedido que veio da fila do balcão
            await _pdvService.FaturarPedidoBalcaoNoCaixaAsync(PedidoBalcaoEmAtendimento.Id, formaDescricao, parcelasDetalhadas);
            PedidoBalcaoEmAtendimento = null;
        }
        else
        {
            // Venda direta lançada pelo caixa
            var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();
            var vendedorId = VendedorSelecionado?.Id ?? 1;
            vendaCriada = await _pdvService.SalvarPedidoAsync(vendedorId, itens, formaDescricao, parcelasDetalhadas);
        }

        // Se houve utilização de Vale-Crédito, resgata no serviço
        if (_trocaService != null && vendaCriada != null)
        {
            var parcelasVale = PagamentosAdicionados.Where(p => p.Forma.StartsWith("Vale-Crédito")).ToList();
            foreach (var pv in parcelasVale)
            {
                var inicio = pv.Forma.IndexOf('(');
                var fim = pv.Forma.IndexOf(')');
                if (inicio >= 0 && fim > inicio)
                {
                    var cod = pv.Forma.Substring(inicio + 1, fim - inicio - 1);
                    await _trocaService.ResgatarValeCreditoAsync(cod, pv.Valor, vendaCriada.Id);
                }
            }
        }

        // Emissão Fiscal Automática NFC-e se estiver ativo
        if (EmitirNfceAoFinalizar && _nfceService != null && _fiscalConfig != null)
        {
            var dadosNfce = new DadosEmissaoNfce
            {
                CpfConsumidor = string.IsNullOrWhiteSpace(ClienteIdentificacao) ? null : ClienteIdentificacao,
                NomeConsumidor = string.IsNullOrWhiteSpace(ClienteIdentificacao) ? null : "CLIENTE BALCAO",
                Itens = itensParaNfce,
                Pagamentos = pagamentosNfce,
                ValorTroco = Troco,
                ModoContingenciaOffline = false
            };

            var retornoNfce = _nfceService.EmitirNfce(dadosNfce, _fiscalConfig);
            if (retornoNfce.Sucesso)
            {
                _ultimosDadosNfce = dadosNfce;
                _ultimoRetornoNfce = retornoNfce;

                if (vendaCriada != null)
                {
                    await _pdvService.VincularDadosFiscaisVendaAsync(
                        vendaCriada.Id,
                        retornoNfce.ChaveAcesso,
                        retornoNfce.NumeroNota,
                        retornoNfce.Serie,
                        retornoNfce.XmlAssinado);
                }

                UltimaNfceDanfeTexto = retornoNfce.DanfeTextoTermica;
                UltimaNfceChaveAcesso = retornoNfce.ChaveAcesso;
                UltimaNfceMensagemStatus = $"✅ NFC-e Nº {retornoNfce.NumeroNota:D6} emitida e assinada com sucesso!";
                ModalNfceEmitidaAberto = true;
                _logger.LogInformation("NFC-e emitida com sucesso. Chave: {Chave}", retornoNfce.ChaveAcesso);
            }
            else
            {
                UltimaNfceMensagemStatus = $"⚠️ Erro na emissão fiscal: {retornoNfce.Mensagem}";
                _logger.LogWarning("Falha ao emitir NFC-e: {Msg}", retornoNfce.Mensagem);
            }
        }

        Carrinho.Clear();
        TextoPesquisa = string.Empty;
        ClienteIdentificacao = string.Empty;
        ResultadosPesquisa.Clear();
        PagamentosAdicionados.Clear();
        AtualizarTotal();
        IsModalAberto = false;
        await AtualizarEstadoTurnoAsync();
        await AtualizarFilaPedidosAsync();
        _logger.LogInformation("Venda processada com sucesso. Modal fechado e fila atualizada.");
    }
}
