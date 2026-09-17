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
    
    // Novo campo antifraude (Blockchain local)
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

// === ENTRADA MANUAL DE MERCADORIAS / NOTAS ===
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

// === CONTROLE DE TURNOS DE CAIXA (ABERTURA, SANGRIA, FECHAMENTO) ===
public class CaixaTurno
{
    public int Id { get; set; }
    public int VendedorId { get; set; }
    public Vendedor Vendedor { get; set; } = null!;
    
    public DateTime DataAbertura { get; set; } = DateTime.Now;
    public DateTime? DataFechamento { get; set; }
    
    public decimal SaldoInicial { get; set; }           // Fundo de Troco
    public decimal TotalVendasDinheiro { get; set; }     // Acumulado de vendas em espécie
    public decimal TotalVendasOutros { get; set; }       // Cartões, PIX, etc.
    public decimal TotalSuprimentos { get; set; }        // Entradas extras de troco
    public decimal TotalSangrias { get; set; }           // Retiradas para cofre
    
    public decimal? SaldoInformado { get; set; }         // Contagem física cega do operador
    public decimal? DiferencaQuebra { get; set; }        // Sobra ou Falta (SaldoInformado - SaldoEsperado)
    
    public string Status { get; set; } = "ABERTO";       // "ABERTO" ou "FECHADO"
    public string Observacao { get; set; } = string.Empty;

    public List<MovimentacaoCaixa> Movimentacoes { get; set; } = [];

    // Saldo esperado em dinheiro físico na gaveta neste momento
    public decimal SaldoEsperadoEmDinheiro => SaldoInicial + TotalVendasDinheiro + TotalSuprimentos - TotalSangrias;
}

public class MovimentacaoCaixa
{
    public int Id { get; set; }
    public int CaixaTurnoId { get; set; }
    public CaixaTurno CaixaTurno { get; set; } = null!;
    
    public DateTime DataHora { get; set; } = DateTime.Now;
    public string Tipo { get; set; } = "SANGRIA"; // "SANGRIA" ou "SUPRIMENTO"
    public decimal Valor { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
