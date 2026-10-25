#!/usr/bin/env bash
# يبني منصة بصيرة ملفاً تنفيذياً واحداً لـ Windows (لا يحتاج تثبيت .NET) ويجمعه في حزمة zip.
# الاستخدام: tools/build-platform-package.sh [مجلد الإخراج]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-$root/dist}"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/Basira.Platform/Basira.Platform.csproj")"
"$root/tools/fetch-tessdata.sh" >/dev/null
stage="$(mktemp -d)"
pkg="$stage/Basira_$version"
dotnet publish "$root/src/Basira.Platform/Basira.Platform.csproj" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none \
  -o "$stage/publish" -nologo
mkdir -p "$pkg"
cp "$stage/publish/Basira.exe" "$pkg/"
# نماذج OCR الدقيقة بجوار الملف التنفيذي (تُقرأ منها مباشرة، لا تُضمَّن في الملف الواحد).
cp -r "$stage/publish/tessdata" "$pkg/"
cp "$root/tools/platform-readme-ar.txt" "$pkg/README_AR.txt"
cp "$root/tools/platform-third-party.txt" "$pkg/THIRD_PARTY_NOTICES.txt"
mkdir -p "$out"
rm -f "$out/Basira_${version}_Windows.zip"
python3 - "$out/Basira_${version}_Windows.zip" "$pkg" <<'PY'
import os, sys, zipfile
z = zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED)
base = os.path.dirname(sys.argv[2])
for d, _, files in sorted(os.walk(sys.argv[2])):
    for f in sorted(files):
        p = os.path.join(d, f)
        z.write(p, os.path.relpath(p, base))
z.close()
PY
rm -rf "$stage"
echo "$out/Basira_${version}_Windows.zip"
