# يولّد نموذج تمثيل صغيراً للاختبار بالواجهة نفسها لنماذج Hugging Face (ONNX + tokenizer.json من نوع Unigram):
# متجه عشوائي ثابت البذرة لكل كلمة، والتمثيل متوسط متجهات الكلمات. يختبر مسار المنصة كاملاً (التقطيع، ONNX، التصنيف،
# المعايرة) دون النموذج الحقيقي؛ لا يقيس دقة الفهم الدلالي.
# python3 make-test-model.py <مجلد الإخراج> <ملفات json بنصوص>...
import json, os, re, sys, unicodedata
import numpy as np
import onnx
from onnx import helper, TensorProto, numpy_helper

out, sources = sys.argv[1], sys.argv[2:]
os.makedirs(out, exist_ok=True)
words, chars = set(), set()
for src in sources:
    for d in json.load(open(src, encoding='utf-8')):
        text = unicodedata.normalize('NFKC', d['text'])
        for w in text.split():
            words.add(w); chars.update(w)
vocab = [['<s>', 0.0], ['<pad>', 0.0], ['</s>', 0.0], ['<unk>', 0.0]]
vocab += [['▁' + w, -5.0] for w in sorted(words)]
vocab += [['▁' + c, -11.0] for c in sorted(chars)] + [[c, -12.0] for c in sorted(chars)]
dim = 48
rng = np.random.default_rng(20261026)
emb = rng.normal(0, 1, (len(vocab), dim)).astype(np.float32)
emb[:4] = 0  # الرموز الخاصة لا تحمل معنى
tokenizer = {
    'version': '1.0', 'truncation': None, 'padding': None, 'added_tokens': [],
    'normalizer': None,
    'pre_tokenizer': {'type': 'Metaspace', 'replacement': '▁', 'prepend_scheme': 'always', 'split': True},
    'post_processor': {'type': 'TemplateProcessing',
        'single': [{'SpecialToken': {'id': '<s>', 'type_id': 0}}, {'Sequence': {'id': 'A', 'type_id': 0}}, {'SpecialToken': {'id': '</s>', 'type_id': 0}}],
        'pair': [], 'special_tokens': {'<s>': {'id': '<s>', 'ids': [0], 'tokens': ['<s>']}, '</s>': {'id': '</s>', 'ids': [2], 'tokens': ['</s>']}}},
    'decoder': None,
    'model': {'type': 'Unigram', 'unk_id': 3, 'vocab': vocab, 'byte_fallback': False},
}
json.dump(tokenizer, open(os.path.join(out, 'tokenizer.json'), 'w', encoding='utf-8'), ensure_ascii=False)
# الرسم: Gather(جدول التمثيل، input_ids) × قناع الانتباه ← last_hidden_state [1، N، dim]
table = numpy_helper.from_array(emb, 'embeddings')
nodes = [
    helper.make_node('Gather', ['embeddings', 'input_ids'], ['gathered'], axis=0),
    helper.make_node('Cast', ['attention_mask'], ['mask_f'], to=TensorProto.FLOAT),
    helper.make_node('Unsqueeze', ['mask_f', 'axes'], ['mask_3d']),
    helper.make_node('Mul', ['gathered', 'mask_3d'], ['last_hidden_state']),
]
axes = numpy_helper.from_array(np.array([2], dtype=np.int64), 'axes')
graph = helper.make_graph(nodes, 'basira-test-embedder',
    [helper.make_tensor_value_info('input_ids', TensorProto.INT64, [1, 'n']), helper.make_tensor_value_info('attention_mask', TensorProto.INT64, [1, 'n'])],
    [helper.make_tensor_value_info('last_hidden_state', TensorProto.FLOAT, [1, 'n', dim])], [table, axes])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid('', 13)], producer_name='basira-test')
model.ir_version = 8
onnx.checker.check_model(model)
onnx.save(model, os.path.join(out, 'model.onnx'))
json.dump({'name': 'basira-test-embedder', 'prefix': '', 'maxTokens': 256}, open(os.path.join(out, 'basira-model.json'), 'w'), ensure_ascii=False)
print(out, len(vocab), 'tokens', os.path.getsize(os.path.join(out, 'model.onnx')), 'bytes')
