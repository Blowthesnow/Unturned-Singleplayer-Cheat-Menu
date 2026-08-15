param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [double]$MaximumNearWhiteRatio = 0.20
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Screenshot does not exist: $Path"
}

Add-Type -AssemblyName System.Drawing

$bitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Path))
try {
    # Normalized crop covering the 5x3 vehicle-card thumbnail grid in the
    # 2560x1440/2430x1794 runtime captures. Sampling every second pixel keeps
    # this check fast while retaining a deterministic signal for solid-white
    # placeholder rectangles.
    $left = [int]($bitmap.Width * 0.21)
    $right = [int]($bitmap.Width * 0.91)
    $top = [int]($bitmap.Height * 0.30)
    $bottom = [int]($bitmap.Height * 0.86)

    [long]$nearWhite = 0
    [long]$samples = 0

    for ($y = $top; $y -lt $bottom; $y += 2) {
        for ($x = $left; $x -lt $right; $x += 2) {
            $pixel = $bitmap.GetPixel($x, $y)
            $samples++
            if ($pixel.R -ge 245 -and $pixel.G -ge 245 -and $pixel.B -ge 245) {
                $nearWhite++
            }
        }
    }

    $ratio = if ($samples -gt 0) { $nearWhite / [double]$samples } else { 1.0 }
    $result = [pscustomobject]@{
        Screenshot            = (Resolve-Path -LiteralPath $Path).Path
        Width                 = $bitmap.Width
        Height                = $bitmap.Height
        Samples               = $samples
        NearWhitePixels       = $nearWhite
        NearWhiteRatio        = [Math]::Round($ratio, 6)
        MaximumAllowedRatio   = $MaximumNearWhiteRatio
        Verdict               = if ($ratio -le $MaximumNearWhiteRatio) { 'PASS' } else { 'FAIL' }
    }

    $result | Format-List

    if ($ratio -gt $MaximumNearWhiteRatio) {
        throw (
            "Vehicle thumbnail grid is still dominated by white placeholders: " +
            "{0:P2} near-white pixels (maximum {1:P2})." -f $ratio, $MaximumNearWhiteRatio
        )
    }
}
finally {
    $bitmap.Dispose()
}
