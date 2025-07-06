using System;
using System.IO;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

public class Program
{
    static List<string> LoadDefaultRefs()
    {
        var list = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
        }
        return list;
    }

    public static async Task<int> Main(string[] args)
    {
        var generateOption = new Option<string>("--generate", () => "all", "Comma separated list of outputs: proto,server,client,ts");
        var outputOption = new Option<string>("--output", () => "generated", "Output directory for generated code files");
        var protoOutputOption = new Option<string>("--protoOutput", () => "protos", "Directory for generated .proto file");
        var vmArgument = new Argument<List<string>>("viewmodels", "ViewModel .cs files") { Arity = ArgumentArity.OneOrMore };
        var protoNsOption = new Option<string>("--protoNamespace", () => "Generated.Protos", "C# namespace for generated proto types");
        var serviceNameOption = new Option<string?>("--serviceName", description: "gRPC service name");
        var clientNsOption = new Option<string?>("--clientNamespace", description: "Namespace for generated client proxy");

        var root = new RootCommand("RemoteMvvm generation tool");
        root.AddOption(generateOption);
        root.AddOption(outputOption);
        root.AddArgument(vmArgument);
        root.AddOption(protoNsOption);
        root.AddOption(protoOutputOption);
        root.AddOption(serviceNameOption);
        root.AddOption(clientNsOption);

        root.SetHandler(async (generate, output, protoOutput, vms, protoNs, serviceNameOpt, clientNsOpt) =>
        {
            var gens = generate.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool genProto = gens.Contains("proto") || gens.Contains("all");
            bool genServer = gens.Contains("server") || gens.Contains("all");
            bool genClient = gens.Contains("client") || gens.Contains("all");
            bool genTs = gens.Contains("ts") || gens.Contains("all") || gens.Contains("tsclient");

            Directory.CreateDirectory(output);
            Directory.CreateDirectory(protoOutput);

            var refs = LoadDefaultRefs();

            var result = await ViewModelAnalyzer.AnalyzeAsync(
                vms,
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                refs,
                "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");

            if (result.ViewModelSymbol == null)
            {
                Console.Error.WriteLine("No ViewModel subclass of ObservableObject found.");
                return;
            }

            var protoNamespace = protoNs;
            var serviceName = string.IsNullOrWhiteSpace(serviceNameOpt) ? result.ViewModelName + "Service" : serviceNameOpt;
            var clientNamespace = string.IsNullOrWhiteSpace(clientNsOpt)
                ? result.ViewModelSymbol.ContainingNamespace.ToDisplayString() + ".RemoteClients"
                : clientNsOpt;

            if (genProto)
            {
                var proto = ProtoGenerator.Generate(protoNamespace, serviceName, result.ViewModelName, result.Properties, result.Commands, result.Compilation);
                await File.WriteAllTextAsync(Path.Combine(protoOutput, serviceName + ".proto"), proto);
            }
            if (genTs)
            {
                var ts = TypeScriptClientGenerator.Generate(result.ViewModelName, protoNamespace, serviceName, result.Properties, result.Commands);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + "RemoteClient.ts"), ts);
            }
            string vmNamespaceStr = result.ViewModelSymbol?.ContainingNamespace.ToDisplayString() ?? string.Empty;
            if (genServer)
            {
                var server = ServerGenerator.Generate(result.ViewModelName, protoNamespace, serviceName, result.Properties, result.Commands, vmNamespaceStr);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + "GrpcServiceImpl.cs"), server);
            }
            if (genClient)
            {
                var client = ClientGenerator.Generate(result.ViewModelName, protoNamespace, serviceName, result.Properties, result.Commands, clientNamespace);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + "RemoteClient.cs"), client);
            }
            if (genServer || genClient)
            {
                var opts = OptionsGenerator.Generate();
                await File.WriteAllTextAsync(Path.Combine(output, "GrpcRemoteOptions.cs"), opts);
                var partial = ViewModelPartialGenerator.Generate(result.ViewModelName, protoNamespace, serviceName, vmNamespaceStr, clientNamespace);
                await File.WriteAllTextAsync(Path.Combine(output, result.ViewModelName + ".Remote.g.cs"), partial);
            }
        }, generateOption, outputOption, protoOutputOption, vmArgument, protoNsOption, serviceNameOption, clientNsOption);

        return await root.InvokeAsync(args);
    }
}
