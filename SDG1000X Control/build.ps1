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
	New-Item -ItemType Directory -Force -Path $output | Out-Null
	dotnet publish (Join-Path $root 'src/Sdg1032X.App/Sdg1032X.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output
	if($LASTEXITCODE -ne 0)
	{
		exit $LASTEXITCODE
	}
}
