using System.IO;
using RemoteMvvmTool.Generators;
using Xunit;

namespace ToolExecution;

public class GrpcWebClientPatcherTests
{
    [Fact]
    public void AddErrorLogging_InsertsLoggingStatements()
    {
        var tempFile = Path.GetTempFileName();
        var stub = @"export class TestServiceClient {
  client_: grpcWeb.AbstractClientBase;
  hostname_: string;
  credentials_: null | { [index: string]: string; };
  options_: null | { [index: string]: any; };

  constructor (hostname: string) {
    this.client_ = new grpcWeb.GrpcWebClientBase({});
    this.hostname_ = hostname;
  }

  methodDescriptorFoo = new grpcWeb.MethodDescriptor(
    '/generated_protos.TestService/Foo',
    grpcWeb.MethodType.UNARY,
    google_protobuf_empty_pb.Empty,
    google_protobuf_empty_pb.Empty,
    (request: google_protobuf_empty_pb.Empty) => {
      return request.serializeBinary();
    },
    google_protobuf_empty_pb.Empty.deserializeBinary
  );

  foo(
    request: google_protobuf_empty_pb.Empty,
    metadata?: grpcWeb.Metadata | null): Promise<google_protobuf_empty_pb.Empty>;

  foo(
    request: google_protobuf_empty_pb.Empty,
    metadata: grpcWeb.Metadata | null,
    callback: (err: grpcWeb.RpcError,
               response: google_protobuf_empty_pb.Empty) => void): grpcWeb.ClientReadableStream<google_protobuf_empty_pb.Empty>;

  foo(
    request: google_protobuf_empty_pb.Empty,
    metadata?: grpcWeb.Metadata | null,
    callback?: (err: grpcWeb.RpcError,
               response: google_protobuf_empty_pb.Empty) => void) {
    if (callback !== undefined) {
      return this.client_.rpcCall(
        this.hostname_ +
          '/generated_protos.TestService/Foo',
        request,
        metadata || {},
        this.methodDescriptorFoo,
        callback);
    }
    return this.client_.unaryCall(
    this.hostname_ +
      '/generated_protos.TestService/Foo',
    request,
    metadata || {},
    this.methodDescriptorFoo);
  }

  methodDescriptorBar = new grpcWeb.MethodDescriptor(
    '/generated_protos.TestService/Bar',
    grpcWeb.MethodType.SERVER_STREAMING,
    google_protobuf_empty_pb.Empty,
    google_protobuf_empty_pb.Empty,
    (request: google_protobuf_empty_pb.Empty) => {
      return request.serializeBinary();
    },
    google_protobuf_empty_pb.Empty.deserializeBinary
  );

  bar(
    request: google_protobuf_empty_pb.Empty,
    metadata?: grpcWeb.Metadata): grpcWeb.ClientReadableStream<google_protobuf_empty_pb.Empty> {
    return this.client_.serverStreaming(
      this.hostname_ +
        '/generated_protos.TestService/Bar',
      request,
      metadata || {},
      this.methodDescriptorBar);
  }

}
";
        File.WriteAllText(tempFile, stub);

        GrpcWebClientPatcher.AddErrorLogging(tempFile);
        var updated = File.ReadAllText(tempFile);

        Assert.Contains("TestServiceClient.foo RPC error", updated);
        Assert.Contains("TestServiceClient.foo Promise error", updated);
        Assert.Contains("TestServiceClient.bar stream error", updated);
    }
}
