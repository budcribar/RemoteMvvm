using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace GrpcRemoteMvvmModelUtil
{
    /// <summary>
    /// Represents information about an observable property in a ViewModel.
    /// </summary>
    /// <param name="Name">The property name.</param>
    /// <param name="TypeString">The property type as a string.</param>
    /// <param name="FullTypeSymbol">The full type symbol from Roslyn analysis.</param>
    /// <param name="IsReadOnly">Whether the property lacks a public setter.</param>
    public record PropertyInfo(string Name, string TypeString, ITypeSymbol FullTypeSymbol, bool IsReadOnly = false);
    
    /// <summary>
    /// Represents information about a relay command in a ViewModel.
    /// </summary>
    /// <param name="MethodName">The name of the method that implements the command.</param>
    /// <param name="CommandPropertyName">The name of the generated command property.</param>
    /// <param name="Parameters">The parameters of the command method.</param>
    /// <param name="IsAsync">Whether the command method is asynchronous.</param>
    public record CommandInfo(string MethodName, string CommandPropertyName, List<ParameterInfo> Parameters, bool IsAsync);
    
    /// <summary>
    /// Represents information about a parameter in a command method.
    /// </summary>
    /// <param name="Name">The parameter name.</param>
    /// <param name="TypeString">The parameter type as a string.</param>
    /// <param name="FullTypeSymbol">The full type symbol from Roslyn analysis.</param>
    public record ParameterInfo(string Name, string TypeString, ITypeSymbol FullTypeSymbol);
}
