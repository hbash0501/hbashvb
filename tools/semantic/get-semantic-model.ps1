# ينزّل النموذج الدلالي الموصى به لمنصة بصيرة (multilingual-e5-small بصيغة ONNX مضغوطة، رخصة MIT، نحو 120 ميجابايت)
# إلى مجلد models\semantic. يُشغَّل على جهاز متصل بالإنترنت، ثم يُنسخ المجلد بجوار Basira.exe إن كان جهاز المنصة غير متصل.
# الاستخدام:  powershell -ExecutionPolicy Bypass -File get-semantic-model.ps1 [-Target <مجلد>] [-Source <رابط مرآة>]
param(
  [string]$Target = (Join-Path $PSScriptRoot "semantic"),
  [string]$Source = "https://huggingface.co/Xenova/multilingual-e5-small/resolve/main/"
)
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
if (-not $Source.EndsWith("/")) { $Source += "/" }
New-Item -ItemType Directory -Force -Path $Target | Out-Null
$files = @(
  @{ Remote = "tokenizer.json"; Local = "tokenizer.json"; Required = $true },
  @{ Remote = "config.json"; Local = "config.json"; Required = $false },
  @{ Remote = "onnx/model_quantized.onnx"; Local = "model_quantized.onnx"; Required = $true }
)
foreach ($f in $files) {
  $out = Join-Path $Target $f.Local
  Write-Host "تنزيل $($f.Local) ..."
  try { Invoke-WebRequest -Uri ($Source + $f.Remote) -OutFile "$out.part" -UseBasicParsing; Move-Item -Force "$out.part" $out }
  catch { Remove-Item -ErrorAction SilentlyContinue "$out.part"; if ($f.Required) { throw "تعذر تنزيل $($f.Remote): $_" } }
}
'{"name":"multilingual-e5-small","prefix":"query: ","maxTokens":256}' | Set-Content -Encoding UTF8 (Join-Path $Target "basira-model.json")
Write-Host "تم. المجلد: $Target"
Write-Host "انسخ المجلد إلى models\semantic بجوار Basira.exe (إن لم يكن هناك)، ثم من الإدارة > التصنيفات: «أعد البحث عن النموذج»."
