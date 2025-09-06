using System.Threading.Tasks;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// Extension methods for ITestClient to provide strongly-typed convenience methods
/// </summary>
public static class TestClientExtensions
{
    // Common property update methods for convenience
    public static async Task UpdateTemperatureAsync(this ITestClient client, int value)
        => await client.UpdatePropertyAsync("Temperature", value);
    
    public static async Task UpdateCounterAsync(this ITestClient client, int value)
        => await client.UpdatePropertyAsync("Counter", value);
    
    public static async Task UpdateMessageAsync(this ITestClient client, string value)
        => await client.UpdatePropertyAsync("Message", value);
    
    public static async Task UpdatePlayerLevelAsync(this ITestClient client, int value)
        => await client.UpdatePropertyAsync("PlayerLevel", value);
    
    // HasBonus in some models is numeric/bool – keep generic (test expects numeric extraction)
    public static async Task UpdateHasBonusAsync(this ITestClient client, bool value)
        => await client.UpdatePropertyAsync("HasBonus", value);
    
    public static async Task UpdateBonusMultiplierAsync(this ITestClient client, double value)
        => await client.UpdatePropertyAsync("BonusMultiplier", value);
    
    // Fix: Property is actually 'IsEnabled' in models. Map both helpers to IsEnabled.
    public static async Task UpdateEnabledAsync(this ITestClient client, bool value)
        => await client.UpdatePropertyAsync("IsEnabled", value);
        
    public static async Task UpdateIsEnabledAsync(this ITestClient client, bool value)
        => await client.UpdatePropertyAsync("IsEnabled", value);
    
    // Collection operations (placeholders)
    public static async Task AddToZoneListAsync(this ITestClient client, object item)
        => await Task.CompletedTask;
    
    public static async Task ClearZoneListAsync(this ITestClient client)
        => await Task.CompletedTask;
    
    // Nested property updates
    public static IndexedUpdaterWrapper ZoneList(this ITestClient client, int index)
        => new IndexedUpdaterWrapper(client, "ZoneList", index);
}

/// <summary>
/// Wrapper for indexed property updates with strongly-typed methods
/// </summary>
public class IndexedUpdaterWrapper
{
    private readonly ITestClient _client;
    private readonly string _collectionName;
    private readonly int _index;

    public IndexedUpdaterWrapper(ITestClient client, string collectionName, int index)
    {
        _client = client;
        _collectionName = collectionName;
        _index = index;
    }

    public async Task UpdateTemperatureAsync(int value)
        => await _client.UpdateIndexedPropertyAsync(_collectionName, _index, "Temperature", value);

    public async Task UpdateZoneAsync(int value)
        => await _client.UpdateIndexedPropertyAsync(_collectionName, _index, "Zone", value);

    public async Task UpdatePropertyAsync(string propertyName, object value)
        => await _client.UpdateIndexedPropertyAsync(_collectionName, _index, propertyName, value);
}