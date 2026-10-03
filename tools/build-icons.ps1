param(
	[switch]$VerifyOnly
)

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing

$root=Split-Path -Parent $PSScriptRoot
$sizes=@(16,32,256)
$icons=@(
	@{
		Name='KoradS'
		Source='KA3005P/assets/reference/KoradS.png'
		Output='KA3005P/src/Ka3005P.App/Assets/KoradS.ico'
		Variants='KA3005P/assets/icons'
	},
	@{
		Name='siglent_sds1102cml+'
		Source='SDS1000CML Viewer/siglent_sds1102cml+.png'
		Output='SDS1000CML Viewer/siglent_sds1102cml+.ico'
		Variants='SDS1000CML Viewer/assets/icons'
	},
	@{
		Name='sdg1062x'
		Source='SDG1000X Control/sdg1062x.png'
		Output='SDG1000X Control/sdg1062x.ico'
		Variants='SDG1000X Control/assets/icons'
	},
	@{
		Name='Siglent_SDM3055'
		Source='SDM3000 Viewer/Siglent_SDM3055.png'
		Output='SDM3000 Viewer/Siglent_SDM3055.ico'
		Variants='SDM3000 Viewer/assets/icons'
	},
	@{
		Name='SDL1020X'
		Source='SDL1000X Control/SDL1020X.png'
		Output='SDL1000X Control/SDL1020X.ico'
		Variants='SDL1000X Control/assets/icons'
	}
)

function Get-AlphaBounds
{
	param([Drawing.Bitmap]$Bitmap)
	$left=$Bitmap.Width
	$top=$Bitmap.Height
	$right=-1
	$bottom=-1
	for($y=0;$y -lt $Bitmap.Height;$y++)
	{
		for($x=0;$x -lt $Bitmap.Width;$x++)
		{
			if($Bitmap.GetPixel($x,$y).A -gt 4)
			{
				$left=[Math]::Min($left,$x)
				$top=[Math]::Min($top,$y)
				$right=[Math]::Max($right,$x)
				$bottom=[Math]::Max($bottom,$y)
			}
		}
	}
	if($right -lt $left -or $bottom -lt $top)
	{
		throw 'Źródłowy PNG nie zawiera widocznych pikseli.'
	}
	return [Drawing.Rectangle]::FromLTRB($left,$top,$right+1,$bottom+1)
}

