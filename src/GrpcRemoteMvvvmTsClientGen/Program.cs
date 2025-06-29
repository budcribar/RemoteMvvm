using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;

namespace GrpcRemoteMvvmTsClientGen
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: grpc-remote-mvvm-ts-client <inputViewModel.cs> <outputDir>");
                return;
            }

            var viewModelFile = args[0];
            var outputDir = args[1];

            if (!File.Exists(viewModelFile))
            {
                Console.WriteLine($"File not found: {viewModelFile}");
                return;
            }

            Directory.CreateDirectory(outputDir);

            // Use the shared analyzer to load the ViewModel
            var (vmSymbol, vmName, properties, commands, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { viewModelFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute",
                Array.Empty<string>()
            );

            if (vmSymbol == null)
            {
                Console.WriteLine("No ViewModel with [GenerateGrpcRemote] attribute found.");
                return;
            }

            // Generate TypeScript client code
            var tsCode = GenerateTypeScriptClient(vmName, properties, commands);
            var outFile = Path.Combine(outputDir, $"{vmName}RemoteClient.ts");
            await File.WriteAllTextAsync(outFile, tsCode);
            Console.WriteLine($"TypeScript client generated: {outFile}");
        }

        static string GenerateTypeScriptClient(string vmName, System.Collections.Generic.List<GrpcRemoteMvvmModelUtil.PropertyInfo> properties, System.Collections.Generic.List<GrpcRemoteMvvmModelUtil.CommandInfo> commands)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"// Auto-generated TypeScript client for {vmName}");
            sb.AppendLine($"export class {vmName}RemoteClient {{");
            foreach (var prop in properties)
            {
                sb.AppendLine($"    {ToCamelCase(prop.Name)}: any;");
            }
            sb.AppendLine();
            foreach (var cmd in commands)
            {
                var paramList = string.Join(", ", cmd.Parameters.Select(p => ToCamelCase(p.Name) + ": any"));
                sb.AppendLine($"    async {ToCamelCase(cmd.MethodName)}({paramList}): Promise<any> {{");
                sb.AppendLine($"        // TODO: Implement gRPC call for {cmd.MethodName}");
                sb.AppendLine($"    }}");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0])) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }
    }
}