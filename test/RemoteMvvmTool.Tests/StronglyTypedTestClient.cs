using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using System.Net.Http;
using System.Net;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// Base class for strongly-typed test clients that provide compile-time safe property updates
/// </summary>
public abstract class StronglyTypedTestClientBase : IDisposable
{
    protected GrpcChannel _channel;
    protected readonly string _serverAddress;
    protected readonly int _port;
    
    protected StronglyTypedTestClientBase(string serverAddress, int port)
    {
        _serverAddress = serverAddress;
        _port = port;
        InitializeChannel();
    }

    private void InitializeChannel()
    {
        var address = new Uri($"https://{_serverAddress}:{_port}/");
        
        // Use HTTPS with certificate validation bypass for testing
        var httpsHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = httpsHandler });
    }

    /// <summary>
    /// Gets the current state of the model and extracts numeric values for validation
    /// </summary>
    public abstract Task<string> GetModelDataAsync();

    /// <summary>
    /// Initializes the remote connection
    /// </summary>
    public abstract Task InitializeAsync();

    public virtual void Dispose()
    {
        _channel?.Dispose();
    }
}

/// <summary>
/// Interface for collections that support indexed property updates
/// </summary>
public interface IIndexedPropertyUpdater
{
    Task UpdateTemperatureAsync(int value);
    // Add other common properties as needed
}