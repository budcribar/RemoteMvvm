using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static partial class CsProjectGenerator
{
    // Helper methods for type checking
    private static bool IsCollectionType(string typeString)
    {
        if (string.IsNullOrEmpty(typeString)) return false;
        var lower = typeString.ToLowerInvariant();
        return lower.Contains("observablecollection") || 
               lower.Contains("list<") || 
               lower.Contains("dictionary<") || 
               lower.EndsWith("[]") || 
               lower.Contains("icollection") || 
               lower.Contains("ienumerable") ||
               lower.Contains("collection");
    }

    private static bool IsBool(PropertyInfo prop)
    {
        var typeString = prop.TypeString.ToLowerInvariant();
        return typeString.Contains("bool");
    }

    private static bool IsComplexType(string typeString)
    {
        var lower = typeString.ToLowerInvariant();
        return !lower.Contains("string") && 
               !lower.Contains("int") && 
               !lower.Contains("bool") && 
               !lower.Contains("double") && 
               !lower.Contains("float") && 
               !lower.Contains("decimal") && 
               !lower.Contains("long") && 
               !lower.Contains("byte") && 
               !lower.Contains("guid") && 
               !lower.Contains("datetime") &&
               !IsCollectionType(typeString);
    }

    // Single-project .slnx helper
    public static string GenerateSingleProjectSolutionXml(string projectRelativePath)
        => $"<Solution>\n  <Project Path=\"{projectRelativePath}\" />\n</Solution>";

    public static string GenerateSingleProjectLaunchUser(string projectRelativePath)
    {
        var norm = projectRelativePath.Replace("\\", "/");
        return "[\n  {\n    \"Name\": \"RunSingle\",\n    \"Projects\": [\n      { \"Path\": \"" + norm + "\", \"Action\": \"Start\" }\n    ]\n  }\n]\n";
    }

    public static string GenerateSolutionXml(string serverProjectRelativePath, string clientProjectRelativePath)
        => $"<Solution>\n  <Project Path=\"{serverProjectRelativePath}\" />\n  <Project Path=\"{clientProjectRelativePath}\" />\n</Solution>";
}

