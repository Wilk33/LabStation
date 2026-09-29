param(
	[switch]$Publish
)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
$project=Join-Path $root 'src/Ka3005P.App/Ka3005P.App.csproj'
[xml]$projectFile=Get-Content -Raw $project
$version=([string]$projectFile.Project.PropertyGroup.Version).Trim()

dotnet test (Join-Path $root 'tests/Ka3005P.Tests/Ka3005P.Tests.csproj') -c Release --nologo -p:SelfContained=false -p:PublishSingleFile=false -p:RuntimeIdentifier=
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

if($Publish)
{
	$output=Join-Path $root 'artifacts/final/win-x64'
	$archive=Join-Path $root "artifacts/final/Korad-KA3005P-v$version-win-x64.zip"
	if(Test-Path -LiteralPath $output)
	{
		Remove-Item -LiteralPath $output -Recurse -Force
	}
	if(Test-Path -LiteralPath $archive)
	{
		Remove-Item -LiteralPath $archive -Force
	}
	dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $output
	if($LASTEXITCODE -ne 0)
	{
		exit $LASTEXITCODE
	}
	Compress-Archive -Path "$output/*" -DestinationPath $archive -Force
}
