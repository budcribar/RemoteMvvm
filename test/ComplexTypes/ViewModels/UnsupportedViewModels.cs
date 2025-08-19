using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComplexTypes.ViewModels;

public partial class UnsupportedTypesViewModel : ObservableObject
{
    [ObservableProperty]
    private Dictionary<DateTime, string> schedule = new();

    [ObservableProperty]
    private Dictionary<object, int> randomMap = new();

    [ObservableProperty]
    private Tuple<int, string> coordinates = new(0, string.Empty);
}
