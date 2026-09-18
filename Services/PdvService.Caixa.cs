using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models;

namespace GetStartedApp.Services;

public partial class PdvService
{
    // === CONTROLE DE TURNOS DE CAIXA (ABERTURA, SANGRIA, FECHAMENTO) ===

    public async Task<CaixaTurno?> ObterTurnoAtualAsync()
    {
        return await _db.CaixasTurno
            .Include(c => c.Vendedor)
            .Include(c => c.Movimentacoes)
            .FirstOrDefaultAsync(c => c.Status == "ABERTO");
    }

    public async Task<CaixaTurno> AbrirCaixaAsync(int vendedorId, decimal saldoInicial, string observacao = "")
    {
        var existente = await _db.CaixasTurno.FirstOrDefaultAsync(c => c.Status == "ABERTO");
        if (existente != null)
        {
            throw new InvalidOperationException($"Já existe um turno de caixa aberto (Turno #{existente.Id}). Feche o turno atual antes de abrir outro.");
        }

        if (saldoInicial < 0)
        {
            throw new ArgumentException("O fundo de troco inicial não pode ser negativo.", nameof(saldoInicial));
        }

        var turno = new CaixaTurno
        {
            VendedorId = vendedorId,
            DataAbertura = DateTime.Now,
            SaldoInicial = saldoInicial,
            Status = "ABERTO",
            Observacao = observacao
        };

        _db.CaixasTurno.Add(turno);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Turno de Caixa #{Id} aberto com saldo inicial de R$ {Saldo:N2} por Vendedor {VendedorId}", turno.Id, saldoInicial, vendedorId);
        return turno;
    }

    public async Task<MovimentacaoCaixa> RegistrarSuprimentoAsync(int caixaTurnoId, decimal valor, string motivo)
    {
        if (valor <= 0) throw new ArgumentException("O valor do suprimento deve ser maior que zero.", nameof(valor));

        var turno = await _db.CaixasTurno.FindAsync(caixaTurnoId);
        if (turno == null || turno.Status != "ABERTO")
        {
            throw new InvalidOperationException("O turno informado não existe ou não está aberto.");
        }

        turno.TotalSuprimentos += valor;

        var mov = new MovimentacaoCaixa
        {
            CaixaTurnoId = caixaTurnoId,
            DataHora = DateTime.Now,
            Tipo = "SUPRIMENTO",
            Valor = valor,
            Motivo = motivo
        };

        _db.MovimentacoesCaixa.Add(mov);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Suprimento de R$ {Valor:N2} registrado no Caixa #{Id}. Motivo: {Motivo}", valor, caixaTurnoId, motivo);
        return mov;
    }

    public async Task<MovimentacaoCaixa> RegistrarSangriaAsync(int caixaTurnoId, decimal valor, string motivo)
    {
        if (valor <= 0) throw new ArgumentException("O valor da sangria deve ser maior que zero.", nameof(valor));

        var turno = await _db.CaixasTurno.FindAsync(caixaTurnoId);
        if (turno == null || turno.Status != "ABERTO")
        {
            throw new InvalidOperationException("O turno informado não existe ou não está aberto.");
        }

        if (valor > turno.SaldoEsperadoEmDinheiro)
        {
            throw new InvalidOperationException($"Sangria não permitida! Valor solicitado (R$ {valor:N2}) é maior do que o saldo físico disponível na gaveta (R$ {turno.SaldoEsperadoEmDinheiro:N2}).");
        }

        turno.TotalSangrias += valor;

        var mov = new MovimentacaoCaixa
        {
            CaixaTurnoId = caixaTurnoId,
            DataHora = DateTime.Now,
            Tipo = "SANGRIA",
            Valor = valor,
            Motivo = motivo
        };

        _db.MovimentacoesCaixa.Add(mov);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Sangria de R$ {Valor:N2} registrada no Caixa #{Id}. Motivo: {Motivo}", valor, caixaTurnoId, motivo);
        return mov;
    }

    public async Task<CaixaTurno> FecharCaixaAsync(int caixaTurnoId, decimal saldoInformado, string observacao = "")
    {
        if (saldoInformado < 0)
        {
            throw new ArgumentException("O saldo físico informado não pode ser negativo.", nameof(saldoInformado));
        }

        var turno = await _db.CaixasTurno
            .Include(c => c.Vendedor)
            .Include(c => c.Movimentacoes)
            .FirstOrDefaultAsync(c => c.Id == caixaTurnoId);

        if (turno == null || turno.Status != "ABERTO")
        {
            throw new InvalidOperationException("O turno informado não existe ou já foi fechado.");
        }

        turno.DataFechamento = DateTime.Now;
        turno.SaldoInformado = saldoInformado;
        turno.DiferencaQuebra = saldoInformado - turno.SaldoEsperadoEmDinheiro;
        turno.Status = "FECHADO";
        if (!string.IsNullOrWhiteSpace(observacao))
        {
            turno.Observacao = string.IsNullOrWhiteSpace(turno.Observacao) 
                ? observacao 
                : $"{turno.Observacao} | {observacao}";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Caixa #{Id} fechado. Esperado: R$ {Esperado:N2}, Informado: R$ {Informado:N2}, Diferença: R$ {Dif:N2}",
            turno.Id, turno.SaldoEsperadoEmDinheiro, saldoInformado, turno.DiferencaQuebra);

        // Módulo 3: Gatilho automático de Backup Resiliente no fechamento de turno
        if (_backupService != null)
        {
            try
            {
                _logger.LogInformation("Disparando backup automático SQLite após fechamento do Turno #{Id}", turno.Id);
                await _backupService.ExecutarBackupAsync($"FechamentoTurno_{turno.Id}");
                await _backupService.LimparBackupsAntigosAsync(30);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no backup automático pós-fechamento do turno #{Id}", turno.Id);
            }
        }

        return turno;
    }

    public async Task<List<CaixaTurno>> ObterHistoricoTurnosAsync()
    {
        return await _db.CaixasTurno
            .Include(c => c.Vendedor)
            .Include(c => c.Movimentacoes)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();
    }
}
