# Builds ProClicker and puts everything a GitHub release needs into the "release" folder.
# Run it by double-clicking publish.bat.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = [regex]::Match((Get-Content ProClicker.cs -Raw), 'public const string Version = "([^"]+)"').Groups[1].Value
if (-not $version) { throw "Couldn't find the version number in ProClicker.cs" }

Write-Host "Building ProClicker $version..." -ForegroundColor Cyan
cmd /c "`"$PSScriptRoot\build.bat`""
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

New-Item -ItemType Directory -Force release | Out-Null
Copy-Item ProClicker.exe release\ProClicker.exe -Force
$hash = (Get-FileHash release\ProClicker.exe -Algorithm SHA256).Hash.ToLower()
Set-Content release\ProClicker.exe.sha256 "$hash  ProClicker.exe" -Encoding ascii -NoNewline

Write-Host ""
Write-Host "Ready to publish!" -ForegroundColor Green
Write-Host "  Tag:   v$version"
Write-Host "  Files: release\ProClicker.exe and release\ProClicker.exe.sha256"
Write-Host ""
Write-Host "Next: on GitHub go to Releases > Draft a new release, create tag v$version,"
Write-Host "write what changed, attach BOTH files from the release folder, then Publish."
Write-Host ""
explorer.exe release
Read-Host "Press Enter to close"
