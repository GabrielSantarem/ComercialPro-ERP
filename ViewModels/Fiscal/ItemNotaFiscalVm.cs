using CommunityToolkit.Mvvm.ComponentModel;
using GetStartedApp.Models;

namespace GetStartedApp.ViewModels;

public partial class ItemNotaFiscalVm : ObservableObject
{
    public int NumeroItem { get; set; }
    
    [ObservableProperty]
    public partial string CodigoFornecedor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CodigoEan { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DescricaoFornecedor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Ncm { get; set; } = "0000.00.00";

    [ObservableProperty]
    public partial string Cfop { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnidadeFornecedor { get; set; } = "UN";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuantidadeEstoque))]
    [NotifyPropertyChangedFor(nameof(TotalBruto))]
    [NotifyPropertyChangedFor(nameof(CustoRealTotal))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial int QuantidadeFaturada { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuantidadeEstoque))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial int FatorConversao { get; set; } = 1;

    public int QuantidadeEstoque => QuantidadeFaturada * (FatorConversao > 0 ? FatorConversao : 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalBruto))]
    [NotifyPropertyChangedFor(nameof(CustoRealTotal))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial decimal PrecoUnitarioFaturado { get; set; }

    public decimal TotalBruto => QuantidadeFaturada * PrecoUnitarioFaturado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustoRealTotal))]
    [NotifyPropertyChangedFor(nameof(CustoUnitarioEstoque))]
    public partial decimal RateioDespesas { get; set; }

    public decimal CustoRealTotal => TotalBruto + RateioDespesas;

    public decimal CustoUnitarioEstoque => QuantidadeEstoque > 0 ? CustoRealTotal / QuantidadeEstoque : 0;

    [ObservableProperty]
    public partial Produto? ProdutoVinculado { get; set; }

    public bool IsProdutoNovo => ProdutoVinculado == null;
}
