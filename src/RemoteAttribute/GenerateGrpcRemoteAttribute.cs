
using System;

namespace PeakSWC.Mvvm.Remote; 

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateGrpcRemoteAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the C# namespace where the gRPC types (messages, client, service base)
    /// generated from your .proto file are located.
    /// </summary>
    public string ProtoCSharpNamespace { get; set; }

    /// <summary>
    /// Gets or sets the name of the gRPC service defined in your .proto file.
    /// (e.g., "MyViewModelGrpcService" which leads to MyViewModelGrpcService.MyViewModelGrpcServiceBase)
    /// </summary>
    public string GrpcServiceName { get; set; }

    /// <summary>
    /// Gets or sets the desired namespace for the generated server-side gRPC service implementation.
    /// If null or empty, a default will be used (e.g., [OriginalNamespace].GrpcService).
    /// </summary>
    public string ServerImplNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the desired namespace for the generated client-side proxy ViewModel.
    /// If null or empty, a default will be used (e.g., [OriginalNamespace].RemoteClient).
    /// </summary>
    public string ClientProxyNamespace { get; set; } = string.Empty;


    public GenerateGrpcRemoteAttribute(string protoCSharpNamespace, string grpcServiceName)
    {
        if (string.IsNullOrWhiteSpace(protoCSharpNamespace))
            throw new ArgumentNullException(nameof(protoCSharpNamespace));
        if (string.IsNullOrWhiteSpace(grpcServiceName))
            throw new ArgumentNullException(nameof(grpcServiceName));

        ProtoCSharpNamespace = protoCSharpNamespace;
        GrpcServiceName = grpcServiceName;
    }
}
