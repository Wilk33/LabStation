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
	$releaseName="Korad-KA3005P-v$version-win-x64"
	$final=Join-Path $root 'artifacts/final'
	$output=Join-Path $final 'win-x64'
	$archive=Join-Path $final "$releaseName.zip"
	$finalPath=[System.IO.Path]::GetFullPath($final)
	Get-ChildItem -LiteralPath $final -Directory -Filter 'Korad-KA3005P-v*-win-x64' |
		ForEach-Object {
			$candidate=[System.IO.Path]::GetFullPath($_.FullName)
			if([System.IO.Path]::GetDirectoryName($candidate) -ne $finalPath)
			{
				throw "Katalog wydania znajduje się poza artifacts/final: $candidate"
			}
			Remove-Item -LiteralPath $candidate -Recurse -Force
		}
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
