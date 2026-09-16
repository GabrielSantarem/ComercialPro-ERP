using System;
using System.Collections.Generic;

namespace GetStartedApp.Models;

public class Vendedor
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
}

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
}

public class Venda
{
    public int Id { get; set; }
    public int VendedorId { get; set; }
    public Vendedor Vendedor { get; set; } = null!;
    public DateTime DataHora { get; set; }
    public decimal ValorTotal { get; set; }

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
