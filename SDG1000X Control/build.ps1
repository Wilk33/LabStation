param(
	[switch]$Publish
)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet run --project (Join-Path $root 'tests/Sdg1032X.Tests/Sdg1032X.Tests.csproj') -c Release
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet build (Join-Path $root 'SDG1000X.Control.slnx') -c Release --nologo
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

if($Publish)
{
	$output=Join-Path $root 'artifacts/final/win-x64'
	[xml]$buildProperties=Get-Content -Raw (Join-Path $root 'Directory.Build.props')
	$version=[string]$buildProperties.Project.PropertyGroup.Version
	$archive=Join-Path $root "artifacts/final/Siglent-SDG1000X-Control-v$version-win-x64.zip"
	if(Test-Path -LiteralPath $output)
	{
		Remove-Item -LiteralPath $output -Recurse -Force
	}
	if(Test-Path -LiteralPath $archive)
	{
		Remove-Item -LiteralPath $archive -Force
	}
	dotnet publish (Join-Path $root 'src/Sdg1032X.App/Sdg1032X.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $output
	if($LASTEXITCODE -ne 0)
	{
		exit $LASTEXITCODE
	}
	Compress-Archive -Path "$output/*" -DestinationPath $archive -Force
}
