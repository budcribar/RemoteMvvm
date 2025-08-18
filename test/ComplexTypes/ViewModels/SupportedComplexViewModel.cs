using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComplexTypes.ViewModels;

public partial class SupportedComplexViewModel : ObservableObject
{
    [ObservableProperty]
    private Dictionary<int, SecondLevel> layers = new();
}
