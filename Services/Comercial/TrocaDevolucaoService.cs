using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services.Comercial;

public class TrocaDevolucaoService : ITrocaDevolucaoService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TrocaDevolucaoService> _logger;

    public TrocaDevolucaoService(AppDbContext db, ILogger<TrocaDevolucaoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ValeCredito> EmitirValeTrocaAsync(
        List<ItemDevolucaoDto> itensDevolvidos,
        string motivoGeral,
        string? clienteNome,
        string? clienteCpf,
        int? vendaOrigemId = null)
    {
        if (itensDevolvidos == null || itensDevolvidos.Count == 0)
            throw new ArgumentException("A lista de itens devolvidos não pode estar vazia.", nameof(itensDevolvidos));

        if (itensDevolvidos.Any(i => i.Quantidade <= 0))
            throw new ArgumentException("A quantidade de todos os itens devolvidos deve ser maior que zero.", nameof(itensDevolvidos));

        if (itensDevolvidos.Any(i => i.PrecoUnitario <= 0))
            throw new ArgumentException("O preço unitário de todos os itens deve ser maior que zero.", nameof(itensDevolvidos));

        // Se informada a venda de origem, validar se a quantidade devolvida não excede o total faturado
        if (vendaOrigemId.HasValue)
        {
            var vendaOrigem = await _db.Vendas
                .Include(v => v.Itens)
                .FirstOrDefaultAsync(v => v.Id == vendaOrigemId.Value);

            if (vendaOrigem == null)
            {
                throw new InvalidOperationException($"Venda de origem #{vendaOrigemId.Value} não encontrada.");
            }

            var itensAgrupados = itensDevolvidos
                .GroupBy(i => i.ProdutoId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantidade));

            foreach (var kvp in itensAgrupados)
            {
                var produtoId = kvp.Key;
                var qtdDevolvida = kvp.Value;
                var itemVendido = vendaOrigem.Itens.FirstOrDefault(iv => iv.ProdutoId == produtoId);
                var qtdVendida = itemVendido?.Quantidade ?? 0;

                if (qtdDevolvida > qtdVendida)
                {
                    throw new InvalidOperationException(
                        $"Quantidade devolvida ({qtdDevolvida}) para o produto #{produtoId} excede a quantidade vendida ({qtdVendida}) no cupom fiscal #{vendaOrigemId.Value}.");
                }
            }
        }

        decimal valorTotal = 0m;

        // Processar estoque de cada item devolvido
        foreach (var item in itensDevolvidos)
        {
            var produto = await _db.Produtos.FindAsync(item.ProdutoId);
            if (produto == null)
                throw new InvalidOperationException($"Produto com ID {item.ProdutoId} não encontrado no catálogo.");

            var valorSubtotal = item.Quantidade * item.PrecoUnitario;
            valorTotal += valorSubtotal;

            if (item.DestinarAvaria)
            {
                // Destino Quarentena / Avaria: Não incrementa estoque comercial
                _db.AjustesEstoque.Add(new AjusteEstoque
                {
                    ProdutoId = produto.Id,
                    QuantidadeDiferenca = 0,
                    EstoqueAnterior = produto.Estoque,
                    EstoqueNovo = produto.Estoque,
                    TipoAjuste = "SAIDA_AVARIA",
                    Motivo = $"Devolução/Troca com avaria/defeito: {item.Motivo} - Ref Venda Origem: #{vendaOrigemId?.ToString() ?? "Avulso"}",
                    Responsavel = "Operador PDV",
                    DataHora = DateTime.Now
                });
            }
            else
            {
                // Destino Estoque Comercial: Incrementa saldo físico imediatamente
                var estoqueAnterior = produto.Estoque;
                produto.Estoque += item.Quantidade;

                _db.AjustesEstoque.Add(new AjusteEstoque
                {
                    ProdutoId = produto.Id,
                    QuantidadeDiferenca = item.Quantidade,
                    EstoqueAnterior = estoqueAnterior,
                    EstoqueNovo = produto.Estoque,
                    TipoAjuste = "ENTRADA_AVULSA",
                    Motivo = $"Devolução/Troca - Retorno ao estoque comercial: {item.Motivo} - Ref Venda Origem: #{vendaOrigemId?.ToString() ?? "Avulso"}",
                    Responsavel = "Operador PDV",
                    DataHora = DateTime.Now
                });
            }
        }

        // Gera token único e legível para o Vale-Crédito
        var codigoToken = GerarCodigoValeUnico();

        var vale = new ValeCredito
        {
            Codigo = codigoToken,
            ValorOriginal = valorTotal,
            SaldoDisponivel = valorTotal,
            VendaOrigemId = vendaOrigemId,
            ClienteNome = clienteNome,
            ClienteCpf = clienteCpf,
            DataEmissao = DateTime.Now,
            DataValidade = DateTime.Now.AddDays(30),
            Status = "ATIVO",
            MotivoDevolucao = motivoGeral
        };

        _db.ValesCredito.Add(vale);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Vale-Crédito '{Codigo}' emitido com sucesso no valor de R$ {Valor:N2} para {Cliente}",
            vale.Codigo, vale.ValorOriginal, clienteNome ?? "Consumidor");

        return vale;
    }

    public async Task<(bool Sucesso, string Mensagem, decimal ValorAbatido)> ResgatarValeCreditoAsync(
        string codigoVale,
        decimal valorNecessario,
        int vendaDestinoId)
    {
        if (string.IsNullOrWhiteSpace(codigoVale))
            return (false, "Código de Vale-Crédito inválido.", 0m);

        if (valorNecessario <= 0)
            return (false, "Valor a abater deve ser maior que zero.", 0m);

        var codigoFormatado = codigoVale.Trim().ToUpperInvariant();

        var vale = await _db.ValesCredito.FirstOrDefaultAsync(v => v.Codigo.ToUpper() == codigoFormatado);
        if (vale == null)
            return (false, "Vale-Crédito não encontrado.", 0m);

        if (vale.Status == "CANCELADO")
            return (false, "Vale-Crédito cancelado.", 0m);

        if (vale.DataValidade.Date < DateTime.Today || vale.Status == "EXPIRADO")
        {
            if (vale.Status != "EXPIRADO")
            {
                vale.Status = "EXPIRADO";
                await _db.SaveChangesAsync();
            }
            return (false, $"Vale-Crédito expirado em {vale.DataValidade:dd/MM/yyyy}. Revalidação requer autorização de supervisor.", 0m);
        }

        if (vale.SaldoDisponivel <= 0 || vale.Status == "UTILIZADO")
        {
            var dataUso = vale.DataUtilizacaoTotal?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy");
            return (false, $"Vale-Crédito já liquidado integralmente em {dataUso}.", 0m);
        }

        // Abatimento atômico
        var valorAbatido = Math.Min(vale.SaldoDisponivel, valorNecessario);
        vale.SaldoDisponivel -= valorAbatido;

        if (vale.SaldoDisponivel == 0)
        {
            vale.Status = "UTILIZADO";
            vale.DataUtilizacaoTotal = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Vale-Crédito '{Codigo}' resgatado na Venda #{Venda}. Abatido: R$ {Abatido:N2}, Saldo restante: R$ {Restante:N2}",
            vale.Codigo, vendaDestinoId, valorAbatido, vale.SaldoDisponivel);

        var mensagem = $"Vale-crédito aplicado com sucesso. Abatido: R$ {valorAbatido:N2}. Saldo restante no vale: R$ {vale.SaldoDisponivel:N2}.";
        return (true, mensagem, valorAbatido);
    }

    public async Task<ValeCredito?> ConsultarValeAsync(string codigoVale)
    {
        if (string.IsNullOrWhiteSpace(codigoVale)) return null;

        var codigoFormatado = codigoVale.Trim().ToUpperInvariant();
        var vale = await _db.ValesCredito.FirstOrDefaultAsync(v => v.Codigo.ToUpper() == codigoFormatado);

        if (vale != null && vale.Status == "ATIVO" && vale.DataValidade.Date < DateTime.Today)
        {
            vale.Status = "EXPIRADO";
            await _db.SaveChangesAsync();
        }

        return vale;
    }

    private static string GerarCodigoValeUnico()
    {
        var buffer = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        var hex = Convert.ToHexString(buffer).ToUpperInvariant();
        return $"VALE-{DateTime.Now:yyyy}-{hex}";
    }
}
