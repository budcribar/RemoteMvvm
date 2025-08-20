using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComplexTypes.ViewModels;

public partial class SupportedCollectionsViewModel : ObservableObject
{
    // Generic collections
    [ObservableProperty]
    private List<int> intList = new();

    [ObservableProperty]
    private Dictionary<string, int> dictionary = new();

    [ObservableProperty]
    private SortedList<string, int> sortedList = new();

    [ObservableProperty]
    private SortedDictionary<string, int> sortedDictionary = new();

    [ObservableProperty]
    private Queue<int> queue = new();

    [ObservableProperty]
    private Stack<string> stack = new();

    [ObservableProperty]
    private HashSet<string> hashSet = new();

    [ObservableProperty]
    private LinkedList<double> linkedList = new();

    [ObservableProperty]
    private IEnumerable<float> enumerable = new List<float>();

    [ObservableProperty]
    private ICollection<int> collection = new List<int>();

    [ObservableProperty]
    private IList<string> stringList = new List<string>();

    [ObservableProperty]
    private IDictionary<string, int> dictionaryInterface = new Dictionary<string, int>();

    [ObservableProperty]
    private ReadOnlyDictionary<string, int> readOnlyDictionary = new(new Dictionary<string, int>());

    [ObservableProperty]
    private IReadOnlyDictionary<string, int> readOnlyDictionaryInterface = new Dictionary<string, int>();

    // Thread-safe collections
    [ObservableProperty]
    private ConcurrentDictionary<string, int> concurrentDictionary = new();

    [ObservableProperty]
    private ConcurrentQueue<string> concurrentQueue = new();

    [ObservableProperty]
    private ConcurrentStack<int> concurrentStack = new();

    [ObservableProperty]
    private ConcurrentBag<double> concurrentBag = new();

    [ObservableProperty]
    private BlockingCollection<long> blockingCollection = new();

    // Memory-based types
    [ObservableProperty]
    private Memory<byte> memory = Memory<byte>.Empty;

    [ObservableProperty]
    private ReadOnlyMemory<char> readOnlyMemory = ReadOnlyMemory<char>.Empty;
}
