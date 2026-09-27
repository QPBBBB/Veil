@echo off
setlocal
rem Expected layout: Content and UnityProject\Assets beside this script.
set "VEIL_CONTENT_LINK=%~dp0UnityProject\Assets\Content"
for %%I in ("%~dp0Content") do set "VEIL_CONTENT_SOURCE=%%~fI"

rem Never replace an existing directory or a link pointing elsewhere.
powershell.exe -NoProfile -Command ^
  "$ErrorActionPreference = 'Stop'; try {" ^
  "$source = $env:VEIL_CONTENT_SOURCE; $link = $env:VEIL_CONTENT_LINK;" ^
  "if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw ('Content directory not found: ' + $source) };" ^
  "if (-not (Test-Path -LiteralPath (Split-Path -Parent $link) -PathType Container)) { throw 'UnityProject Assets directory not found.' };" ^
  "$existing = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue;" ^
  "if ($null -ne $existing) {" ^
  "  if ($existing.LinkType -ne 'Junction') { throw ('Refusing to replace existing path: ' + $link) };" ^
  "  if ([IO.Path]::GetFullPath([string]$existing.Target) -ne $source) { throw ('Existing link points elsewhere: ' + $existing.Target) };" ^
  "  Write-Host ('Content link already exists: ' + $link + ' -> ' + $source);" ^
  "} else {" ^
  "  New-Item -ItemType Junction -Path $link -Target $source | Out-Null;" ^
  "  Write-Host ('Created Content link: ' + $link + ' -> ' + $source);" ^
  "}; exit 0" ^
  "} catch { Write-Host $_.Exception.Message -ForegroundColor Red; exit 1 }"
set "VEIL_LINK_RESULT=%ERRORLEVEL%"
if not "%VEIL_LINK_RESULT%"=="0" echo Content link setup failed.
exit /b %VEIL_LINK_RESULT%
