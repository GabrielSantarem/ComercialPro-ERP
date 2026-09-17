using CommunityToolkit.Mvvm.ComponentModel;
using GetStartedApp.Models;

namespace GetStartedApp.ViewModels;

public partial class ItemEntradaTemp : ObservableObject
{
    public Produto Produto { get; set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustoTotal))]
    public partial int Quantidade { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustoTotal))]
    public partial decimal CustoUnitario { get; set; }

    public decimal CustoTotal => Quantidade * CustoUnitario;
}
