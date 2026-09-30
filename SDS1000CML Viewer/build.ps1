param([switch]$Publish)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try
{
	[xml]$buildProperties=Get-Content -Raw Directory.Build.props
	$version=([string]$buildProperties.Project.PropertyGroup.Version).Trim()
	$releaseName="Siglent-SDS1000CML-Viewer-v$version-win-x64"
	dotnet run --project tests/Scope.Tests -c Release
	if ($LASTEXITCODE -ne 0) { throw 'Testy nie powiodły się.' }
	dotnet run --project tests/Scope.UiTests -c Release
	if ($LASTEXITCODE -ne 0) { throw 'Testy interfejsu nie powiodły się.' }
	dotnet build src/Scope.App -c Release
	if ($LASTEXITCODE -ne 0) { throw 'Kompilacja nie powiodła się.' }
	if ($Publish)
	{
		$finalDirectory=Join-Path $PSScriptRoot 'artifacts/final'
		$publishDirectory=Join-Path $PSScriptRoot "artifacts/final/win-x64"
		$archivePath=Join-Path $PSScriptRoot "artifacts/final/$releaseName.zip"
		$finalPath=[System.IO.Path]::GetFullPath($finalDirectory)
		Get-ChildItem -LiteralPath $finalDirectory -Directory -Filter 'Siglent-SDS1000CML-Viewer-v*-win-x64' |
			ForEach-Object {
				$candidate=[System.IO.Path]::GetFullPath($_.FullName)
				if([System.IO.Path]::GetDirectoryName($candidate) -ne $finalPath)
				{
					throw "Katalog wydania znajduje się poza artifacts/final: $candidate"
				}
				Remove-Item -LiteralPath $candidate -Recurse -Force
			}
		if (Test-Path -LiteralPath $publishDirectory)
		{
			Remove-Item -LiteralPath $publishDirectory -Recurse -Force
		}
		if (Test-Path -LiteralPath $archivePath)
		{
			Remove-Item -LiteralPath $archivePath -Force
		}
		dotnet publish src/Scope.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publishDirectory
		if ($LASTEXITCODE -ne 0) { throw 'Publikowanie nie powiodło się.' }
		Copy-Item -LiteralPath LICENSE,README.md -Destination $publishDirectory
		$dotnetRoot=Split-Path (Get-Command dotnet).Source
		Copy-Item -LiteralPath (Join-Path $dotnetRoot 'LICENSE.txt') -Destination "$publishDirectory/DOTNET-LICENSE.txt"
		Copy-Item -LiteralPath (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') -Destination "$publishDirectory/DOTNET-ThirdPartyNotices.txt"
		Compress-Archive -Path "$publishDirectory/*" -DestinationPath $archivePath -Force
	}
}
finally { Pop-Location }
