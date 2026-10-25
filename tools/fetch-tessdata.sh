#!/usr/bin/env bash
# يجلب نماذج Tesseract الدقيقة (tessdata_best، رخصة Apache 2.0) للعربية والإنجليزية ويتحقق من بصمتها.
# تُرفق بجوار الملف التنفيذي، فتعمل المنصة بها مع أي Tesseract مثبت. لا تُحفظ في المستودع لحجمها (نحو 28 م.ب).
# الاستخدام: tools/fetch-tessdata.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dir="$root/src/Basira.Platform/tessdata"
base="https://raw.githubusercontent.com/tesseract-ocr/tessdata_best/main"
declare -A sha=(
  [ara]=ab9d157d8e38ca00e7e39c7d5363a5239e053f5b0dbdb3167dde9d8124335896
  [eng]=8280aed0782fe27257a68ea10fe7ef324ca0f8d85bd2fd145d1c2b560bcb66ba
)
mkdir -p "$dir"
for lang in ara eng; do
  file="$dir/$lang.traineddata"
  if [[ -f "$file" ]] && echo "${sha[$lang]}  $file" | sha256sum -c --status; then continue; fi
  curl -fsSL --retry 4 --retry-delay 2 -o "$file.part" "$base/$lang.traineddata"
  echo "${sha[$lang]}  $file.part" | sha256sum -c --status || { rm -f "$file.part"; echo "بصمة $lang.traineddata غير مطابقة" >&2; exit 1; }
  mv "$file.part" "$file"
done
echo "$dir"
