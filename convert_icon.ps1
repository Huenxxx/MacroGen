Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("$pwd\logo.png")
$icoStream = [System.IO.File]::OpenWrite("$pwd\logo.ico")

$icoStream.WriteByte(0); $icoStream.WriteByte(0)
$icoStream.WriteByte(1); $icoStream.WriteByte(0)
$icoStream.WriteByte(1); $icoStream.WriteByte(0)

$width = $img.Width; if($width -ge 256) { $width = 0 }
$height = $img.Height; if($height -ge 256) { $height = 0 }
$icoStream.WriteByte($width); $icoStream.WriteByte($height)
$icoStream.WriteByte(0); $icoStream.WriteByte(0)
$icoStream.WriteByte(1); $icoStream.WriteByte(0)
$icoStream.WriteByte(32); $icoStream.WriteByte(0)

$pngStream = New-Object System.IO.MemoryStream
$img.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngStream.ToArray()

$size = $pngBytes.Length
$icoStream.Write([System.BitConverter]::GetBytes([int]$size), 0, 4)
$icoStream.Write([System.BitConverter]::GetBytes([int]22), 0, 4)

$icoStream.Write($pngBytes, 0, $size)
$icoStream.Close()
$pngStream.Close()
$img.Dispose()
