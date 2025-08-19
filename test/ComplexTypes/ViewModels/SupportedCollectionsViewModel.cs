using System.Collections.Generic;
using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComplexTypes.ViewModels;

public partial class SupportedCollectionsViewModel : ObservableObject
{
    [ObservableProperty]
    private HashSet<string> tags = new();

    [ObservableProperty]
    private ConcurrentDictionary<string, int> counts = new();

    [ObservableProperty]
    private Queue<int> numbers = new();
}
