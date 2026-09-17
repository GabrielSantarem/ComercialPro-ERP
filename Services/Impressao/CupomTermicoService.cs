using System;
using System.Collections.Generic;
using System.Text;
using GetStartedApp.Models;

namespace GetStartedApp.Services.Impressao;

public class CupomTermicoService
{
    public const int LARGURA_80MM = 48; // Padrão ESC/POS 80mm (Fonte A)
    public const int LARGURA_58MM = 32; // Padrão ESC/POS 58mm (Fonte A)

    public string GerarCupomComandaTexto(
        PedidoBalcao pedido, 
        string nomeFantasia = "COMERCIAL PRO ERP & PDV", 
        string largura = "80mm")
    {
        int colunas = largura.Equals("58mm", StringComparison.OrdinalIgnoreCase) 
            ? LARGURA_58MM 
            : LARGURA_80MM;

        var sb = new StringBuilder();

        // 1. Cabeçalho do Estabelecimento
        sb.AppendLine(Centralizar("================================", colunas));
        sb.AppendLine(Centralizar(nomeFantasia, colunas));
        sb.AppendLine(Centralizar("DISTRIBUIDORA DE EMBALAGENS", colunas));
        sb.AppendLine(Centralizar("Rua do Comércio, 1000 - Centro", colunas));
        sb.AppendLine(Centralizar("CNPJ: 12.345.678/0001-90", colunas));
        sb.AppendLine(Centralizar("================================", colunas));

        // 2. Identificação da Comanda de Balcão
        sb.AppendLine(Centralizar("*** COMPROVANTE DE COMANDA ***", colunas));
        sb.AppendLine(Centralizar("(NÃO É DOCUMENTO FISCAL)", colunas));
        sb.AppendLine();
        sb.AppendLine(Centralizar($">> {pedido.NumeroComanda} <<", colunas));
        sb.AppendLine();

        sb.AppendLine($"DATA/HORA: {pedido.DataHora:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"ATENDENTE: {(pedido.Vendedor != null ? pedido.Vendedor.Nome.ToUpper() : "BALCÃO")}");
        sb.AppendLine($"CLIENTE  : {pedido.ClienteNome.ToUpper()}");
        
        if (!string.IsNullOrWhiteSpace(pedido.ClienteCpf))
        {
            sb.AppendLine($"CPF      : {pedido.ClienteCpf}");
        }

        sb.AppendLine(new string('-', colunas));

        // 3. Cabeçalho de Itens
        if (colunas == LARGURA_80MM)
        {
            sb.AppendLine(FormatarLinhaDupla("ITEM DESCRIÇÃO", "QTD x UNIT    TOTAL", colunas));
        }
        else
        {
            sb.AppendLine(FormatarLinhaDupla("DESCRIÇÃO", "QTDxUNIT TOTAL", colunas));
        }
        sb.AppendLine(new string('-', colunas));

        // 4. Lista de Itens
        int seq = 1;
        foreach (var item in pedido.Itens)
        {
            var nomeProd = item.Produto?.Nome ?? "PRODUTO";
            if (nomeProd.Length > colunas - 2)
            {
                nomeProd = nomeProd.Substring(0, colunas - 5) + "...";
            }

            var linhaQtdPreco = $"{item.Quantidade} un x {item.PrecoUnitario:N2}";
            var linhaSubtotal = $"R$ {item.Total:N2}";

            sb.AppendLine($"{seq:D3} {nomeProd}");
            sb.AppendLine(FormatarLinhaDupla($"    {linhaQtdPreco}", linhaSubtotal, colunas));
            seq++;
        }

        sb.AppendLine(new string('-', colunas));

        // 5. Totais da Comanda
        sb.AppendLine(FormatarLinhaDupla("QTD TOTAL ITENS:", $"{pedido.Itens.Count}", colunas));
        sb.AppendLine(FormatarLinhaDupla("VALOR TOTAL:", $"R$ {pedido.ValorTotal:N2}", colunas));
        sb.AppendLine(new string('=', colunas));

        // 6. Rodapé com Instruções ao Consumidor
        sb.AppendLine(Centralizar("DIRIJA-SE AO CAIXA CENTRAL", colunas));
        sb.AppendLine(Centralizar("APRESENTE ESTA COMANDA", colunas));
        sb.AppendLine(Centralizar("PARA EFETUAR O PAGAMENTO", colunas));
        sb.AppendLine(new string('=', colunas));

        // 7. Representação de Código de Barras ASCII
        sb.AppendLine();
        sb.AppendLine(Centralizar("||| | |||| || ||||| |||| || |||| || ||||", colunas));
        sb.AppendLine(Centralizar(pedido.NumeroComanda, colunas));
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Centralizar(string texto, int largura)
    {
        if (texto.Length >= largura) return texto.Substring(0, largura);
        int espacos = (largura - texto.Length) / 2;
        return texto.PadLeft(texto.Length + espacos).PadRight(largura);
    }

    private static string FormatarLinhaDupla(string esquerda, string direita, int largura)
    {
        int espacoDisponivel = largura - esquerda.Length - direita.Length;
        if (espacoDisponivel < 1)
        {
            // Trunca a esquerda se estourar
            int tamanhoEsquerda = Math.Max(1, largura - direita.Length - 1);
            esquerda = esquerda.Substring(0, tamanhoEsquerda);
            espacoDisponivel = 1;
        }

        return esquerda + new string(' ', espacoDisponivel) + direita;
    }
}
