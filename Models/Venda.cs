using System;
using System.Collections.Generic;

namespace GetStartedApp.Models;

public class Venda
{
    public int Id { get; set; }
    public int VendedorId { get; set; }
    public Vendedor Vendedor { get; set; } = null!;
    public DateTime DataHora { get; set; }
    public decimal ValorTotal { get; set; }
    
    // Assinatura de cadeia antifraude (Blockchain local SHA-256)
    public string HashSeguranca { get; set; } = string.Empty;

    public List<ItemVenda> Itens { get; set; } = [];
}

public class ItemVenda
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}
