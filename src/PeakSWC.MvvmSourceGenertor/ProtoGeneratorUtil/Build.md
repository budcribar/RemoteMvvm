# Build Instructions for ProtoGeneratorUtil

1. **Publish the ProtoGeneratorUtil Executable**

   Use the following command to publish the self-contained executable that is embedded into the GrpcRemoteMvvmGenerator package:
dotnet publish src/PeakSWC.MvvmSourceGenertor/ProtoGeneratorUtil/ProtoGeneratorUtil.csproj /p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
   > The publish profile already sets the configuration, runtime, and self-contained options.

2. **After Making Changes**

   If you make any changes to this project, follow the instructions in the `Build.md` file in the `GrpcRemoteMvvmGenerator` project to update the package and consumers.


