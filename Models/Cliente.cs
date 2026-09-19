using System;

namespace GetStartedApp.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? CpfCnpj { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Endereco { get; set; }
    
    public decimal LimiteCredito { get; set; } = 0m;
    public bool BloqueadoManualmente { get; set; } = false;
    public bool Bloqueado { get => BloqueadoManualmente; set => BloqueadoManualmente = value; }
    public string? MotivoBloqueio { get; set; }
    public string StatusFormatado => BloqueadoManualmente ? "BLOQUEADO" : "ATIVO";
    
    public DateTime DataCadastro { get; set; } = DateTime.Now;
}

public record StatusCreditoClienteDto(
    int ClienteId,
    string Nome,
    decimal LimiteTotal,
    decimal SaldoDevedor,
    decimal LimiteDisponivel,
    int QuantidadeTitulosVencidos,
    bool AptoParaCrediario,
    string? MotivoRestricao);
