using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GeneratedTests;

public partial class TestViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _preciseValue;

    [ObservableProperty]
    private char _unicodeChar;

    [ObservableProperty]
    private Guid _emptyGuid = Guid.Empty;

    [ObservableProperty]
    private Half _halfValue;

    public enum SampleEnum { A, B, C }
    public class NestedType { public int Value { get; set; } }
}