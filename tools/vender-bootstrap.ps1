<#
.SYNOPSIS
  Offline vendor bootstrap for LanMessenger.

.DESCRIPTION
  Ensures required frontend vendor assets exist under wwwroot/vendor/<package>/<version>/...
  Prefers local copy sources if present, otherwise attempts LibMan install.

  Safe to run repeatedly.

.NOTES
  Run from project root:
    powershell -ExecutionPolicy Bypass -File .\tools\vendor-bootstrap.ps1
#>

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Fail($msg) { Write-Host "`nERROR: $msg" -ForegroundColor Red; exit 1 }

# --- Paths ---
$ProjectRoot = (Resolve-Path ".").Path
$Wwwroot     = Join-Path $ProjectRoot "wwwroot"
$VendorRoot  = Join-Path $Wwwroot "vendor"
$ToolsRoot   = Join-Path $ProjectRoot "tools"

# --- Required assets definition ---
$Required = @(
    @{
        Name    = "microsoft-signalr"
        Version = "8.0.7"
        # Destination under wwwroot/vendor
        Dest    = "microsoft-signalr/8.0.7/signalr.min.js"
        # If you already have it somewhere in repo from earlier work, list copy sources here (checked in order)
        CopySources = @(
            "wwwroot/lib/signalr/dist/browser/signalr.min.js",
            "wwwroot/lib/signalr/signalr.min.js"
        )
        # LibMan fallback info (downloads the exact file)
        LibManProvider = "unpkg"
        LibManLibrary  = "@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"
        LibManDestDir  = "wwwroot/vendor/microsoft-signalr/8.0.7"
    }
)

Write-Step "Validating project structure"
if (!(Test-Path $Wwwroot)) { Fail "Missing wwwroot folder. Run in the project root." }

# Ensure vendor root exists
New-Item -ItemType Directory -Path $VendorRoot -Force | Out-Null

# Detect LibMan CLI
function Get-LibManCmd {
    $cmd = Get-Command libman -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}
$LibMan = Get-LibManCmd

Write-Step "Bootstrapping vendor assets"
foreach ($asset in $Required) {
    $destPath = Join-Path $ProjectRoot ("wwwroot/vendor/" + $asset.Dest)

    if (Test-Path $destPath) {
        Write-Host "OK: $($asset.Name) $($asset.Version) exists at $destPath" -ForegroundColor Green
        continue
    }

    # Ensure destination directory exists
    $destDir = Split-Path $destPath -Parent
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null

    # 1) Try local copy sources
    $copied = $false
    foreach ($srcRel in $asset.CopySources) {
        $srcPath = Join-Path $ProjectRoot $srcRel
        if (Test-Path $srcPath) {
            Write-Host "Copying from local source: $srcRel -> $($asset.Dest)" -ForegroundColor Yellow
            Copy-Item -Path $srcPath -Destination $destPath -Force
            $copied = $true
            break
        }
    }

    if ($copied -and (Test-Path $destPath)) {
        Write-Host "OK: Installed via local copy: $($asset.Name) $($asset.Version)" -ForegroundColor Green
        continue
    }

    # 2) Fallback to LibMan
    if (-not $LibMan) {
        Fail "LibMan not found and local copy sources were missing. Install LibMan or add the file manually. See vendor-manifest.md."
    }

    Write-Host "LibMan installing: $($asset.LibManLibrary) -> $($asset.LibManDestDir)" -ForegroundColor Yellow

    # Initialize libman.json if missing
    $libmanJson = Join-Path $ProjectRoot "libman.json"
    if (!(Test-Path $libmanJson)) {
        Write-Host "Creating libman.json (libman init)" -ForegroundColor Yellow
        & $LibMan init | Out-Null
    }

    & $LibMan install $asset.LibManLibrary -p $asset.LibManProvider -d $asset.LibManDestDir

    if (!(Test-Path $destPath)) {
        Fail "LibMan completed but destination file still missing: $destPath. Check provider/library path or directory structure."
    }

    Write-Host "OK: Installed via LibMan: $($asset.Name) $($asset.Version)" -ForegroundColor Green
}

Write-Step "Verifying vendor URLs resolve"
foreach ($asset in $Required) {
    $urlPath = "/vendor/$($asset.Dest.Replace('\','/'))"
    Write-Host "Expect URL: $urlPath" -ForegroundColor Gray
}

Write-Step "Done"
Write-Host "Next: ensure your Index.cshtml uses local vendor paths, e.g.:" -ForegroundColor Gray
Write-Host '  <script src="/vendor/microsoft-signalr/8.0.7/signalr.min.js"></script>' -ForegroundColor Gray
