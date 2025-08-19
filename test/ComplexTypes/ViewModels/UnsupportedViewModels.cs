using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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

    // Non-generic collections
    [ObservableProperty]
    private ArrayList arrayList = new();

    [ObservableProperty]
    private Hashtable hashtable = new();

    [ObservableProperty]
    private Queue queue = new();

    [ObservableProperty]
    private Stack stack = new();

    [ObservableProperty]
    private SortedList sortedList = new();

    [ObservableProperty]
    private IEnumerable enumerable = new ArrayList();

    [ObservableProperty]
    private ICollection collection = new ArrayList();

    [ObservableProperty]
    private IList list = new ArrayList();

    [ObservableProperty]
    private IDictionary dictionary = new Hashtable();

    // Specialized collections
    [ObservableProperty]
    private NameValueCollection nameValueCollection = new();

    [ObservableProperty]
    private StringCollection stringCollection = new();

    [ObservableProperty]
    private StringDictionary stringDictionary = new();

    [ObservableProperty]
    private HybridDictionary hybridDictionary = new();

    [ObservableProperty]
    private OrderedDictionary orderedDictionary = new();

    [ObservableProperty]
    private BitVector32 bitVector = new();
}
