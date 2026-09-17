using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public class ResumoFinanceiroDto
{
    public decimal TotalPendente { get; set; }
    public decimal TotalVencido { get; set; }
    public decimal TotalPagoNoMes { get; set; }
    public int QuantidadeVencidos { get; set; }
    public int QuantidadePendentes { get; set; }
}

public class ResumoFluxoCaixaDto
{
    // A Pagar
    public decimal TotalPagarPendente { get; set; }
    public decimal TotalPagarVencido { get; set; }
    public decimal TotalPagoMes { get; set; }

    // A Receber
    public decimal TotalReceberPendente { get; set; }
    public decimal TotalReceberVencido { get; set; }
    public decimal TotalRecebidoMes { get; set; }

    // Projeção & Realizado
    public decimal SaldoProjetado => TotalReceberPendente - TotalPagarPendente;
    public decimal SaldoRealizadoMes => TotalRecebidoMes - TotalPagoMes;
    public int QtdInadimplentes => TotalReceberVencido > 0 ? 1 : 0;
}

public partial class PdvService
{
    // === 1. GESTÃO DE CONTAS A PAGAR ===

    public async Task<List<ContaPagar>> ObterContasPagarAsync(string? statusFiltro = null, DateTime? inicio = null, DateTime? fim = null)
    {
        var query = _db.ContasPagar
            .Include(c => c.EntradaMercadoria)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFiltro) && !statusFiltro.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
        {
            if (statusFiltro.Equals("VENCIDOS", StringComparison.OrdinalIgnoreCase))
            {
                var hoje = DateTime.Today;
                query = query.Where(c => c.Status == "PENDENTE" && c.DataVencimento < hoje);
            }
            else
            {
                query = query.Where(c => c.Status == statusFiltro.ToUpper());
            }
        }

        if (inicio.HasValue)
        {
            var dataIni = inicio.Value.Date;
            query = query.Where(c => c.DataVencimento >= dataIni);
        }

        if (fim.HasValue)
        {
            var dataFim = fim.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(c => c.DataVencimento <= dataFim);
        }

