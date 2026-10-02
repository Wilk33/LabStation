param(
	[switch]$Publish
)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet run --project (Join-Path $root 'tests/Sdm3000.Tests/Sdm3000.Tests.csproj') -c Release
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet build (Join-Path $root 'SDM3000.Viewer.slnx') -c Release --nologo
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

if($Publish)
{
	$output=Join-Path $root 'artifacts/final/win-x64'
	[xml]$buildProperties=Get-Content -Raw (Join-Path $root 'Directory.Build.props')
	$version=([string]$buildProperties.Project.PropertyGroup.Version).Trim()
	$archive=Join-Path $root "artifacts/final/Siglent-SDM3000-Viewer-v$version-win-x64.zip"
	if(Test-Path -LiteralPath $output)
	{
		Remove-Item -LiteralPath $output -Recurse -Force
	}
	if(Test-Path -LiteralPath $archive)
	{
		Remove-Item -LiteralPath $archive -Force
	}
	dotnet publish (Join-Path $root 'src/Sdm3000.App/Sdm3000.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $output
	if($LASTEXITCODE -ne 0)
	{
		exit $LASTEXITCODE
	}
	Compress-Archive -Path "$output/*" -DestinationPath $archive -Force
}