function New-IconFrame
{
	param(
		[Drawing.Bitmap]$Source,
		[Drawing.Rectangle]$Bounds,
		[int]$Size
	)
	$bitmap=[Drawing.Bitmap]::new(
		$Size,
		$Size,
		[Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$graphics=[Drawing.Graphics]::FromImage($bitmap)
	try
	{
		$graphics.Clear([Drawing.Color]::Transparent)
		$graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
		$graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
		$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
		$graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
		$graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::HighQuality
		$padding=[Math]::Max(1,[Math]::Round($Size*0.04))
		$available=$Size-2*$padding
		$scale=[Math]::Min(
			$available/[double]$Bounds.Width,
			$available/[double]$Bounds.Height)
		$width=[Math]::Max(1,[Math]::Round($Bounds.Width*$scale))
		$height=[Math]::Max(1,[Math]::Round($Bounds.Height*$scale))
		$x=[Math]::Floor(($Size-$width)/2)
		$y=[Math]::Floor(($Size-$height)/2)
		$destination=[Drawing.Rectangle]::new($x,$y,$width,$height)
		$graphics.DrawImage(
			$Source,
			$destination,
			$Bounds.X,
			$Bounds.Y,
			$Bounds.Width,
			$Bounds.Height,
			[Drawing.GraphicsUnit]::Pixel)
	}
	finally
	{
		$graphics.Dispose()
	}
	return $bitmap
}

function Convert-BitmapToPngBytes
{
	param([Drawing.Bitmap]$Bitmap)
	$stream=[IO.MemoryStream]::new()
	try
	{
		$Bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
		return ,$stream.ToArray()
	}
	finally
	{
		$stream.Dispose()
	}
}

function Write-MultiFrameIcon
{
	param(
		[string]$Path,
		[hashtable]$Frames
	)
	$ordered=$sizes|ForEach-Object {
		@{
			Size=$_
			Data=$Frames[$_]
		}
	}
	$stream=[IO.File]::Create($Path)
	$writer=[IO.BinaryWriter]::new($stream)
	try
	{
		$writer.Write([uint16]0)
		$writer.Write([uint16]1)
		$writer.Write([uint16]$ordered.Count)
		$offset=6+16*$ordered.Count
		foreach($frame in $ordered)
		{
			$dimension=if($frame.Size -eq 256){0}else{$frame.Size}
			$writer.Write([byte]$dimension)
			$writer.Write([byte]$dimension)
			$writer.Write([byte]0)
			$writer.Write([byte]0)
			$writer.Write([uint16]1)
			$writer.Write([uint16]32)
			$writer.Write([uint32]$frame.Data.Length)
			$writer.Write([uint32]$offset)
			$offset+=$frame.Data.Length
		}
		foreach($frame in $ordered)
		{
			$writer.Write([byte[]]$frame.Data)
		}
	}
	finally
	{
		$writer.Dispose()
	}
}

function Read-IconEntries
{
	param([string]$Path)
	$stream=[IO.File]::OpenRead($Path)
	$reader=[IO.BinaryReader]::new($stream)
	try
	{
		if($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1)
		{
			throw "Nieprawidłowy nagłówek ICO: $Path"
		}
		$count=$reader.ReadUInt16()
		$entries=@()
		for($index=0;$index -lt $count;$index++)
		{
			$width=$reader.ReadByte()
			$height=$reader.ReadByte()
			$reader.ReadByte()|Out-Null
			$reader.ReadByte()|Out-Null
			$planes=$reader.ReadUInt16()
			$bits=$reader.ReadUInt16()
			$length=$reader.ReadUInt32()
			$offset=$reader.ReadUInt32()
			$entries+=@{
				Width=if($width -eq 0){256}else{[int]$width}
				Height=if($height -eq 0){256}else{[int]$height}
				Planes=$planes
				Bits=$bits
				Length=$length
				Offset=$offset
			}
		}
		return $entries
	}
	finally
	{
		$reader.Dispose()
	}
}

foreach($icon in $icons)
{
	$sourcePath=Join-Path $root $icon.Source
	$outputPath=Join-Path $root $icon.Output
	$variantDirectory=Join-Path $root $icon.Variants
	if(!$VerifyOnly)
	{
		[IO.Directory]::CreateDirectory($variantDirectory)|Out-Null
		$source=[Drawing.Bitmap]::new($sourcePath)
		try
		{
			$bounds=Get-AlphaBounds $source
			$frames=@{}
			foreach($size in $sizes)
			{
				$frame=New-IconFrame $source $bounds $size
				try
				{
					$pngPath=Join-Path $variantDirectory ($icon.Name+"-$size.png")
					$frame.Save($pngPath,[Drawing.Imaging.ImageFormat]::Png)
					$frames[$size]=Convert-BitmapToPngBytes $frame
				}
				finally
				{
					$frame.Dispose()
				}
			}
			Write-MultiFrameIcon $outputPath $frames
		}
		finally
		{
			$source.Dispose()
		}
	}
	$entries=Read-IconEntries $outputPath
	$actual=@($entries|ForEach-Object {$_.Width})
	$invalidEntries=@($entries|Where-Object {
		$_.Width -ne $_.Height -or $_.Bits -ne 32
	})
	if($entries.Count -ne 3 -or
		[string]::Join(',',$actual) -ne [string]::Join(',',$sizes) -or
		$invalidEntries.Count -ne 0)
	{
		throw "Ikona $($icon.Name) nie zawiera klatek 16, 32 i 256 px w formacie 32 bpp."
	}
	foreach($size in $sizes)
	{
		$variant=Join-Path $variantDirectory ($icon.Name+"-$size.png")
		$bitmap=[Drawing.Bitmap]::new($variant)
		try
		{
			if($bitmap.Width -ne $size -or $bitmap.Height -ne $size)
			{
				throw "Wariant $variant ma nieprawidłowy rozmiar."
			}
		}
		finally
		{
			$bitmap.Dispose()
		}
	}
	Write-Host "OK $($icon.Name): 16, 32, 256 px"
}

if(!$VerifyOnly)
{
	$qaDirectory=Join-Path $root 'artifacts/qa'
	[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
	$sheet=[Drawing.Bitmap]::new(
		900,
		$icons.Count*170,
		[Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$graphics=[Drawing.Graphics]::FromImage($sheet)
	$font=[Drawing.Font]::new('Segoe UI',12,[Drawing.FontStyle]::Regular)
	$labelBrush=[Drawing.SolidBrush]::new([Drawing.Color]::White)
	try
	{
		$graphics.Clear([Drawing.Color]::FromArgb(36,36,36))
		for($row=0;$row -lt $icons.Count;$row++)
		{
			$icon=$icons[$row]
			$top=$row*170
			$graphics.DrawString($icon.Name,$font,$labelBrush,10,$top+8)
			$column=0
			foreach($size in $sizes)
			{
				$variantDirectory=Join-Path $root $icon.Variants
				$variant=Join-Path $variantDirectory ($icon.Name+"-$size.png")
				$image=[Drawing.Bitmap]::new($variant)
				try
				{
					$x=170+$column*220
					for($checkerY=0;$checkerY -lt 128;$checkerY+=16)
					{
						for($checkerX=0;$checkerX -lt 128;$checkerX+=16)
						{
							$shade=if((($checkerX+$checkerY)/16)%2 -eq 0){72}else{96}
							$brush=[Drawing.SolidBrush]::new(
								[Drawing.Color]::FromArgb($shade,$shade,$shade))
							try
							{
								$graphics.FillRectangle(
									$brush,
									$x+$checkerX,
									$top+32+$checkerY,
									16,
									16)
							}
							finally
							{
								$brush.Dispose()
							}
						}
					}
					$graphics.InterpolationMode=if($size -lt 256)
					{
						[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
					}
					else
					{
						[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
					}
					$graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
					$graphics.DrawImage($image,$x,$top+32,128,128)
					$graphics.DrawString(
						"$size px",
						$font,
						$labelBrush,
						$x+132,
						$top+82)
				}
				finally
				{
					$image.Dispose()
				}
				$column++
			}
		}
		$sheet.Save(
			(Join-Path $qaDirectory 'icon-variants.png'),
			[Drawing.Imaging.ImageFormat]::Png)
	}
	finally
	{
		$labelBrush.Dispose()
		$font.Dispose()
		$graphics.Dispose()
		$sheet.Dispose()
	}
}
