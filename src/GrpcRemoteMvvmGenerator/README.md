# PeakSWC.MvvmSourceGenerator NuGet Package

**Version:** 1.0.2 (as per your `.csproj` in the Canvas)

## Overview

`PeakSWC.MvvmSourceGenerator` is a powerful .NET toolset designed to streamline the development of applications using the Model-View-ViewModel (MVVM) pattern with gRPC for remoting capabilities. It consists of two main components:

1.  **C# Source Generator (`PeakSWC.MvvmSourceGenerator.dll`):**
    * Automatically generates the server-side gRPC service implementation that wraps your existing MVVM ViewModel.
    * Automatically generates a client-side proxy ViewModel that communicates with the gRPC service, allowing your client application (e.g., WPF, MAUI) to interact with the remote ViewModel seamlessly.
    * Works by analyzing ViewModels decorated with the `[GenerateGrpcRemote]` attribute.

2.  **Proto File Generation Utility (`ProtoGeneratorUtil.exe`):**
    * A command-line tool, bundled with this NuGet package, that analyzes your C# ViewModel files.
    * Generates the necessary `.proto` file definition based on the ViewModel's observable properties and relay commands.
    * This `.proto` file is then used by `Grpc.Tools` to generate the base gRPC C# classes (messages, service base, client stub).
    * This utility is executed as a pre-build step when configured in the consuming project.

This combination allows you to define your ViewModel once in C# and have the gRPC contract, server implementation, and client proxy largely auto-generated, significantly reducing boilerplate and keeping your remote interface in sync with your ViewModel.

## Prerequisites

* .NET SDK (compatible with .NET Standard 2.0 for the generator, and .NET 8.0 for the bundled `ProtoGeneratorUtil.exe` and the consuming project if following the examples).
* NuGet package manager.
* For projects using the generated code:
    * `Google.Protobuf`
    * `Grpc.Tools`
    * `Grpc.Net.Client` (for clients)
    * `Grpc.Core.Api` (for servers using `Grpc.Core.Server`) or `Grpc.AspNetCore.Server` (for ASP.NET Core hosted servers)
    * `CommunityToolkit.Mvvm` (for `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`)

## Installation

Install the `PeakSWC.MvvmSourceGenerator` NuGet package into your main project (e.g., your WPF application project like `MonsterClicker`) where your ViewModels are defined, or into a shared library project.