        return await query.OrderBy(c => c.DataVencimento).ToListAsync();
    }

    public async Task<ContaPagar> RegistrarContaPagarManualAsync(
        string fornecedorNome,
        string fornecedorCnpj,
        string numeroDocumento,
        string numeroParcela,
        decimal valor,
        DateTime dataVencimento,
        string observacao = "")
    {
        if (string.IsNullOrWhiteSpace(fornecedorNome))
        {
            throw new ArgumentException("O nome do fornecedor é obrigatório.", nameof(fornecedorNome));
        }

        if (valor <= 0)
        {
            throw new ArgumentException("O valor da conta a pagar deve ser maior que zero.", nameof(valor));
        }

        var conta = new ContaPagar
        {
            FornecedorNome = fornecedorNome.Trim(),
            FornecedorCnpj = fornecedorCnpj?.Trim() ?? string.Empty,
            NumeroDocumento = string.IsNullOrWhiteSpace(numeroDocumento) ? "DOC-AVULSO" : numeroDocumento.Trim(),
            NumeroParcela = string.IsNullOrWhiteSpace(numeroParcela) ? "1/1" : numeroParcela.Trim(),
            Valor = valor,
            DataEmissao = DateTime.Today,
            DataVencimento = dataVencimento.Date,
            Status = "PENDENTE",
            Observacao = observacao
        };

        _db.ContasPagar.Add(conta);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Conta a Pagar #{Id} criada para '{Fornecedor}'. Vcto: {Vcto:dd/MM/yyyy}, Valor: R$ {Valor:N2}",
            conta.Id, conta.FornecedorNome, conta.DataVencimento, conta.Valor);

        return conta;
    }

    public async Task LiquidarContaPagarAsync(int contaPagarId, decimal valorPago, string formaPagamento, string observacao = "")
    {
        if (valorPago <= 0)
        {
            throw new ArgumentException("O valor pago deve ser maior que zero.", nameof(valorPago));
        }

        var conta = await _db.ContasPagar.FindAsync(contaPagarId);
        if (conta == null)
        {
            throw new InvalidOperationException("Título a pagar não encontrado.");
        }

        if (conta.Status == "PAGO")
        {
            throw new InvalidOperationException($"O título #{conta.Id} já foi liquidado em {conta.DataPagamento:dd/MM/yyyy}.");
        }

        if (conta.Status == "CANCELADO")
        {
            throw new InvalidOperationException($"Não é possível liquidar o título #{conta.Id} pois ele está CANCELADO.");
        }

        conta.Status = "PAGO";
        conta.DataPagamento = DateTime.Now;
        conta.ValorPago = valorPago;
        conta.FormaPagamento = string.IsNullOrWhiteSpace(formaPagamento) ? "Boleto" : formaPagamento.Trim();
        if (!string.IsNullOrWhiteSpace(observacao))
        {
            conta.Observacao = string.IsNullOrWhiteSpace(conta.Observacao)
                ? observacao
                : $"{conta.Observacao} | {observacao}";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Título a Pagar #{Id} ({Doc} - {Forn}) liquidado. Valor Pago: R$ {Valor:N2} via {Forma}",
            conta.Id, conta.NumeroDocumento, conta.FornecedorNome, valorPago, conta.FormaPagamento);
    }

    public async Task CancelarContaPagarAsync(int contaPagarId, string motivo = "")
    {
        var conta = await _db.ContasPagar.FindAsync(contaPagarId);
        if (conta == null)
        {
            throw new InvalidOperationException("Título a pagar não encontrado.");
        }

        if (conta.Status == "PAGO")
        {
            throw new InvalidOperationException("Não é possível cancelar um título que já foi PAGO.");
        }

        conta.Status = "CANCELADO";
        if (!string.IsNullOrWhiteSpace(motivo))
        {
            conta.Observacao = string.IsNullOrWhiteSpace(conta.Observacao)
                ? $"Cancelado: {motivo}"
                : $"{conta.Observacao} | Cancelado: {motivo}";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Título a Pagar #{Id} cancelado. Motivo: {Motivo}", conta.Id, motivo);
    }

    public async Task<ResumoFinanceiroDto> ObterResumoFinanceiroContasPagarAsync()
    {
        var hoje = DateTime.Today;
        var primeiroDiaMes = new DateTime(hoje.Year, hoje.Month, 1);
        var ultimoDiaMes = primeiroDiaMes.AddMonths(1).AddDays(-1);

        var todosTitulos = await _db.ContasPagar.ToListAsync();

        var pendentes = todosTitulos.Where(c => c.Status == "PENDENTE").ToList();
        var vencidos = pendentes.Where(c => c.DataVencimento < hoje).ToList();
        var pagosNoMes = todosTitulos
            .Where(c => c.Status == "PAGO" && c.DataPagamento.HasValue && c.DataPagamento.Value.Date >= primeiroDiaMes && c.DataPagamento.Value.Date <= ultimoDiaMes)
            .ToList();

        return new ResumoFinanceiroDto
        {
            TotalPendente = pendentes.Sum(c => c.Valor),
            TotalVencido = vencidos.Sum(c => c.Valor),
            TotalPagoNoMes = pagosNoMes.Sum(c => c.ValorPago ?? c.Valor),
            QuantidadePendentes = pendentes.Count,
            QuantidadeVencidos = vencidos.Count
        };
    }

    // === 2. GESTÃO DE CONTAS A RECEBER & CREDIÁRIO ("FIADO") ===

    public async Task<List<ContaReceber>> ObterContasReceberAsync(
        string? statusFiltro = null, 
        string? termoBusca = null, 
        DateTime? inicio = null, 
        DateTime? fim = null)
    {
        var query = _db.ContasReceber.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFiltro) && !statusFiltro.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
        {
            if (statusFiltro.Equals("VENCIDOS", StringComparison.OrdinalIgnoreCase))
            {
                var hoje = DateTime.Today;
                query = query.Where(c => c.Status == "PENDENTE" && c.DataVencimento < hoje);
            }
            else
            {
                query = query.Where(c => c.Status == statusFiltro.ToUpper());
            }
        }

        if (!string.IsNullOrWhiteSpace(termoBusca))
        {
            var termo = termoBusca.Trim().ToLower();
            query = query.Where(c => c.ClienteNome.ToLower().Contains(termo) 
                                  || c.ClienteCpfCnpj.Contains(termo) 
                                  || c.NumeroDocumento.ToLower().Contains(termo));
        }

        if (inicio.HasValue)
        {
            var dataIni = inicio.Value.Date;
            query = query.Where(c => c.DataVencimento >= dataIni);
        }

        if (fim.HasValue)
        {
            var dataFim = fim.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(c => c.DataVencimento <= dataFim);
        }

        return await query.OrderBy(c => c.DataVencimento).ToListAsync();
    }

    public async Task<ContaReceber> RegistrarContaReceberManualAsync(
        string clienteNome,
        string clienteCpfCnpj,
        string clienteTelefone,
        string numeroDocumento,
        string numeroParcela,
        decimal valor,
        DateTime dataVencimento,
        string observacao = "",
        int? vendaId = null,
        int? pedidoBalcaoId = null)
    {
        if (string.IsNullOrWhiteSpace(clienteNome))
        {
            throw new ArgumentException("O nome do cliente é obrigatório para crediário.", nameof(clienteNome));
        }

        if (valor <= 0)
        {
            throw new ArgumentException("O valor a receber deve ser maior que zero.", nameof(valor));
        }

        var titulo = new ContaReceber
        {
            ClienteNome = clienteNome.Trim(),
            ClienteCpfCnpj = clienteCpfCnpj?.Trim() ?? string.Empty,
            ClienteTelefone = clienteTelefone?.Trim() ?? string.Empty,
            NumeroDocumento = string.IsNullOrWhiteSpace(numeroDocumento) ? "CREDIARIO" : numeroDocumento.Trim(),
            NumeroParcela = string.IsNullOrWhiteSpace(numeroParcela) ? "1/1" : numeroParcela.Trim(),
            ValorOriginal = valor,
            DataEmissao = DateTime.Today,
            DataVencimento = dataVencimento.Date,
            Status = "PENDENTE",
            Observacao = observacao,
            VendaId = vendaId,
            PedidoBalcaoId = pedidoBalcaoId
        };

        _db.ContasReceber.Add(titulo);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Conta a Receber #{Id} criada para '{Cliente}'. Vcto: {Vcto:dd/MM/yyyy}, Valor: R$ {Valor:N2}",
            titulo.Id, titulo.ClienteNome, titulo.DataVencimento, titulo.ValorOriginal);

        return titulo;
    }

    public async Task LiquidarContaReceberAsync(
        int contaReceberId,
        decimal valorRecebido,
        decimal juros = 0m,
        decimal desconto = 0m,
        string formaRecebimento = "Dinheiro",
        string observacao = "")
    {
        if (valorRecebido <= 0)
        {
            throw new ArgumentException("O valor recebido deve ser maior que zero.", nameof(valorRecebido));
        }

        var conta = await _db.ContasReceber.FindAsync(contaReceberId);
        if (conta == null)
        {
            throw new InvalidOperationException("Título a receber não encontrado.");
        }

        if (conta.Status == "RECEBIDO")
        {
            throw new InvalidOperationException($"O título #{conta.Id} já foi recebido em {conta.DataRecebimento:dd/MM/yyyy}.");
        }

        if (conta.Status == "CANCELADO")
        {
            throw new InvalidOperationException($"Não é possível receber o título #{conta.Id} pois ele está CANCELADO.");
        }

        conta.JurosMulta = Math.Max(0, juros);
        conta.Desconto = Math.Max(0, desconto);
        conta.ValorRecebido = valorRecebido;
        conta.DataRecebimento = DateTime.Now;
        conta.FormaRecebimento = string.IsNullOrWhiteSpace(formaRecebimento) ? "Dinheiro" : formaRecebimento.Trim();
        conta.Status = "RECEBIDO";

        if (!string.IsNullOrWhiteSpace(observacao))
        {
            conta.Observacao = string.IsNullOrWhiteSpace(conta.Observacao)
                ? observacao
                : $"{conta.Observacao} | {observacao}";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Título a Receber #{Id} ({Doc} - {Cliente}) liquidado. Valor Recebido: R$ {Valor:N2} via {Forma}",
            conta.Id, conta.NumeroDocumento, conta.ClienteNome, valorRecebido, conta.FormaRecebimento);
    }

    public async Task CancelarContaReceberAsync(int contaReceberId, string motivo = "")
    {
        var conta = await _db.ContasReceber.FindAsync(contaReceberId);
        if (conta == null)
        {
            throw new InvalidOperationException("Título a receber não encontrado.");
        }

        if (conta.Status == "RECEBIDO")
        {
            throw new InvalidOperationException("Não é possível cancelar um título que já foi RECEBIDO.");
        }

        conta.Status = "CANCELADO";
        if (!string.IsNullOrWhiteSpace(motivo))
        {
            conta.Observacao = string.IsNullOrWhiteSpace(conta.Observacao)
                ? $"Cancelado: {motivo}"
                : $"{conta.Observacao} | Cancelado: {motivo}";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Título a Receber #{Id} cancelado. Motivo: {Motivo}", conta.Id, motivo);
    }

    // === 3. FLUXO DE CAIXA CONSOLIDADO ===

    public async Task<ResumoFluxoCaixaDto> ObterResumoFluxoCaixaAsync()
    {
        var hoje = DateTime.Today;
        var primeiroDiaMes = new DateTime(hoje.Year, hoje.Month, 1);
        var ultimoDiaMes = primeiroDiaMes.AddMonths(1).AddDays(-1);

        // A Pagar
        var todosPagar = await _db.ContasPagar.ToListAsync();
        var pagarPendentes = todosPagar.Where(c => c.Status == "PENDENTE").ToList();
        var pagarVencidos = pagarPendentes.Where(c => c.DataVencimento < hoje).ToList();
        var pagosMes = todosPagar
            .Where(c => c.Status == "PAGO" && c.DataPagamento.HasValue && c.DataPagamento.Value.Date >= primeiroDiaMes && c.DataPagamento.Value.Date <= ultimoDiaMes)
            .ToList();

        // A Receber
        var todosReceber = await _db.ContasReceber.ToListAsync();
        var receberPendentes = todosReceber.Where(c => c.Status == "PENDENTE").ToList();
        var receberVencidos = receberPendentes.Where(c => c.DataVencimento < hoje).ToList();
        var recebidosMes = todosReceber
            .Where(c => c.Status == "RECEBIDO" && c.DataRecebimento.HasValue && c.DataRecebimento.Value.Date >= primeiroDiaMes && c.DataRecebimento.Value.Date <= ultimoDiaMes)
            .ToList();

        return new ResumoFluxoCaixaDto
        {
            TotalPagarPendente = pagarPendentes.Sum(c => c.Valor),
            TotalPagarVencido = pagarVencidos.Sum(c => c.Valor),
            TotalPagoMes = pagosMes.Sum(c => c.ValorPago ?? c.Valor),

            TotalReceberPendente = receberPendentes.Sum(c => c.ValorFinal),
            TotalReceberVencido = receberVencidos.Sum(c => c.ValorFinal),
            TotalRecebidoMes = recebidosMes.Sum(c => c.ValorRecebido ?? c.ValorFinal)
        };
    }
}
