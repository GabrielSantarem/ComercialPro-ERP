using System;

namespace GetStartedApp.Models;

public class ValeCredito
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // Ex: VALE-2026-XXXXX
    public decimal ValorOriginal { get; set; }
    public decimal SaldoDisponivel { get; set; }
    
    public int? VendaOrigemId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }
    public string? ClienteCpf { get; set; }
    
    public DateTime DataEmissao { get; set; } = DateTime.Now;
    public DateTime DataValidade { get; set; } = DateTime.Now.AddDays(30);
    public DateTime? DataUtilizacaoTotal { get; set; }
    
    public string Status { get; set; } = "ATIVO"; // ATIVO, UTILIZADO, EXPIRADO, CANCELADO
    public string MotivoDevolucao { get; set; } = string.Empty;
}

public record ItemDevolucaoDto(int ProdutoId, int Quantidade, decimal PrecoUnitario, bool DestinarAvaria, string Motivo);
