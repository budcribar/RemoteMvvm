using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RemoteMvvmTool.Generators;

public static class GrpcWebClientPatcher
{
    public static void AddErrorLogging(string filePath)
    {
        if (!File.Exists(filePath))
            return;
        var lines = new List<string>(File.ReadAllLines(filePath));
        string? className = null;
        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"export class (\w+)");
            if (m.Success)
            {
                className = m.Groups[1].Value;
                break;
            }
        }
        if (className == null)
            return;
        string currentMethod = string.Empty;
        string? responseType = null;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var methodMatch = Regex.Match(line, @"^\s*(\w+)\($");
            if (methodMatch.Success)
            {
                currentMethod = methodMatch.Groups[1].Value;
                responseType = null;
            }
            if (line.Contains("callback: (err: grpcWeb.RpcError") || line.Contains("callback?: (err: grpcWeb.RpcError"))
            {
                var searchLine = line;
                if (!line.Contains("response:"))
                {
                    if (i + 1 < lines.Count)
                        searchLine = lines[i + 1];
                }
                var rm = Regex.Match(searchLine, @"response: ([^\)\s]+)");
                if (rm.Success)
                    responseType = rm.Groups[1].Value.Trim();
            }
            if (line.Trim() == "if (callback !== undefined) {")
            {
                if (string.IsNullOrEmpty(currentMethod) || string.IsNullOrEmpty(responseType))
                    continue;
                var wrapper = new List<string>
                {
                    "      const wrappedCallback = (err: grpcWeb.RpcError,",
                    $"                               response: {responseType}) => {{",
                    "        if (err) {",
                    $"          console.error('{className}.{currentMethod} RPC error:', err);",
                    "        }",
                    "        callback(err, response);",
                    "      };"
                };
                lines.InsertRange(i + 1, wrapper);
                int searchStart = i + 1 + wrapper.Count;
                for (int j = searchStart; j < lines.Count; j++)
                {
                    if (lines[j].TrimEnd().EndsWith("callback);"))
                    {
                        lines[j] = lines[j].Replace("callback);", "wrappedCallback);");
                        break;
                    }
                }
                for (int j = searchStart; j < lines.Count; j++)
                {
                    if (lines[j].TrimStart().StartsWith("return this.client_.unaryCall"))
                    {
                        for (int k = j; k < lines.Count; k++)
                        {
                            if (lines[k].Contains("this.methodDescriptor"))
                            {
                                var indent = Regex.Match(lines[k], @"^\s*").Value;
                                var descriptor = lines[k].Trim().TrimEnd(')', ';');
                                lines[k] = indent + descriptor + ").catch((err: any) => {";
                                lines.Insert(k + 1, indent + $"  console.error('{className}.{currentMethod} Promise error:', err);");
                                lines.Insert(k + 2, indent + "  throw err;");
                                lines.Insert(k + 3, indent + "});");
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            if (line.TrimStart().StartsWith("return this.client_.serverStreaming("))
            {
                var indent = Regex.Match(line, @"^\s*").Value;
                lines[i] = indent + "const stream = this.client_.serverStreaming(";
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j].Contains("this.methodDescriptor"))
                    {
                        var indent2 = Regex.Match(lines[j], @"^\s*").Value;
                        var descriptor = lines[j].Trim().TrimEnd(')', ';');
                        lines[j] = indent2 + descriptor + ");";
                        lines.Insert(j + 1, indent + "if (stream && typeof (stream as any).on === 'function') {");
                        lines.Insert(j + 2, indent + "  (stream as any).on('error', (err: any) => {");
                        lines.Insert(j + 3, indent + $"    console.error('{className}.{currentMethod} stream error:', err);");
                        lines.Insert(j + 4, indent + "  });");
                        lines.Insert(j + 5, indent + "}");
                        lines.Insert(j + 6, indent + "return stream;");
                        break;
                    }
                }
            }
        }
        File.WriteAllLines(filePath, lines);
    }
}
