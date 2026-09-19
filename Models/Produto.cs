using System;

namespace GetStartedApp.Models;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
    public int EstoqueMinimo { get; set; } = 5;
    
    // Identificadores fiscais e comerciais
    public string? CodigoBarras { get; set; }
    public string? Ncm { get; set; }
    public string? UnidadeMedida { get; set; } = "UN";
    public decimal CustoUltimaCompra { get; set; }
    public DateTime DataCadastro { get; set; } = DateTime.Now;

    public bool EstaAbaixoDoMinimo => Estoque <= EstoqueMinimo;
}
