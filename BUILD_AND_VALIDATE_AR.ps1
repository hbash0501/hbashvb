$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFolder = Join-Path $root 'AIDocumentArchiveVB'
$projectPath = Join-Path $projectFolder 'AIDocumentArchiveVB.vbproj'
[xml]$project = Get-Content $projectPath -Encoding UTF8
$ns = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
$ns.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
foreach ($item in $project.SelectNodes('//m:Compile', $ns)) {
    if (-not (Test-Path (Join-Path $projectFolder $item.Include))) { throw "Missing source file: $($item.Include)" }
}
foreach ($item in $project.SelectNodes('//m:Reference/m:HintPath', $ns)) {
    if (-not (Test-Path (Join-Path $projectFolder $item.InnerText))) { throw "Missing reference: $($item.InnerText)" }
}
$command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
$msbuildPath = if ($command) { $command.Source } else { $null }
if (-not $msbuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) { $msbuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
}
if (-not $msbuildPath) { throw 'Install Visual Studio 2022 VB.NET desktop tools and .NET Framework 4.8 Targeting Pack.' }
& $msbuildPath (Join-Path $root 'AIDocumentArchiveVB.sln') /t:Rebuild /p:Configuration=Release /m
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Host 'Release build completed. Run ACCEPTANCE_WINDOWS_AR.md before employee deployment.' -ForegroundColor Green
