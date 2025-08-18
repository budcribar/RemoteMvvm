using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComplexTypes.ViewModels;

public enum Mode
{
    Off = 0,
    On = 1
}

public partial class FourthLevel : ObservableObject
{
    [ObservableProperty]
    private double measurement;
}

public partial class ThirdLevel : ObservableObject
{
    [ObservableProperty]
    private FourthLevel[] series;

    [ObservableProperty]
    private List<FourthLevel> items = new();
}

public partial class SecondLevel : ObservableObject
{
    [ObservableProperty]
    private Dictionary<Mode, ThirdLevel[]> modeMap = new();

    [ObservableProperty]
    private Dictionary<string, List<ThirdLevel>> namedGroups = new();
}