```bash
dotnet add package PeakSWC.MvvmSourceGenerator --version 1.0.2
or via Visual Studio NuGet Package Manager.This package is a development dependency and includes an analyzer (the source generator) and build targets for the proto generation utility.How to UseThe workflow involves three main parts:Decorating your ViewModel.Configuring your project to run ProtoGeneratorUtil.exe to generate the .proto file.Configuring Grpc.Tools to compile the generated .proto file.Using the source-generated server implementation and client proxy.1. Decorate Your ViewModelIn your ViewModel C# file (e.g., GameViewModel.cs):Add using PeakSWC.Mvvm.Remote; (assuming this is the namespace of your attribute).Decorate your ViewModel class with the [GenerateGrpcRemote] attribute.Use [ObservableProperty] and [RelayCommand] from CommunityToolkit.Mvvm as usual.Example GameViewModel.cs:using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeakSWC.Mvvm.Remote; // Your attribute's namespace
using System;
using System.Threading.Tasks;

namespace MyProject.ViewModels
{
    [GenerateGrpcRemote(
        protoCsNamespace: "MyProject.ViewModels.Protos", // C# namespace for Grpc.Tools generated types
        grpcServiceName: "GameViewModelService",       // Service name in the .proto file
        ServerImplNamespace = "MyProject.GrpcServices", // Optional: C# namespace for generated server impl
        ClientProxyNamespace = "MyProject.RemoteClients" // Optional: C# namespace for generated client proxy
    )]
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _playerName = "Hero";

        [ObservableProperty]
        private int _score;

        [RelayCommand]
        private void IncreaseScore()
        {
            Score++;
        }

        // ... other properties and commands ...
    }
}
2. Configure .proto File Generation (in Consuming Project's .csproj)The PeakSWC.MvvmSourceGenerator NuGet package includes a .targets file that defines an MSBuild target to run ProtoGeneratorUtil.exe. You need to tell this target which ViewModel files to process by adding MvvmViewModelProtoSource items to your project file (e.g., MonsterClicker.csproj).<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <MvvmViewModelProtoSource Include="ViewModels\GameViewModel.cs">
      <ProtoNamespace>MonsterClicker.ViewModels.Protos</ProtoNamespace>
      <ServiceName>GameViewModelService</ServiceName>
      
      <OutputPath>Protos\GameViewModelService.proto</OutputPath> 
      
      </MvvmViewModelProtoSource>
    
    </ItemGroup>

  </Project>
Explanation of MvvmViewModelProtoSource Metadata:Include: Path to your ViewModel C# file.ProtoNamespace: The desired C# namespace for the types that Grpc.Tools will generate from the .proto file. This must match the first argument of your [GenerateGrpcRemote] attribute.ServiceName: The desired service name for the gRPC service in the .proto file. This must match the second argument of your [GenerateGrpcRemote] attribute.OutputPath: (Optional) Where the generated .proto file should be saved. Defaults to Protos\{ServiceName}.proto relative to the ViewModel file if not specified.AttributeFullName, ObservablePropertyAttribute, RelayCommandAttribute: (Optional) Override if you use different attribute names than the defaults configured in ProtoGeneratorUtil.3. Configure Grpc.Tools (in Consuming Project's .csproj)After ProtoGeneratorUtil.exe generates the .proto file (e.g., Protos\GameViewModelService.proto), you need to tell Grpc.Tools to compile it.Add a <Protobuf> item to your .csproj:<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Grpc.Tools" Version="2.63.0"> <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Google.Protobuf" Version="3.26.1" />
    </ItemGroup>

  <ItemGroup>
    <Protobuf Include="Protos\GameViewModelService.proto" GrpcServices="Both" ProtoRoot="Protos\" Access="Public" />
    </ItemGroup>
  
  </Project>
4. Using the Generated CodeBuild your project. The following will happen:The PeakSWC_GenerateProtoFiles target (from the NuGet package) runs ProtoGeneratorUtil.exe.ProtoGeneratorUtil.exe reads your GameViewModel.cs and writes Protos\GameViewModelService.proto.Grpc.Tools compiles Protos\GameViewModelService.proto and generates C# message classes, the service base class, and the client stub (e.g., in obj\Debug\netX.X\Protos\GameViewModelService.cs and GameViewModelServiceGrpc.cs). These will be in the MonsterClicker.ViewModels.Protos namespace.Your PeakSWC.MvvmSourceGenerator runs and:Generates GameViewModelGrpcServiceImpl.g.cs in the MonsterClicker.GrpcServices namespace. This class will inherit from MonsterClicker.ViewModels.Protos.GameViewModelService.GameViewModelServiceBase.Generates GameViewModelRemoteClient.g.cs in the MonsterClicker.RemoteClients namespace. This class will use MonsterClicker.ViewModels.Protos.GameViewModelService.GameViewModelServiceClient.You can then use these generated classes in your application (e.g., in App.xaml.cs to set up server or client mode).Example App.xaml.cs (Server Mode Snippet):// In App.xaml.cs
using MonsterClicker.ViewModels;
using MonsterClicker.GrpcServices; // Namespace for GameViewModelGrpcServiceImpl
using MonsterClicker.ViewModels.Protos; // Namespace for GameViewModelService (from Grpc.Tools)
using Grpc.Core;
using System.Windows.Threading; // For Dispatcher

// ...
case "server":
    var gameViewModelForServer = new GameViewModel();
    // Pass Application.Current.Dispatcher if your ViewModel interactions need UI thread affinity
    var grpcServiceImpl = new GameViewModelGrpcServiceImpl(gameViewModelForServer, Application.Current.Dispatcher); 
    Server server = new Server
    {
        Services = { GameViewModelService.BindService(grpcServiceImpl) },
        Ports = { new ServerPort("localhost", 50051, ServerCredentials.Insecure) }
    };
    server.Start();
    mainWindow.DataContext = gameViewModelForServer;
    break;
// ...
Example App.xaml.cs (Client Mode Snippet):// In App.xaml.cs
using MonsterClicker.RemoteClients; // Namespace for GameViewModelRemoteClient
using MonsterClicker.ViewModels.Protos; // Namespace for GameViewModelService.GameViewModelServiceClient
using Grpc.Net.Client;

// ...
case "client":
    var channel = GrpcChannel.ForAddress("http://localhost:50051");
    var grpcGeneratedClient = new GameViewModelService.GameViewModelServiceClient(channel);
    var remoteViewModel = new GameViewModelRemoteClient(grpcGeneratedClient);
    await remoteViewModel.InitializeRemoteAsync();
    mainWindow.DataContext = remoteViewModel;
    break;
// ...
TroubleshootingBuild Output: Check the Visual Studio "Output" window (set to "Build") for messages from PeakSWC.MvvmSourceGenerator (prefixed with SGINFO, SGWARN, SGERR) and ProtoGeneratorUtil.exe.Generated Files:The .proto file generated by ProtoGeneratorUtil.exe will be at the OutputPath you specified (or defaulted).C# files from Grpc.Tools are in your project's obj folder (e.g., obj\Debug\netX.X\Protos\).C# files from PeakSWC.MvvmSourceGenerator are also in the obj folder (e.g., obj\Debug\netX.X\generated\PeakSWC.MvvmSourceGenerator\PeakSWC.MvvmSourceGenerator.GrpcRemoteMvvmGenerator\).Namespace Alignment: Ensure the protoCsNamespace in the [GenerateGrpcRemote] attribute, the ProtoNamespace metadata in the .csproj for MvvmViewModelProtoSource, and the option csharp_namespace in the generated .proto file all match.Contributing(Add details if you plan to