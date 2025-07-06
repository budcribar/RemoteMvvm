# Build Instructions for GrpcRemoteMvvmGenerator

1. **Publish ProtoGeneratorUtil**

   Ensure you have published the latest version of the ProtoGeneratorUtil executable as described in its `Build.md` file.

2. **Update Version**

   Increment the `VersionSuffix` in `GrpcRemoteMvvmGenerator.csproj` to reflect a new package version.

3. **Build and Publish GrpcRemoteMvvmGenerator**

   Build the project to generate the updated NuGet package:
dotnet build src/GrpcRemoteMvvmGenerator/GrpcRemoteMvvmGenerator.csproj -c Release
   Or, if you need to publish a self-contained executable, use the appropriate publish profile.

4. **Update Consumers**

   Update the following projects to reference the new version of the `PeakSWC.MvvmSourceGenerator` package:
   - `BlazorMonsterClicker`
   - `MonsterClicker`

   Update the `PackageReference` in their `.csproj` files to match the new version.

5. **Rebuild All Projects**

   Rebuild the solution to ensure all projects use the updated package.