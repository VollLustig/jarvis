param([Parameter(Mandatory=$true)][string]$Setup)

# Installs Jarvis with no window, the way a package manager runs her setup. It changes this Windows user's own install,
# so it only runs in a throwaway profile like a CI runner's, with JARVIS_DISPOSABLE_USER=1, and never over a Jarvis that's
# already there.
$ErrorActionPreference = 'Stop'
if ($env:JARVIS_DISPOSABLE_USER -ne '1') { throw 'Set JARVIS_DISPOSABLE_USER=1 in a disposable Windows profile.' }

$setup = (Resolve-Path $Setup).Path
$jarvisDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Jarvis'
$link = Join-Path ([Environment]::GetFolderPath('Programs')) 'Jarvis.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Jarvis.lnk'
$arp = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Jarvis.Desktop'
foreach ($installed in (Join-Path $jarvisDir '.jarvis-install'), (Join-Path $jarvisDir 'Jarvis.exe'), (Join-Path $jarvisDir 'Uninstall.exe'), $link, $desktopLink, $arp) {
    if (Test-Path $installed) { throw "Jarvis is already installed for this user: $installed" }
}

$err = Join-Path $env:TEMP ('jarvis-install-' + [guid]::NewGuid().ToString('N') + '.txt')
function Run([string]$flags) {
    $p = Start-Process $setup -ArgumentList $flags -Wait -PassThru -RedirectStandardError $err
    return @($p.ExitCode, (Get-Content $err -Raw -ErrorAction SilentlyContinue))
}
try {
    $done = Run '--silent'
    if ($done[0] -ne 0) { throw "Jarvis-Setup.exe --silent exited $($done[0]): $($done[1])" }
    foreach ($file in 'Jarvis.exe', 'Uninstall.exe', '.jarvis-install') {
        if (!(Test-Path (Join-Path $jarvisDir $file))) { throw "$file is missing after a silent install." }
    }
    if (!(Test-Path $link)) { throw 'The Start menu shortcut is missing.' }
    if (Test-Path $desktopLink) { throw 'The silent install added a desktop shortcut.' }
    $entry = Get-ItemProperty $arp
    if ($entry.UninstallString -ne ('"' + (Join-Path $jarvisDir 'Uninstall.exe') + '" --uninstall')) { throw "The uninstall entry runs $($entry.UninstallString)." }
    if (Test-Path (Join-Path $jarvisDir 'voice')) { throw 'The silent install downloaded the speech model.' }
    if (@(Get-Process Jarvis -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$jarvisDir\*" }).Count -ne 0) { throw 'The silent install opened Jarvis.' }

    $again = Run '--silent'
    if ($again[0] -ne 0) { throw "A second silent install exited $($again[0]): $($again[1])" }
    if (!(Test-Path (Join-Path $jarvisDir 'Jarvis.exe')) -or !(Test-Path $link)) { throw 'A second silent install lost her app or shortcut.' }

    foreach ($flags in '--silent --unknown', '--silent --update', '--silent --remove-files') {
        $wrong = Run $flags
        if ($wrong[0] -eq 0) { throw "Jarvis-Setup.exe $flags was accepted." }
        if ([string]::IsNullOrWhiteSpace($wrong[1])) { throw "Jarvis-Setup.exe $flags failed without a reason." }
    }
    if (!(Test-Path (Join-Path $jarvisDir '.jarvis-install'))) { throw 'A refused command removed her.' }
    Write-Output 'PASS silent install: app, uninstaller, marker, Start menu shortcut and uninstall entry, no desktop shortcut, speech model or launch; a second run installs over it, and a wrong flag fails with a reason instead of opening a window'
}
finally {
    if (Test-Path $err) { Remove-Item $err -Force }
}
