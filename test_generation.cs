using System;
using System.Collections.Generic;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class TestGeneratedCode
{
    static void Main()
    {
        // Create a simple compilation to get type symbols
        var compilation = CSharpCompilation.Create("Test",
            new[] { CSharpSyntaxTree.ParseText(@"
                using System.Collections.ObjectModel;
                using System.ComponentModel;
                
                namespace Test 
                {
                    public class ThermalZoneComponentViewModel : INotifyPropertyChanged
                    {
                        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
                        public int Temperature { get; set; }
                    }
                }")
            },
            references: new[] { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.ObjectModel.ObservableCollection<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location)
            });

        // Find the ThermalZoneComponentViewModel type
        var thermalZoneType = compilation.GetTypeByMetadataName("Test.ThermalZoneComponentViewModel");
        
        // Create ObservableCollection<ThermalZoneComponentViewModel> type
        var observableCollectionType = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ObservableCollection`1")
            ?.Construct(thermalZoneType);

        var properties = new List<PropertyInfo>
        {
            new PropertyInfo("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", observableCollectionType!)
        };

        var generated = ViewModelPartialGenerator.Generate(
            "TestViewModel", 
            "Test.Protos", 
            "TestViewModelService", 
            "Test.ViewModels", 
            "Test.Clients", 
            "ObservableObject", 
            "wpf", 
            true, 
            properties);

        Console.WriteLine("=== Generated Code ===");
        Console.WriteLine(generated);
        
        // Check for auto-generated content
        if (generated.Contains("// Auto-generated nested property change handlers for ZoneList"))
        {
            Console.WriteLine("\n? SUCCESS: Auto-generated nested property change handlers found!");
        }
        else
        {
            Console.WriteLine("\n? FAILED: No auto-generated nested property change handlers found.");
        }
    }
}