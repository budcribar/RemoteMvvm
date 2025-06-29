using System;
using System.IO;

namespace GrpcRemoteMvvmTsClientGen
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: grpc-remote-mvvm-ts-client <input.proto> <outputDir>");
                return;
            }

            var protoFile = args[0];
            var outputDir = args[1];

            if (!File.Exists(protoFile))
            {
                Console.WriteLine($"File not found: {protoFile}");
                return;
            }

            Directory.CreateDirectory(outputDir);

            // Call protoc to generate TypeScript client using ts-proto
            var tsProtoPlugin = "protoc-gen-ts_proto"; // Assumes ts-proto is installed and in PATH
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "protoc",
                    Arguments = $"--plugin=protoc-gen-ts_proto={tsProtoPlugin} --ts_proto_out={outputDir} --ts_proto_opt=outputServices=grpc-web {protoFile}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
                Console.Error.WriteLine(error);

            if (process.ExitCode == 0)
                Console.WriteLine("TypeScript client generated successfully.");
            else
                Console.WriteLine("Failed to generate TypeScript client.");
        }
    }
}