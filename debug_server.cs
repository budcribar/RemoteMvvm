using System;
using System.Collections.Generic;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

class DebugServerGenerator
{
    static void Main()
    {
        var server = ServerGenerator.Generate(
            "SampleViewModel",
            "Generated.Protos",
            "SampleViewModelService",
            new List<PropertyInfo>(),
            new List<CommandInfo>(),
            "Generated.ViewModels");
            
        Console.WriteLine("=== Generated Server Code ===");
        
        // Find the ConvertAnyToTargetType method
        var lines = server.Split('\n');
        bool inConvertMethod = false;
        int contextLines = 0;
        
        foreach (var line in lines)
        {
            if (line.Contains("ConvertAnyToTargetType"))
            {
                inConvertMethod = true;
                contextLines = 30; // Show more context
            }
            
            if (inConvertMethod && contextLines > 0)
            {
                Console.WriteLine(line);
                contextLines--;
            }
            
            if (contextLines <= 0)
                inConvertMethod = false;
        }
        
        Console.WriteLine("\n=== Test Results ===");
        Console.WriteLine($"Contains DoubleValue.Descriptor: {server.Contains("request.NewValue.Is(DoubleValue.Descriptor)")}");
        Console.WriteLine($"Contains FloatValue.Descriptor: {server.Contains("request.NewValue.Is(FloatValue.Descriptor)")}");
        Console.WriteLine($"Contains Int64Value.Descriptor: {server.Contains("request.NewValue.Is(Int64Value.Descriptor)")}");
        Console.WriteLine($"Contains 'case uint': {server.Contains("case uint")}");
    }
}