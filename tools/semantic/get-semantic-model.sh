#!/usr/bin/env bash
# ينزّل النموذج الدلالي الموصى به لمنصة بصيرة (multilingual-e5-small بصيغة ONNX مضغوطة، رخصة MIT، نحو 120 ميجابايت).
# الاستخدام: get-semantic-model.sh [مجلد الإخراج] [رابط مرآة]
set -euo pipefail
target="${1:-$(pwd)/models/semantic}"
source="${2:-https://huggingface.co/Xenova/multilingual-e5-small/resolve/main/}"
[[ "$source" == */ ]] || source="$source/"
mkdir -p "$target"
fetch() { curl -fL --retry 3 -o "$target/$2.part" "$source$1" && mv "$target/$2.part" "$target/$2"; }
fetch tokenizer.json tokenizer.json
fetch config.json config.json || { rm -f "$target/config.json.part"; echo "config.json غير متاح (اختياري)"; }
fetch onnx/model_quantized.onnx model_quantized.onnx
printf '{"name":"multilingual-e5-small","prefix":"query: ","maxTokens":256}\n' > "$target/basira-model.json"
echo "تم: $target"
