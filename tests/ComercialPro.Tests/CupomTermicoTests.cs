using System;
using System.Collections.Generic;
using GetStartedApp.Models;
using GetStartedApp.Services.Impressao;
using Xunit;

namespace GetStartedApp.Tests;

public class CupomTermicoTests
{
    private readonly CupomTermicoService _cupomService = new();

    private PedidoBalcao CriarPedidoExemplo()
    {
        var pedido = new PedidoBalcao
        {
            Id = 1,
            NumeroComanda = "#PED-0042",
            DataHora = new DateTime(2026, 9, 17, 18, 30, 0),
            ClienteNome = "Carlos Eduardo",
            ClienteCpf = "111.222.333-44",
            Vendedor = new Vendedor { Id = 2, Nome = "Lucas Vendedor" },
            Status = "PENDENTE",
            ValorTotal = 75.50m
        };

        pedido.Itens.Add(new ItemPedidoBalcao
        {
            Id = 1,
            Produto = new Produto { Id = 10, Nome = "Sacola Reforçada 2k" },
            Quantidade = 10,
            PrecoUnitario = 2.50m
        });

        pedido.Itens.Add(new ItemPedidoBalcao
        {
            Id = 2,
            Produto = new Produto { Id = 20, Nome = "Copo Descartável 200ml" },
            Quantidade = 1,
            PrecoUnitario = 50.50m
        });

        return pedido;
    }

    [Fact]
    public void Deve_Gerar_Cupom_80mm_Com_Cabecalho_Itens_E_Total_Formatados()
    {
        var pedido = CriarPedidoExemplo();

        var cupom = _cupomService.GerarCupomComandaTexto(pedido, largura: "80mm");

        Assert.NotEmpty(cupom);
        Assert.Contains("#PED-0042", cupom);
        Assert.Contains("LUCAS VENDEDOR", cupom);
        Assert.Contains("CARLOS EDUARDO", cupom);
        Assert.Contains("111.222.333-44", cupom);
        Assert.Contains("Sacola Reforçada 2k", cupom);
        Assert.Contains("R$ 75,50", cupom);
        Assert.Contains("DIRIJA-SE AO CAIXA CENTRAL", cupom);

        // Verifica que nenhuma linha excede 48 colunas
        var linhas = cupom.Split(Environment.NewLine);
        foreach (var linha in linhas)
        {
            Assert.True(linha.Length <= CupomTermicoService.LARGURA_80MM, 
                $"Linha excedeu 48 colunas: '{linha}' (Tamanho: {linha.Length})");
        }
    }

    [Fact]
    public void Deve_Gerar_Cupom_58mm_Sem_Ultrapassar_Largura_De_32_Colunas()
    {
        var pedido = CriarPedidoExemplo();

        var cupom = _cupomService.GerarCupomComandaTexto(pedido, largura: "58mm");

        Assert.NotEmpty(cupom);
        Assert.Contains("#PED-0042", cupom);
        Assert.Contains("R$ 75,50", cupom);

        // Verifica que nenhuma linha excede 32 colunas
        var linhas = cupom.Split(Environment.NewLine);
        foreach (var linha in linhas)
        {
            Assert.True(linha.Length <= CupomTermicoService.LARGURA_58MM, 
                $"Linha excedeu 32 colunas: '{linha}' (Tamanho: {linha.Length})");
        }
    }

    [Fact]
    public void Deve_Omitir_Cpf_Quando_Nao_Informado()
    {
        var pedido = CriarPedidoExemplo();
        pedido.ClienteCpf = string.Empty;

        var cupom = _cupomService.GerarCupomComandaTexto(pedido, largura: "80mm");

        Assert.DoesNotContain("CPF      :", cupom);
    }
}
