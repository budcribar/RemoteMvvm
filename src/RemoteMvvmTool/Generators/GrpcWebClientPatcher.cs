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
            
        var content = File.ReadAllText(filePath);
        
        // Check if already patched to avoid duplicate processing
        if (content.Contains("console.error") && content.Contains("RPC error:"))
        {
            return; // Already patched
        }
        
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

        // Process methods - use reverse iteration to avoid index shifting issues
        var processedMethods = new HashSet<string>();
        string currentMethod = string.Empty;
        string? responseType = null;
        
        // Collect method information first
        var methodInfos = new List<(int lineIndex, string method, string responseType)>();
        
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
            
            if (line.Trim() == "if (callback !== undefined) {" && 
                !string.IsNullOrEmpty(currentMethod) && 
                !string.IsNullOrEmpty(responseType) &&
                !processedMethods.Contains(currentMethod))
            {
                methodInfos.Add((i, currentMethod, responseType));
                processedMethods.Add(currentMethod);
            }
        }
        
        // Process methods in reverse order to maintain line indices
        for (int m = methodInfos.Count - 1; m >= 0; m--)
        {
            var (lineIndex, method, respType) = methodInfos[m];
            ProcessCallbackMethod(lines, lineIndex, method, respType, className);
        }
        
        // Process streaming methods
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("return this.client_.serverStreaming("))
            {
                ProcessStreamingMethod(lines, i, className);
            }
        }

        File.WriteAllLines(filePath, lines);
    }

    private static void ProcessCallbackMethod(List<string> lines, int callbackIndex, string method, string responseType, string className)
    {
        var wrapper = new List<string>
        {
            "      const wrappedCallback = (err: grpcWeb.RpcError,",
            $"                               response: {responseType}) => {{",
            "        if (err) {",
            $"          console.error('{className}.{method} RPC error:', err);",
            "        }",
            "        callback(err, response);",
            "      };"
        };
        
        lines.InsertRange(callbackIndex + 1, wrapper);
        
        // Update callback reference
        int searchStart = callbackIndex + 1 + wrapper.Count;
        for (int j = searchStart; j < Math.Min(searchStart + 20, lines.Count); j++)
        {
            if (lines[j].TrimEnd().EndsWith("callback);"))
            {
                lines[j] = lines[j].Replace("callback);", "wrappedCallback);");
                break;
            }
        }
        
        // Add promise error handling
        for (int j = searchStart; j < Math.Min(searchStart + 30, lines.Count); j++)
        {
            if (lines[j].TrimStart().StartsWith("return this.client_.unaryCall"))
            {
                for (int k = j; k < Math.Min(j + 10, lines.Count); k++)
                {
                    if (lines[k].Contains("this.methodDescriptor") && !lines[k].Contains(".catch("))
                    {
                        var indent = Regex.Match(lines[k], @"^\s*").Value;
                        var descriptor = lines[k].Trim().TrimEnd(')', ';');
                        lines[k] = indent + descriptor + ").catch((err: any) => {";
                        lines.Insert(k + 1, indent + $"  console.error('{className}.{method} Promise error:', err);");
                        lines.Insert(k + 2, indent + "  throw err;");
                        lines.Insert(k + 3, indent + "});");
                        break;
                    }
                }
                break;
            }
        }
    }

    private static void ProcessStreamingMethod(List<string> lines, int streamIndex, string className)
    {
        var indent = Regex.Match(lines[streamIndex], @"^\s*").Value;
        lines[streamIndex] = indent + "const stream = this.client_.serverStreaming(";
        
        for (int j = streamIndex + 1; j < Math.Min(streamIndex + 10, lines.Count); j++)
        {
            if (lines[j].Contains("this.methodDescriptor"))
            {
                var indent2 = Regex.Match(lines[j], @"^\s*").Value;
                var descriptor = lines[j].Trim().TrimEnd(')', ';');
                lines[j] = indent2 + descriptor + ");";
                lines.Insert(j + 1, indent + "if (stream && typeof (stream as any).on === 'function') {");
                lines.Insert(j + 2, indent + "  (stream as any).on('error', (err: any) => {");
                lines.Insert(j + 3, indent + $"    console.error('Stream error:', err);");
                lines.Insert(j + 4, indent + "  });");
                lines.Insert(j + 5, indent + "}");
                lines.Insert(j + 6, indent + "return stream;");
                break;
            }
        }
    }
}
