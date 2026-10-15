$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (Get-Process -Name AIDocumentArchiveVB -ErrorAction SilentlyContinue) { throw 'Close the archive application before backup.' }
$settingsFolder = Join-Path $env:APPDATA 'SaeedAIDocArchive'
$settingsFile = Join-Path $settingsFolder 'settings.xml'
$baseFolder = $settingsFolder
$archiveFolder = Join-Path $settingsFolder 'ArchiveFiles'
if (Test-Path $settingsFile) {
    [xml]$settings = Get-Content $settingsFile -Encoding UTF8
    if ($settings.Settings.BaseDataFolder) { $baseFolder = [string]$settings.Settings.BaseDataFolder }
    if ($settings.Settings.ArchiveRootFolder) { $archiveFolder = [string]$settings.Settings.ArchiveRootFolder }
}
$destination = Join-Path $root ('Backups\Basira_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$copied = @{}
foreach ($entry in @(@('Settings', $settingsFolder), @('Data', $baseFolder), @('Archive', $archiveFolder))) {
    $label = $entry[0]; $source = $entry[1]
    $alreadyIncluded = $false
    foreach ($existingRoot in $copied.Keys) {
        if ([string]::Equals($source, $existingRoot, [StringComparison]::OrdinalIgnoreCase) -or $source.StartsWith($existingRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { $alreadyIncluded = $true }
    }
    if ((Test-Path $source) -and -not $alreadyIncluded) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $destination $label) -Recurse -Force
        $copied[$source] = $true
    }
}
Write-Host "Backup saved: $destination" -ForegroundColor Green
