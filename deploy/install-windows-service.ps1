# تثبيت البوابة كخدمة Windows (تشغيل كمسؤول). لم يُختبر على Windows في بيئة التطوير.
# الاستخدام:  .\install-windows-service.ps1 -Publish C:\PtzGateway -Config C:\PtzGateway\gateway.json
param(
  [Parameter(Mandatory)] [string] $Publish,
  [Parameter(Mandatory)] [string] $Config,
  [string] $ServiceName = "PtzGateway"
)
$ErrorActionPreference = "Stop"
$exe = Join-Path $Publish "PtzGateway.exe"
if (-not (Test-Path $exe)) { throw "لم يُعثر على $exe (انشر أولاً: dotnet publish src/Ptz.Gateway -c Release -o $Publish)" }
$data = Join-Path $env:ProgramData "PtzGateway"
New-Item -ItemType Directory -Force -Path $data | Out-Null
# صلاحيات المجلد: النظام والمسؤولون فقط
icacls $data /inheritance:r /grant:r "SYSTEM:(OI)(CI)F" "BUILTIN\Administrators:(OI)(CI)F" | Out-Null
[Environment]::SetEnvironmentVariable("PTZ_GATEWAY_CONFIG", $Config, "Machine")
New-Service -Name $ServiceName -BinaryPathName "`"$exe`" serve" -DisplayName "PTZ Gateway" -StartupType Automatic | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
Write-Host "أُنشئت الخدمة. أضف المستخدمين أولاً:  & `"$exe`" user add <name> supervisor ; totp enroll <name> ; camera set ..."
Write-Host "ثم:  Start-Service $ServiceName"
