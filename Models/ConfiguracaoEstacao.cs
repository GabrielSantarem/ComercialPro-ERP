using System;

namespace GetStartedApp.Models;

public class ConfiguracaoEstacao
{
    public string ModoEstacao { get; set; } = "GERENCIAL"; // GERENCIAL, CAIXA, BALCAO
    public string NomeTerminal { get; set; } = "TERMINAL-01";
    public string SenhaMaster { get; set; } = "admin";
    public bool BloquearSaidaSemSenha { get; set; } = true;
}
