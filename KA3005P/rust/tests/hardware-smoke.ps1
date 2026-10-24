param(
	[int]$DurationSeconds=120
)

$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot '..\target\release\korad-rs.exe'
$ports=@('COM3','COM4')
$processInfo=[System.Diagnostics.ProcessStartInfo]::new()
$processInfo.FileName=[System.IO.Path]::GetFullPath($exe)
$processInfo.Arguments='serve'
$processInfo.UseShellExecute=$false
$processInfo.RedirectStandardOutput=$true
$processInfo.RedirectStandardInput=$true
$processInfo.RedirectStandardError=$true
$processInfo.CreateNoWindow=$true
$server=[System.Diagnostics.Process]::Start($processInfo)
$lines=@()
for($index=0;$index -lt 5;$index++)
{
	$lines+=$server.StandardOutput.ReadLine()
}
$api=$lines[0].Substring(5)
$headers=@{Authorization="Bearer $($lines[2].Substring(10))"}
$connected=@()
$initial=@{}
$samples=0
$failure=$null

function Invoke-Korad
{
	param([string]$Path,[hashtable]$Body)
	return Invoke-RestMethod -Uri "$api$Path" -Method Post -Headers $headers -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Compress -Depth 5) -ErrorAction Stop
}

function Read-DirectStatus
{
	param([string]$Port)
	$serial=[System.IO.Ports.SerialPort]::new($Port,9600,[System.IO.Ports.Parity]::None,8,[System.IO.Ports.StopBits]::One)
	$serial.DtrEnable=$false
	$serial.ReadTimeout=500
	$serial.WriteTimeout=500
	try
	{
		$serial.Open()
		$serial.DiscardInBuffer()
		$serial.Write('STATUS?')
		$raw=$serial.ReadByte()
		if(($raw -band 64) -ne 0)
		{
			Start-Sleep -Milliseconds 45
			$serial.Write('OUT0')
			Start-Sleep -Milliseconds 45
			$serial.DiscardInBuffer()
			$serial.Write('STATUS?')
			$raw=$serial.ReadByte()
		}
		if(($raw -band 64) -ne 0)
		{
			throw "${Port}: wyjście nadal ON"
		}
		Write-Output "$Port final status=OFF, raw=$raw"
	}
	finally
	{
		$serial.Dispose()
	}
}

try
{
	foreach($port in $ports)
	{
		$null=Invoke-Korad '/v1/connect' @{port=$port}
		$connected+=$port
		$state=Invoke-Korad '/v1/poll' @{port=$port}
		$voltage=(Invoke-Korad '/v1/command' @{port=$port;command='read_set_voltage'})[1].set_voltage_hundredths
		$current=(Invoke-Korad '/v1/command' @{port=$port;command='read_set_current'})[1].set_current_thousandths
		if($state.output_on -ne $false -or $null -eq $voltage -or $null -eq $current -or $voltage -gt 1250 -or $current -gt 1500)
		{
			throw "${port}: niepotwierdzone OFF lub nastawa ponad 12,50 V / 1,500 A"
		}
		$initial[$port]=@{voltage=$voltage;current=$current}
	}
	$null=Invoke-Korad '/v1/dual/output' @{first='COM3';second='COM4';enabled=$true}
	$start=[System.Diagnostics.Stopwatch]::StartNew()
	$lastChange=-1
	while($start.Elapsed.TotalSeconds -lt $DurationSeconds)
	{
		$change=[int][Math]::Floor($start.Elapsed.TotalSeconds/10)
		if($change -ne $lastChange)
		{
			$lastChange=$change
			$delta=$change%2
			foreach($port in $ports)
			{
				$voltageDelta=if($initial[$port].voltage -ge 1250){-$delta}else{$delta}
				$currentDelta=if($initial[$port].current -ge 1500){-$delta}else{$delta}
				$null=Invoke-Korad '/v1/command' @{port=$port;command='set_voltage';value=$initial[$port].voltage+$voltageDelta}
				$null=Invoke-Korad '/v1/command' @{port=$port;command='set_current';value=$initial[$port].current+$currentDelta}
			}
		}
		foreach($port in $ports)
		{
			$state=Invoke-Korad '/v1/poll' @{port=$port}
			if($state.output_on -ne $true -or $null -ne $state.error_code)
			{
				throw "${port}: nieoczekiwany stan wyjścia lub błąd $($state.error_code)"
			}
			$samples++
		}
		Start-Sleep -Seconds 1
	}
	Write-Output "samples=$samples, elapsed=$([Math]::Round($start.Elapsed.TotalSeconds,1))s"
}
catch
{
	$failure=$_.Exception.Message
	Write-Output "test_failure=$failure"
}
finally
{
	foreach($port in $connected)
	{
		try{$null=Invoke-Korad '/v1/command' @{port=$port;command='output_off'}}catch{Write-Output "$port API OFF failed: $($_.Exception.Message)"}
	}
	foreach($port in $connected)
	{
		if($initial.ContainsKey($port))
		{
			try
			{
				$null=Invoke-Korad '/v1/command' @{port=$port;command='set_voltage';value=$initial[$port].voltage}
				$null=Invoke-Korad '/v1/command' @{port=$port;command='set_current';value=$initial[$port].current}
			}
			catch{Write-Output "$port restore failed: $($_.Exception.Message)"}
		}
		try{$null=Invoke-Korad '/v1/disconnect' @{port=$port}}catch{Write-Output "$port disconnect failed: $($_.Exception.Message)"}
	}
	try{$server.StandardInput.WriteLine('quit')}catch{}
	if(-not $server.WaitForExit(3000)){$server.Kill()}
	$stderr=$server.StandardError.ReadToEnd()
	$server.Dispose()
	if($stderr){Write-Output "server_stderr=$stderr"}
	foreach($port in $ports)
	{
		Read-DirectStatus $port
	}
}
if($failure){throw $failure}
