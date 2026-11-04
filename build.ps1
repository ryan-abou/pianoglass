<#
  PianoGlass — headless Android build.

      .\build.ps1              release build
      .\build.ps1 -Development profiler + script debugging
      .\build.ps1 -Install     also push it to the connected headset

  Why this script exists rather than one long command:

  Unity.exe is a Windows GUI application. Launching one from PowerShell with
  the call operator (&) starts it and returns IMMEDIATELY — it does not wait,
  and it never sets $LASTEXITCODE. You get your prompt back in two seconds, a
  half-written log, and no way to tell whether the build worked.

  Start-Process -Wait -PassThru is the fix: it blocks until Unity exits and
  hands back the process object, whose .ExitCode is the number BuildScript.cs
  set. That is what makes this scriptable — and what lets an agent check the
  result instead of guessing.

  Close the Unity editor first. Unity locks the project's Library folder, and a
  second instance fails with a message about multiple instances that says
  nothing about building.
#>

[CmdletBinding()]
param(
    [switch]$Development,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

$Unity      = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$ProjectDir = $PSScriptRoot
$LogFile    = Join-Path $ProjectDir "Logs\build.log"
$Apk        = Join-Path $ProjectDir "Builds\PianoGlass.apk"

$Method = if ($Development) {
    "PianoGlass.EditorTools.BuildScript.BuildAndroidDevelopment"
} else {
    "PianoGlass.EditorTools.BuildScript.BuildAndroid"
}

if (-not (Test-Path $Unity)) {
    Write-Host "Unity not found at $Unity" -ForegroundColor Red
    Write-Host "Check the version folder under C:\Program Files\Unity\Hub\Editor\"
    exit 1
}

New-Item -ItemType Directory -Force -Path (Split-Path $LogFile) | Out-Null

Write-Host "Building $(if ($Development) {'development'} else {'release'}) — this takes a few minutes." -ForegroundColor Cyan
Write-Host "Watch progress in another window with:" -ForegroundColor DarkGray
Write-Host "  Get-Content `"$LogFile`" -Wait -Tail 20" -ForegroundColor DarkGray

# A running adb server crashes batchmode Unity (0xC0000409) before the
# build script runs. Unity's Android module restarts it when it needs it.
# try/catch: with ErrorActionPreference Stop, PowerShell 5.1 turns adb's
# "server not running" on stderr into a terminating error.
try { & adb kill-server 2>&1 | Out-Null } catch { }

$started = Get-Date

# -PassThru + WaitForExit, not -Wait: -Wait waits for the whole process
# tree, and the adb server Unity's Android module starts outlives Unity, so
# -Wait can hang long after the build has finished. Touching .Handle first
# keeps the exit code readable after the process is gone.
$process = Start-Process -FilePath $Unity -PassThru -ArgumentList @(
    '-batchmode'
    '-quit'
    '-projectPath', $ProjectDir
    '-buildTarget', 'Android'
    '-executeMethod', $Method
    '-logFile', $LogFile
)
$null = $process.Handle
$process.WaitForExit()

$elapsed = (Get-Date) - $started

# Everything BuildScript.cs reports is tagged, so this is the whole story
# without wading through tens of thousands of lines of Unity talking to itself.
Write-Host ""
Select-String -Path $LogFile -Pattern "\[PianoGlassBuild\]" |
    ForEach-Object { Write-Host $_.Line }

Write-Host ""
if ($process.ExitCode -eq 0) {
    $sizeMb = (Get-Item $Apk).Length / 1MB
    Write-Host ("BUILD OK  {0:0.0} MB  {1:0} s  ->  {2}" -f $sizeMb, $elapsed.TotalSeconds, $Apk) -ForegroundColor Green

    if ($Install) {
        Write-Host "Installing to headset..." -ForegroundColor Cyan
        & adb install -r $Apk
        if ($LASTEXITCODE -ne 0) {
            Write-Host "adb install failed. Is the headset awake, plugged in, and authorised?" -ForegroundColor Red
            exit 1
        }
        Write-Host "Installed. Launch it from App Library > Unknown Sources." -ForegroundColor Green
    }
} else {
    Write-Host "BUILD FAILED (exit $($process.ExitCode)) after $([int]$elapsed.TotalSeconds) s" -ForegroundColor Red
    Write-Host "Full log: $LogFile"
    Write-Host "If nothing is tagged above, Unity died before reaching the build script —" -ForegroundColor DarkGray
    Write-Host "usually the editor still being open, or a licensing prompt it cannot show." -ForegroundColor DarkGray
}

exit $process.ExitCode
