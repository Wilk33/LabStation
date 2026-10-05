param(
	[switch]$Publish
)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
$project=Join-Path $root 'src/LabStation.App/LabStation.App.csproj'
$tests=Join-Path $root 'tests/LabStation.App.Tests/LabStation.App.Tests.csproj'
$sharedTests=Join-Path $root 'Shared/LabStation.Instruments.Tests/LabStation.Instruments.Tests.csproj'
[xml]$projectFile=Get-Content -Raw $project
$version=([string]$projectFile.Project.PropertyGroup.Version).Trim()

dotnet restore $project -p:LabStationEmbed=true --nologo
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet restore $project --nologo
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet restore $tests -p:LabStationEmbed=true --nologo
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet run --project $tests -c Release --no-restore -p:LabStationEmbed=true
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet run --project $sharedTests -c Release
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

dotnet build $project -c Release --no-restore --nologo
if($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

if($Publish)
{
	$finalRoot=[System.IO.Path]::GetFullPath((Join-Path $root 'artifacts/final'))
	$output=[System.IO.Path]::GetFullPath((Join-Path $finalRoot 'win-x64'))
	if(!$output.StartsWith(
		$finalRoot+[System.IO.Path]::DirectorySeparatorChar,
		[System.StringComparison]::OrdinalIgnoreCase) -or
		[System.IO.Path]::GetFileName($output) -ne 'win-x64')
	{
		throw 'Nieprawidłowy katalog publikacji.'
	}
	$archive=Join-Path $finalRoot "LabStation-v$version-win-x64.zip"
	$versionedExecutable=Join-Path $finalRoot "LabStation-v$version-win-x64.exe"
	if(Test-Path -LiteralPath $output)
	{
		Remove-Item -LiteralPath $output -Recurse -Force
	}
	if(Test-Path -LiteralPath $archive)
	{
		Remove-Item -LiteralPath $archive -Force
	}
	if(Test-Path -LiteralPath $versionedExecutable)
	{
		Remove-Item -LiteralPath $versionedExecutable -Force
	}
	dotnet restore $project -r win-x64 --nologo
	if($LASTEXITCODE -ne 0)
	{
		exit $LASTEXITCODE
	}
	dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None --no-restore -o $output
	if($LASTEXITCODE -ne 0)
	{
		exit $LASTEXITCODE
	}
	$required=@(
		'LabStation.exe',
		'LICENSE',
		'README.md',
		'licenses'
	)
	foreach($name in $required)
	{
		if(!(Test-Path -LiteralPath (Join-Path $output $name)))
		{
			throw "Brak wymaganego elementu publikacji: $name"
		}
	}
	Copy-Item -LiteralPath (Join-Path $output 'LabStation.exe') -Destination $versionedExecutable
	Compress-Archive -Path "$output/*" -DestinationPath $archive -Force
	Write-Host "Opublikowano: $output"
	Write-Host "Wersjonowany EXE: $versionedExecutable"
	Write-Host "Archiwum: $archive"
}
