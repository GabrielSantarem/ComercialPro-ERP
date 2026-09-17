using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GetStartedApp.ViewModels;

public partial class ParcelaFinanceiroVm : ObservableObject
{
    public int Numero { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public string Documento { get; set; } = string.Empty;
}
