using System;
using System.Collections.Generic;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

class Program
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
            
        // Find and print the ConvertAnyToTargetType method
        var lines = server.Split('\n');
        bool inConvertMethod = false;
        int contextLines = 0;
        
        Console.WriteLine("=== ConvertAnyToTargetType method ===");
        foreach (var line in lines)
        {
            if (line.Contains("ConvertAnyToTargetType"))
            {
                inConvertMethod = true;
                contextLines = 30;
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
        Console.WriteLine($"Contains UInt64Value.Descriptor: {server.Contains("request.NewValue.Is(UInt64Value.Descriptor)")}");
    }
}