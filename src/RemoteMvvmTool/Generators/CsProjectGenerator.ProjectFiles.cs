using System;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static partial class CsProjectGenerator
{
    // ---------------- Project Files ----------------
    public static string GenerateCsProj(string projectName, string serviceName, string runType)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <LangVersion>preview</LangVersion>
    {(isWpf ? "<UseWPF>true</UseWPF>" : string.Empty)}
    {(isWinForms ? "<UseWindowsForms>true</UseWindowsForms>" : string.Empty)}
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Grpc.Net.Client.Web" Version="2.71.0" />
    <PackageReference Include="Google.Protobuf" Version="3.31.1" />
    <PackageReference Include="Grpc.Tools" Version="2.71.0" PrivateAssets="all" />
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <PackageReference Include="Grpc.AspNetCore.Web" Version="2.71.0" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="protos/{serviceName}.proto" GrpcServices="Both" ProtoRoot="protos" Access="Public" />
  </ItemGroup>
</Project>
""";
    }

    public static string GenerateGuiClientCsProj(string projectName, string serviceName, string runType)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);
        var xamlItems = string.Empty; // rely on implicit WPF items
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <LangVersion>preview</LangVersion>
    {(isWpf ? "<UseWPF>true</UseWPF>" : string.Empty)}
    {(isWinForms ? "<UseWindowsForms>true</UseWindowsForms>" : string.Empty)}
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Grpc.Net.Client.Web" Version="2.71.0" />
    <PackageReference Include="Google.Protobuf" Version="3.31.1" />
    <PackageReference Include="Grpc.Tools" Version="2.71.0" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="protos/{serviceName}.proto" GrpcServices="Client" ProtoRoot="protos" Access="Public" />
  </ItemGroup>
{xamlItems}</Project>
""";
    }

    public static string GenerateServerLaunchSettings() => """
{ "profiles": { "Server": { "commandName": "Project", "commandLineArgs": "6000" } } }
""";
    public static string GenerateClientLaunchSettings() => """
{ "profiles": { "Client": { "commandName": "Project", "commandLineArgs": "6000 client" } } }
""";
    public static string GenerateLaunchSettings() => """
{ "profiles": { "TestProject": { "commandName": "Project", "commandLineArgs": "6000" } } }
""";
}