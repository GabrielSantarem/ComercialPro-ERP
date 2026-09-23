using System;

namespace GetStartedApp.Models;

public class ConfiguracaoTerminal
{
    public int Id { get; set; } = 1;
    public string NomeEstacao { get; set; } = "Terminal Caixa 01";
    public string ModeloImpressora { get; set; } = "EPSON TM-T20X";
    public string LarguraBobina { get; set; } = "80mm"; // "80mm" ou "58mm"
    public string PortaComunicacao { get; set; } = "USB / Spooler";
    public bool CortarPapelAutomatico { get; set; } = true;
    public string ModoImpressaoPadrao { get; set; } = "Cupom Térmico NFC-e"; // "Cupom Térmico NFC-e", "DANFE Simplificado A4", "Apenas Visualizar"
    public bool ImprimirComandaBalcaoAutomatico { get; set; } = true;
    public bool IntegracaoBalancaHabilitada { get; set; } = false;
    public string ModeloBalanca { get; set; } = "Toledo Prix 3";

    // Automação Comercial, Hardware & Etiquetas (REV-004)
    public string ModoBalancaEtiqueta { get; set; } = "ValorTotal"; // "ValorTotal" ou "PesoLiquido"
    public bool AcionarGavetaAutomaticamente { get; set; } = true;
    public bool UsarEmuladorBalanca { get; set; } = true;
    public int TamanhoCodigoBalanca { get; set; } = 4;
}
