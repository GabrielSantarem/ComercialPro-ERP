using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public partial class PdvService
{
    // === GESTÃO DE PRÉ-VENDA / TERMINAL DE BALCÃO ===

    public async Task<PedidoBalcao> CriarPedidoBalcaoAsync(
        int vendedorId, 
        string clienteNome, 
        string clienteCpf, 
        IEnumerable<(Produto Produto, int Quantidade)> itens)
    {
        var itensList = itens.ToList();
        if (itensList.Count == 0)
        {
            throw new InvalidOperationException("Não é possível gerar um pedido de balcão sem itens.");
        }

        foreach (var item in itensList)
        {
            if (item.Quantidade <= 0)
            {
                throw new ArgumentException($"A quantidade do produto '{item.Produto.Nome}' deve ser maior que zero.", nameof(itens));
            }
        }

        var valorTotal = itensList.Sum(x => x.Quantidade * x.Produto.Preco);

        var pedido = new PedidoBalcao
        {
            VendedorId = vendedorId,
            ClienteNome = string.IsNullOrWhiteSpace(clienteNome) ? "Cliente Balcão" : clienteNome.Trim(),
            ClienteCpf = clienteCpf?.Trim() ?? string.Empty,
            DataHora = DateTime.Now,
            ValorTotal = valorTotal,
            Status = "AGUARDANDO_PAGAMENTO"
        };

        foreach (var item in itensList)
        {
            pedido.Itens.Add(new ItemPedidoBalcao
            {
                ProdutoId = item.Produto.Id,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.Produto.Preco
            });
        }

        _db.PedidosBalcao.Add(pedido);
        await _db.SaveChangesAsync();

        // Atribui comanda amigável baseada no ID autoincrementado
        pedido.NumeroComanda = $"PED-{pedido.Id:D4}";
        await _db.SaveChangesAsync();

        _logger.LogInformation("Pré-Venda {Comanda} criada por Vendedor {VendedorId} para {Cliente}. Total: R$ {Total:N2}",
            pedido.NumeroComanda, vendedorId, pedido.ClienteNome, pedido.ValorTotal);

        return pedido;
    }

    public async Task<List<PedidoBalcao>> ObterPedidosAguardandoPagamentoAsync()
    {
        return await _db.PedidosBalcao
            .Include(p => p.Vendedor)
            .Include(p => p.Itens)
                .ThenInclude(i => i.Produto)
            .Where(p => p.Status == "AGUARDANDO_PAGAMENTO")
            .OrderBy(p => p.DataHora) // Fila FIFO (o cliente mais antigo primeiro)
            .ToListAsync();
    }

    public async Task CancelarPedidoBalcaoAsync(int pedidoBalcaoId, string motivo = "")
    {
        var pedido = await _db.PedidosBalcao.FindAsync(pedidoBalcaoId);
        if (pedido == null) throw new InvalidOperationException("Pedido não encontrado.");

        if (pedido.Status != "AGUARDANDO_PAGAMENTO")
        {
            throw new InvalidOperationException($"Não é possível cancelar um pedido com status '{pedido.Status}'.");
        }

        pedido.Status = "CANCELADO";
        await _db.SaveChangesAsync();
        _logger.LogInformation("Pedido {Comanda} cancelado. Motivo: {Motivo}", pedido.NumeroComanda, motivo);
    }

    public async Task<Venda> FaturarPedidoBalcaoNoCaixaAsync(
        int pedidoBalcaoId, 
        string formaPagamento,
        IEnumerable<(string Forma, decimal Valor)>? pagamentosDetalhados = null)
    {
        var turnoAtivo = await _db.CaixasTurno.FirstOrDefaultAsync(c => c.Status == "ABERTO");
        if (turnoAtivo == null)
        {
            throw new InvalidOperationException("Não é possível faturar pedidos no caixa sem um turno de caixa aberto.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var pedido = await _db.PedidosBalcao
                .Include(p => p.Itens)
                    .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == pedidoBalcaoId);

            if (pedido == null)
            {
                throw new InvalidOperationException("Pedido não encontrado.");
            }

            if (pedido.Status != "AGUARDANDO_PAGAMENTO")
            {
                throw new InvalidOperationException($"O pedido {pedido.NumeroComanda} não está aguardando pagamento (Status: {pedido.Status}).");
            }

            // Cria a Venda fiscal definitiva
            var venda = new Venda
            {
                VendedorId = pedido.VendedorId,
                DataHora = DateTime.Now,
                ValorTotal = pedido.ValorTotal
            };
            _db.Vendas.Add(venda);
            await _db.SaveChangesAsync();

            // Lança itens na venda e baixa o estoque físico
            foreach (var item in pedido.Itens)
            {
                var itemVenda = new ItemVenda
                {
                    VendaId = venda.Id,
                    ProdutoId = item.ProdutoId,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = item.PrecoUnitario
                };
                _db.ItensVenda.Add(itemVenda);

                var produtoDb = await _db.Produtos.FindAsync(item.ProdutoId);
                if (produtoDb != null)
                {
                    produtoDb.Estoque -= item.Quantidade;
                }
            }

            // Atualiza status do pedido de balcão
            pedido.Status = "FATURADO";
            pedido.VendaId = venda.Id;

            // Acumula o valor no turno de caixa aberto considerando split payments
            var parcelas = pagamentosDetalhados?.ToList();
            if (parcelas != null && parcelas.Count > 0)
            {
                var valorDinheiro = parcelas
                    .Where(p => p.Forma.Contains("Dinheiro", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Valor);
                var valorOutros = parcelas
                    .Where(p => !p.Forma.Contains("Dinheiro", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Valor);

                turnoAtivo.TotalVendasDinheiro += valorDinheiro;
                turnoAtivo.TotalVendasOutros += valorOutros;
            }
            else
            {
                if (formaPagamento.Contains("Dinheiro", StringComparison.OrdinalIgnoreCase))
                {
                    turnoAtivo.TotalVendasDinheiro += venda.ValorTotal;
                }
                else
                {
                    turnoAtivo.TotalVendasOutros += venda.ValorTotal;
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Pedido {Comanda} faturado com sucesso no Caixa #{TurnoId}. Venda #{VendaId} gerada.",
                pedido.NumeroComanda, turnoAtivo.Id, venda.Id);
            return venda;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erro ao faturar pedido de balcão #{Id}", pedidoBalcaoId);
            throw;
        }
    }
}
