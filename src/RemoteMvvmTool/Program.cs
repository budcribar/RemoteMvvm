using System;
using System.IO;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var generateOption = new Option<string>("--generate", () => "all", "Comma separated list of outputs: proto,server,client,ts");
        var outputOption = new Option<string>("--output", () => "generated", "Output directory");
        var vmArgument = new Argument<List<string>>("viewmodels", "ViewModel .cs files") { Arity = ArgumentArity.OneOrMore };

        var root = new RootCommand("RemoteMvvm generation tool");
        root.AddOption(generateOption);
        root.AddOption(outputOption);
        root.AddArgument(vmArgument);

        root.SetHandler(async (generate, output, vms) =>
        {
            var gens = generate.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool genProto = gens.Contains("proto") || gens.Contains("all");
            bool genServer = gens.Contains("server") || gens.Contains("all");
            bool genClient = gens.Contains("client") || gens.Contains("all");
            bool genTs = gens.Contains("ts") || gens.Contains("all") || gens.Contains("tsclient");

            Directory.CreateDirectory(output);

            var result = await ViewModelAnalyzer.AnalyzeAsync(
                vms,
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute",
                new List<string>());

            if (result.ViewModelSymbol == null)
            {
                Console.Error.WriteLine("No ViewModel with [GenerateGrpcRemote] found.");
                return;
            }

            var protoNamespace = result.ViewModelSymbol.GetAttributes()
                .First(a => a.AttributeClass?.Name.Contains("GenerateGrpcRemote") == true)
                .ConstructorArguments[0].Value?.ToString() ?? "Generated.Protos";
            var serviceName = result.ViewModelSymbol.GetAttributes()
                .First(a => a.AttributeClass?.Name.Contains("GenerateGrpcRemote") == true)
                .ConstructorArguments[1].Value?.ToString() ?? result.ViewModelName + "Service";

            if (genProto)
            {
                var proto = Generators.GenerateProto(protoNamespace, serviceName, result.ViewModelName, result.Properties, result.Commands, result.Compilation);
                await File.WriteAllTextAsync(Path.Combine(output, serviceName + ".proto"), proto);
            }
            if (genTs)
            {
                var ts = Generators.GenerateTypeScriptClient(result.ViewModelName, protoNamespace, serviceName, result.Properties, result.Commands);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + "RemoteClient.ts"), ts);
            }
            if (genServer)
            {
                var server = Generators.GenerateServer(result.ViewModelName, protoNamespace, serviceName, result.Properties, result.Commands);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + "GrpcServiceImpl.cs"), server);
            }
            if (genClient)
            {
                var client = Generators.GenerateClient(result.ViewModelName, protoNamespace, serviceName, result.Properties, result.Commands);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + "RemoteClient.cs"), client);
            }
        }, generateOption, outputOption, vmArgument);

        return await root.InvokeAsync(args);
    }
}
