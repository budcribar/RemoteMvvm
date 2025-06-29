using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace GrpcRemoteMvvmModelUtil
{
    public record PropertyInfo(string Name, string TypeString, ITypeSymbol FullTypeSymbol);
    public record CommandInfo(string MethodName, string CommandPropertyName, List<ParameterInfo> Parameters, bool IsAsync);
    public record ParameterInfo(string Name, string TypeString, ITypeSymbol FullTypeSymbol);
}
