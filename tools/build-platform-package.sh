#!/usr/bin/env bash
# يبني منصة بصيرة ملفاً تنفيذياً واحداً لـ Windows (لا يحتاج تثبيت .NET) ويجمعه في حزمة zip.
# الاستخدام: tools/build-platform-package.sh [مجلد الإخراج]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-$root/dist}"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/Basira.Platform/Basira.Platform.csproj")"
stage="$(mktemp -d)"
pkg="$stage/Basira_$version"
dotnet publish "$root/src/Basira.Platform/Basira.Platform.csproj" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none \
  -o "$stage/publish" -nologo
mkdir -p "$pkg"
cp "$stage/publish/Basira.exe" "$pkg/"
cp "$root/tools/platform-readme-ar.txt" "$pkg/README_AR.txt"
cp "$root/tools/platform-third-party.txt" "$pkg/THIRD_PARTY_NOTICES.txt"
mkdir -p "$out"
rm -f "$out/Basira_${version}_Windows.zip"
python3 - "$out/Basira_${version}_Windows.zip" "$pkg" <<'PY'
import os, sys, zipfile
z = zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED)
for f in sorted(os.listdir(sys.argv[2])):
    z.write(os.path.join(sys.argv[2], f), os.path.join(os.path.basename(sys.argv[2]), f))
z.close()
PY
rm -rf "$stage"
echo "$out/Basira_${version}_Windows.zip"
