using System;
using System.Collections.Generic;

namespace GetStartedApp.Models;

public class EntradaMercadoria
{
    public int Id { get; set; }
    public string NumeroNota { get; set; } = string.Empty;
    public string Fornecedor { get; set; } = string.Empty;
    public DateTime DataEntrada { get; set; } = DateTime.Now;
    public string Observacao { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }

    public List<ItemEntradaMercadoria> Itens { get; set; } = [];
}

public class ItemEntradaMercadoria
{
    public int Id { get; set; }
    public int EntradaMercadoriaId { get; set; }
    public EntradaMercadoria EntradaMercadoria { get; set; } = null!;
    
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    
    public int QuantidadeEntrada { get; set; }
    public decimal CustoUnitario { get; set; }
    public decimal CustoTotal => QuantidadeEntrada * CustoUnitario;
}
