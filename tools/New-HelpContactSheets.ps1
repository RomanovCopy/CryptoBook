[CmdletBinding()]
param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination, [int]$Columns = 3, [int]$Rows = 4, [int]$TileWidth = 420, [int]$TileHeight = 310)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$files = @(Get-ChildItem -LiteralPath $Source -Filter '*.png' -File | Sort-Object Name)
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$font = [Drawing.Font]::new('Segoe UI', 10)
try {
    $perPage = $Columns * $Rows
    for ($offset=0; $offset -lt $files.Count; $offset += $perPage) {
        $bitmap = [Drawing.Bitmap]::new($Columns*$TileWidth, $Rows*$TileHeight)
        $g = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $g.Clear([Drawing.Color]::Gainsboro)
            $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            for ($j=0; $j -lt $perPage -and $offset+$j -lt $files.Count; $j++) {
                $file = $files[$offset+$j]; $x=($j % $Columns)*$TileWidth; $y=[Math]::Floor($j/$Columns)*$TileHeight
                $g.DrawString($file.BaseName,$font,[Drawing.Brushes]::Black,$x+5,$y+4)
                $sourceImage=[Drawing.Image]::FromFile($file.FullName)
                try {
                    $ratio=[Math]::Min(($TileWidth-10)/$sourceImage.Width,($TileHeight-28)/$sourceImage.Height)
                    $g.DrawImage($sourceImage,[single]($x+5),[single]($y+24),[single]($sourceImage.Width*$ratio),[single]($sourceImage.Height*$ratio))
                } finally { $sourceImage.Dispose() }
            }
            $target=Join-Path $Destination ('contact-{0:D2}.png' -f (1+[int]($offset/$perPage)))
            $bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)
        } finally { $g.Dispose(); $bitmap.Dispose() }
    }
} finally { $font.Dispose() }
Write-Output "Contact sheets: $($files.Count) images in $Destination"
