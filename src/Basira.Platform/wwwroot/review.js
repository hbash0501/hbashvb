// بصيرة — التحقق من القراءة الآلية: الحقول الحرجة غير المؤكدة تُعرض بمقتطع صورتها ليؤكدها المراجع أو يصححها.
import { h, icon, api, mount, nf, toast, fmt } from './core.js';
import { pageHead, empty } from './views.js';

const HINT = {
  number: 'اكتب الرقم كما في الصورة، مع الحرف الملحق إن وُجد (مثل 534/ت).',
  date: 'اكتب التاريخ كما في الصورة (مثل 1446/08/07هـ أو 7 شعبان 1446هـ).',
  subject: 'صحح كلمات الموضوع كما في الصورة.',
  reference: 'صحح الرقم أو التاريخ داخل العبارة واترك بقيتها كما هي.',
  deadline: 'صحح عدد الأيام أو التاريخ كما في الصورة.',
};

/** بطاقة فحص واحد. onDone تُستدعى بعد الحسم بعدد الحقول المعلقة المتبقي. */
export function checkCard(item, { onDone, showDoc = true } = {}) {
  const k = item.check;
  const input = h(k.field === 'reference' || k.field === 'subject' ? 'textarea' : 'input', { value: k.spanText, dir: 'auto', 'aria-label': 'القراءة', class: 'review-input', rows: 2 });
  const err = h('p', { class: 'error', role: 'alert' });
  const confirmBtn = h('button', { class: 'btn primary sm', type: 'button' }, icon('check'), 'القراءة صحيحة');
  const saveBtn = h('button', { class: 'btn sm', type: 'button', disabled: true }, icon('edit'), 'حفظ التصحيح');
  const card = h('article', { class: 'review-card card', tabindex: -1 });
  const changed = () => input.value.trim() !== k.spanText.trim();
  // حقل لم يُقرأ أصلاً لا يُؤكَّد؛ يُكتب من الصورة.
  const empty = !k.spanText.trim();
  if (empty) { confirmBtn.disabled = true; input.placeholder = 'لم يُقرأ؛ اكتبه كما في الصورة'; }
  input.addEventListener('input', () => { saveBtn.disabled = !changed(); confirmBtn.disabled = empty || changed(); err.textContent = ''; });
  async function send(action) {
    confirmBtn.disabled = saveBtn.disabled = true;
    try {
      const r = await api('/review/' + k.id, { method: 'POST', body: { action, value: input.value } });
      card.classList.add('done');
      mount(card, h('div', { class: 'row', style: 'padding:14px 18px' }, icon('check'), h('b', { text: action === 'confirm' ? 'أُكِّدت القراءة' : 'حُفظ التصحيح' }),
        h('span', { class: 'muted small', text: `${k.fieldLabel}${showDoc ? ' • ' + item.document.archiveNo : ''}` })));
      onDone?.(r.pending, card);
    } catch (e) { err.textContent = e.message; confirmBtn.disabled = empty || changed(); saveBtn.disabled = !changed(); }
  }
  confirmBtn.onclick = () => send('confirm');
  saveBtn.onclick = () => send('correct');
  input.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); if (changed()) send('correct'); else if (!empty) send('confirm'); }
  });
  const others = (k.candidates || []).filter(c => c && c !== k.value);
  mount(card,
    h('div', { class: 'review-head' },
      h('span', { class: 'chip warn' }, icon('eye'), k.fieldLabel),
      showDoc ? h('a', { href: '#/doc/' + encodeURIComponent(item.document.id), class: 'small', style: 'font-weight:600' }, h('span', { class: 'num', text: item.document.archiveNo }), ' — ', item.document.title || 'بلا عنوان') : null,
      h('span', { class: 'muted small', style: 'margin-inline-start:auto', text: k.reason })),
    k.hasCrop ? h('div', { class: 'crop-wrap' }, h('img', { class: 'crop', src: `/api/review/${k.id}/crop`, alt: 'مقتطع الحقل من الصورة الممسوحة', loading: 'lazy' }))
      : h('p', { class: 'muted small', style: 'padding:0 18px', text: 'لا يوجد مقتطع؛ قارن بالأصل.' }),
    h('div', { class: 'review-body' },
      h('label', { class: 'f', text: 'القراءة الآلية (عدّلها إن خالفت الصورة)' }), input,
      h('p', { class: 'muted small', text: HINT[k.field] || '' }),
      others.length ? h('div', { class: 'chips', style: 'margin-top:6px' }, h('span', { class: 'muted small', text: 'قراءات أخرى:' }), others.map(c => h('span', { class: 'chip', text: c }))) : null,
      item.suggestions?.length ? h('div', { class: 'suggest' }, h('b', { class: 'small', text: 'مستندات في الأرشيف قد تكون المقصودة:' }),
        item.suggestions.map(s => h('a', { class: 'ref-item', style: 'display:block', href: '#/doc/' + encodeURIComponent(s.id), target: '_blank' },
          h('span', { class: 'chip ' + (s.exact ? 'ok' : ''), text: s.exact ? 'رقم مطابق' : 'يختلف بحرف' }), ' ', h('b', { class: 'num', text: s.number }),
          h('div', { class: 'small', text: `${s.archiveNo} — ${s.title}${s.documentDate ? ' • ' + fmt.both(s.documentDate) : ''}` })))) : null,
      h('div', { class: 'row', style: 'margin-top:10px;gap:8px' }, confirmBtn, saveBtn, err)));
  return card;
}

export async function renderReview(view, onCount) {
  const data = await api('/review');
  const list = h('div', { class: 'stack' });
  let left = data.items.length;
  const counter = h('span', { class: 'num', text: nf.format(left) });
  const done = (pending, card) => {
    left--; counter.textContent = nf.format(left); onCount?.(pending);
    const next = card.nextElementSibling;
    if (next) next.querySelector('.review-input')?.focus();
    if (!left) mount(list, empty('check', 'لا حقول بانتظار التحقق. كل القراءات الآلية مؤكدة.'));
  };
  if (!data.items.length) mount(list, empty('check', 'لا حقول بانتظار التحقق. كل القراءات الآلية مؤكدة.'));
  else mount(list, data.items.map(item => checkCard(item, { onDone: done })));
  mount(view,
    pageHead('التحقق من القراءة', 'حقول قرأها التعرف الضوئي ولم تتفق قراءاتها أو انخفضت ثقتها. لا تُبنى عليها روابط ولا مهل ولا تنبيهات حتى تُحسم.'),
    h('div', { class: 'card pad', style: 'margin-bottom:14px' },
      h('div', { class: 'row', style: 'gap:10px;flex-wrap:wrap' }, icon('eye'), h('b', {}, 'بانتظار التحقق: ', counter),
        h('span', { class: 'muted small', text: 'قارن القراءة بالمقتطع: Enter يؤكدها كما هي، أو عدّلها ثم Enter لحفظ التصحيح.' }),
        data.hidden ? h('span', { class: 'chip lock' }, icon('shield'), `${nf.format(data.hidden)} في مستندات حساسة`) : null)),
    list);
  list.querySelector('.review-input')?.focus({ preventScroll: true });
}
