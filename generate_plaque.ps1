Add-Type -AssemblyName System.Drawing
$width = 120
$height = 40
$bmp = New-Object System.Drawing.Bitmap($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Black)

# Load the downloaded icon
$iconPath = "D:\Software\Playnite Roblox Integration\Resources\icon.png"
if (Test-Path $iconPath) {
    $icon = [System.Drawing.Image]::FromFile($iconPath)
    # Resize and draw icon on the left
    $iconSize = 24
    $iconY = ($height - $iconSize) / 2
    $g.DrawImage($icon, 10, $iconY, $iconSize, $iconSize)
    $icon.Dispose()
}

# Draw texts and line
$fontTop = New-Object System.Drawing.Font("Arial", 9, [System.Drawing.FontStyle]::Bold)
$fontBottom = New-Object System.Drawing.Font("Arial", 7, [System.Drawing.FontStyle]::Bold)
$brush = [System.Drawing.Brushes]::White
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 1)

# Draw ROBLOX text
$g.DrawString("ROBLOX", $fontTop, $brush, 42, 4)

# Draw Line
$g.DrawLine($pen, 42, 20, 110, 20)

# Draw GAME text
$g.DrawString("GAME", $fontBottom, $brush, 42, 23)

# Save image
$outputPath = "D:\Software\Playnite Roblox Integration\Resources\platform_icon.png"
$bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

$pen.Dispose()
$fontTop.Dispose()
$fontBottom.Dispose()
$g.Dispose()
$bmp.Dispose()
Write-Host "Plaque created successfully!"
