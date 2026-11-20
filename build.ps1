# Builds the mods (one per branch, see README) with the C# compiler that ships with Windows (.NET Framework 4),
# so no SDK is needed. References come straight from the game install; nothing from the game is copied here.
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Slime Rancher"
)
$ErrorActionPreference = "Stop"

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$managed = Join-Path $GameDir "SlimeRancher_Data\Managed"
$refs = @(
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.UI.dll"),
    (Join-Path $managed "UnityEngine.UIModule.dll"),
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managed "Unity.TextMeshPro.dll"),
    (Join-Path $managed "UnityEngine.InputLegacyModule.dll"),
    (Join-Path $managed "SRML.dll"),
    (Join-Path $GameDir "SRML\Libs\0Harmony.dll"),
    (Join-Path $GameDir "SRML\Mods\SRMP.dll")
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "Missing reference: $r" } }

$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null

$built = 0
foreach ($mod in @("SRMPFixes", "SRBoost", "SRHud", "SRMarket", "SRCorrals", "SRFusion")) {
    $root = Join-Path $PSScriptRoot $mod
    # Each mod lives on its own branch: build only the ones present in this checkout.
    if (-not (Test-Path (Join-Path $root "src"))) { continue }
    $dll = Join-Path $out "$mod.dll"
    $sources = Get-ChildItem (Join-Path $root "src") -Filter *.cs | ForEach-Object { $_.FullName }
    $cscArgs = @("/nologo", "/target:library", "/optimize+", "/out:$dll",
                 "/resource:$(Join-Path $root 'modinfo.json'),$mod.modinfo.json")
    $cscArgs += $refs | ForEach-Object { "/reference:$_" }
    $cscArgs += $sources

    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed for $mod with exit code $LASTEXITCODE" }
    Write-Host "Built $dll"
    $built++
}
if ($built -eq 0) { Write-Host "No mod in this checkout: switch to a mod branch (see README)." }
