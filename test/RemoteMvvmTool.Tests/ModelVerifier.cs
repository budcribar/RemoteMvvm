using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RemoteMvvmTool.Tests;

// Shared test interfaces (moved from TestContext)
public interface ITestClient : IDisposable
{
    Task InitializeAsync();
    Task<string> GetModelDataAsync();
    Task<string> GetStructuralStateAsync();
    Task UpdatePropertyAsync(string propertyName, object value);
    Task UpdateIndexedPropertyAsync(string collectionName, int index, string propertyName, object value);
}

public interface IIndexedUpdater
{
    Task UpdatePropertyAsync(string propertyName, object value);
}

public sealed class IndexedUpdater : IIndexedUpdater
{
    private readonly ITestClient _client; private readonly string _collection; private readonly int _index;
    public IndexedUpdater(ITestClient client, string collection, int index){ _client = client; _collection = collection; _index = index; }
    public Task UpdatePropertyAsync(string propertyName, object value)
        => _client.UpdateIndexedPropertyAsync(_collection, _index, propertyName, value);
}

// Model verification helpers
public static class ModelVerifier
{
    public static async Task VerifyModelAsync(ITestClient client, string expectedData, string context)
    {
        var actual = await client.GetModelDataAsync();
        VerifyModelData(actual, expectedData, context);
        Console.WriteLine($"? {context}: Expected=[{expectedData}], Actual=[{actual}]");
    }

    public static async Task VerifyModelStructuralAsync(ITestClient client, string expectedNumericValues, string context)
    {
        var snapshot = await client.GetStructuralStateAsync();
        VerifyNumbersContained(snapshot, expectedNumericValues, context);
        Console.WriteLine($"? {context} (structural): Verified numeric tokens present");
    }

    public static async Task VerifyModelContainsAllDistinctAsync(ITestClient client, string expectedData, string context)
    {
        var actual = await client.GetModelDataAsync();
        VerifyModelDistinct(actual, expectedData, context);
        Console.WriteLine($"? {context} (distinct coverage)");
    }

    public static void VerifyModelData(string actualData, string expectedData, string context)
    {
        if (string.IsNullOrWhiteSpace(actualData) && string.IsNullOrWhiteSpace(expectedData)) return;
        if (string.IsNullOrWhiteSpace(actualData) || string.IsNullOrWhiteSpace(expectedData))
            throw new Exception($"[{context}] Expected [{expectedData}] but actual was [{actualData}]");
        var actualNumbers = actualData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Parse).OrderBy(x => x).ToArray();
        var expectedNumbers = expectedData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Parse).OrderBy(x => x).ToArray();
        if (!actualNumbers.SequenceEqual(expectedNumbers))
            throw new Exception($"[{context}] MISMATCH\nExpected: {expectedData}\nActual:   {actualData}");
        static double Parse(string s) => double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void VerifyNumbersContained(string structuralSnapshot, string expectedNumericValues, string context)
    {
        if (string.IsNullOrWhiteSpace(expectedNumericValues)) return;
        var raw = structuralSnapshot ?? string.Empty;
        var numberPattern = Regex.Matches(raw, @"-?[0-9]+(?:\.[0-9]+)?");
        var actualNumbers = new List<double>();
        foreach (var m in numberPattern.Cast<Match>())
            if (double.TryParse(m.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                actualNumbers.Add(d);
        bool ContainsToken(string token)
        {
            if (raw.Contains(token)) return true;
            if (!token.Contains('.') && raw.Contains(token + ".0")) return true;
            return false;
        }
        var missing = new List<string>();
        foreach (var token in expectedNumericValues.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Distinct())
        {
            if (token.Length == 0) continue;
            if (ContainsToken(token)) continue;
            if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var expectedVal))
            {
                double relTol = Math.Max(Math.Abs(expectedVal) * 1e-9, 1e-9);
                double absTol = 1e-6;
                if (actualNumbers.Any(a => Math.Abs(a - expectedVal) <= Math.Max(relTol, absTol)))
                    continue;
            }
            missing.Add(token);
        }
        if (missing.Count > 0)
        {
            string snippet = raw.Length <= 800 ? raw : raw.Substring(0, 800) + "...";
            throw new Exception($"[{context}] Structural verification failed. Missing: {string.Join(",", missing)}\nSnapshot (trunc): {snippet}");
        }
    }

    private static void VerifyModelDistinct(string actualData, string expectedData, string context)
    {
        if (string.IsNullOrWhiteSpace(expectedData)) return;
        if (string.IsNullOrWhiteSpace(actualData))
            throw new Exception($"[{context}] Actual data empty");
        var actualSet = actualData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet();
        var expectedSet = expectedData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet();
        var missing = expectedSet.Where(e => !actualSet.Contains(e)).ToList();
        if (missing.Count > 0)
            throw new Exception($"[{context}] Missing distinct tokens: {string.Join(",", missing.Take(25))}{(missing.Count > 25 ? "..." : "")}");
    }
}
