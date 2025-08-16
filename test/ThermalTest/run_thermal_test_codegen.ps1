# Define paths and parameters
$workspaceRoot = "C:\users\budcr\source\repos\RemoteMvvm\test\ThermalTest"
$baseDir = $workspaceRoot
$viewModel1 = Join-Path $baseDir "ViewModels\HP3LSThermalTestViewModel.cs"
$viewModel2 = Join-Path $baseDir "ViewModels\ThermalZoneComponentViewModel.cs"
$viewModels = "'$viewModel1'", "'$viewModel2'" -join " "
#$viewModels = "$viewModel1"
$outputDir = Join-Path $workspaceRoot "ThermalTestGenerated"
$protoNamespace = "ThermalTest.Protos"

# Create output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir
}

# Run the RemoteMvvmTool
#$command = "remotemvvm $viewModels --generate proto --output `"$outputDir`" --protoNamespace $protoNamespace"
#Write-Host "Executing: $command"
#Invoke-Expression $command

$command = "remotemvvm $viewModels"
Write-Host "Executing: $command"
Invoke-Expression $command

# Find and display the generated proto file
$protoFile = Get-ChildItem -Path $outputDir -Filter *.proto | Select-Object -First 1
if ($protoFile) {
    Write-Host "Generated proto file: $($protoFile.FullName)"
    Get-Content -Path $protoFile.FullName
} else {
    Write-Host "Proto file not found in $outputDir"
}

