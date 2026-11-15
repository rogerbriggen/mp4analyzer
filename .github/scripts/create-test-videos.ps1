param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../testdata')
)

$ErrorActionPreference = 'Stop'
Get-Command ffmpeg -ErrorAction Stop | Out-Null

# A blinking block represents the changing clock, entirely inside the clock mask.
$clock = "drawbox=x=280:y=4:w=32:h=10:color=white:t=fill:enable='lt(mod(t,1),0.5)'"
# Alternate the object's position every half-second, outside the clock mask.
$motion = "drawbox=x=40:y=80:w=48:h=48:color=white:t=fill:enable='lt(mod(t,1),0.5)'," +
          "drawbox=x=160:y=80:w=48:h=48:color=white:t=fill:enable='gte(mod(t,1),0.5)'"

$videos = @(
    @{ Category = 'no_movement'; Name = 'synthetic-clock-only.mp4'; Filter = $clock },
    @{ Category = 'movement'; Name = 'synthetic-motion.mp4'; Filter = "$motion,$clock" }
)

foreach ($video in $videos) {
    $folder = Join-Path $OutputDirectory $video.Category
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $target = Join-Path $folder $video.Name
    if (Test-Path -LiteralPath $target) {
        throw "Refusing to overwrite existing video: $target"
    }

    & ffmpeg -hide_banner -loglevel error -nostdin -n `
        -f lavfi -i 'color=c=black:s=320x180:r=10:d=4' `
        -vf $video.Filter -c:v libx264 -pix_fmt yuv420p $target
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed to generate $target (exit code $LASTEXITCODE)."
    }
    Write-Host "Generated $target"
}
