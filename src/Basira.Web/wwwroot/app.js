'use strict';
/* بصيرة — واجهة البوابة. كل النصوص الواردة من الخادم تُعرض عبر textContent فقط (لا innerHTML). */
const $ = (s, r = document) => r.querySelector(s);
const state = { me: null, filter: 'all', category: '', query: '', categories: [] };
const FILTERS = [['all', 'الكل'], ['review', 'قيد المراجعة'], ['overdue', 'متأخرة'], ['week', 'هذا الأسبوع'], ['ocr', 'بلا نص'], ['sensitive', 'حساسة']];
const nf = new Intl.NumberFormat('ar-SA-u-nu-latn');

function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(props || {})) {
    if (v == null || v === false) continue;
    if (k === 'class') el.className = v;
    else if (k === 'text') el.textContent = v;
    else if (k === 'style') el.style.cssText = v; /* CSSOM مسموح بها مع CSP بخلاف سمة style المضمّنة */
    else if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
    else el.setAttribute(k, v === true ? '' : v);
  }
  for (const kid of kids.flat()) if (kid != null && kid !== false) el.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  return el;
}
/* replaceChildren تحوّل المصفوفات وnull إلى نصوص؛ هذه الدالة تسطّحها وتتجاهل الفارغ. */
const mount = (el, ...kids) => el.replaceChildren(...kids.flat().filter(k => k != null && k !== false && k !== ''));
const svg = (tag, attrs, ...kids) => {
  const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
  for (const [k, v] of Object.entries(attrs || {})) el.setAttribute(k, v);
  kids.forEach(k => el.append(k));
  return el;
};

let toastTimer;
function toast(msg) {
  const t = $('#toast'); t.textContent = msg; t.classList.add('show');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.remove('show'), 3200);
}

async function api(path, opts = {}) {
  const init = { credentials: 'same-origin', ...opts, headers: { 'X-Requested-With': 'basira', ...(opts.body ? { 'Content-Type': 'application/json' } : {}), ...opts.headers } };
  const res = await fetch('/api' + path, init);
  if (res.status === 401 && path !== '/login') { showLogin(); throw new Error('auth'); }
  const data = res.headers.get('content-type')?.includes('json') ? await res.json() : null;
  if (!res.ok) throw Object.assign(new Error(data?.error || 'تعذر تنفيذ الطلب.'), { status: res.status });
  return data;
}

/* ---- تطبيع عربي مطابق لمحرك البحث لتمييز المطابقات في النص الأصلي ---- */
function normMap(text) {
  let out = ''; const map = [];
  for (let i = 0; i < text.length; i++) {
    let c = text[i]; const n = c.charCodeAt(0);
    if ((n >= 0x64b && n <= 0x65f) || n === 0x670 || n === 0x640) continue;
    if ('أإآٱ'.includes(c)) c = 'ا'; else if (c === 'ى') c = 'ي'; else if (c === 'ة') c = 'ه';
    out += c.toLowerCase(); map.push(i);
  }
  return { out, map };
}
function highlight(text, terms) {
  const frag = document.createDocumentFragment();
  const list = (terms || '').split('،').map(s => normMap(s.trim()).out).filter(s => s.length > 1);
  if (!text || !list.length) { frag.append(text || ''); return frag; }
  const { out, map } = normMap(text); const ranges = [];
  for (const t of list) { let i = out.indexOf(t); while (i !== -1) { ranges.push([map[i], map[i + t.length - 1] + 1]); i = out.indexOf(t, i + t.length); } }
  ranges.sort((a, b) => a[0] - b[0]);
  let pos = 0;
  for (const [s, e] of ranges) { if (e <= pos) continue; const st = Math.max(s, pos); frag.append(text.slice(pos, st)); frag.append(h('mark', { text: text.slice(st, e) })); pos = e; }
  frag.append(text.slice(pos)); return frag;
}

/* ---- التنقل ---- */
const ROUTES = [
  ['pulse', 'نبض الأرشيف', renderPulse], ['docs', 'المستندات', renderDocs], ['ask', 'اسأل بصيرة', renderAsk],
  ['audit', 'سجل التدقيق', renderAudit, 'audit'],
];
function renderNav(current) {
  const nav = $('#nav'); nav.replaceChildren();
  for (const [id, label, , perm] of ROUTES) {
    if (perm && !state.me.permissions.includes(perm)) continue;
    nav.append(h('a', { href: '#/' + id, text: label, 'aria-current': id === current ? 'page' : null }));
  }
}
async function route() {
  if (!state.me) return;
  const id = (location.hash.match(/^#\/(\w+)/) || [])[1] || 'pulse';
  const r = ROUTES.find(x => x[0] === id) || ROUTES[0];
  renderNav(r[0]); document.title = r[1] + ' | بصيرة';
  const view = $('#view'); view.replaceChildren(h('div', { class: 'grid' }, h('div', { class: 'skeleton' }), h('div', { class: 'skeleton' })));
  try { const node = await r[2](); view.replaceChildren(node); }
  catch (e) { if (e.message !== 'auth') view.replaceChildren(h('div', { class: 'card empty', text: e.message })); }
  view.focus({ preventScroll: true });
}
window.addEventListener('hashchange', route);

/* ---- الدخول ---- */
function showLogin() {
  state.me = null; $('#app').hidden = true; $('#login').hidden = false; $('#u').focus();
}
async function boot() {
  try { state.me = await api('/me'); } catch { return showLogin(); }
  $('#login').hidden = true; $('#app').hidden = false; $('#who-name').textContent = state.me.fullName + ' · ' + state.me.role;
  try { state.categories = await api('/categories'); } catch { /* غير حرج */ }
  route();
}
$('#login-form').addEventListener('submit', async e => {
  e.preventDefault(); const err = $('#login-error'); err.textContent = '';
  const btn = e.submitter; btn.disabled = true;
  try {
    await api('/login', { method: 'POST', body: JSON.stringify({ userName: $('#u').value, password: $('#p').value }) });
    $('#p').value = ''; await boot();
  } catch (ex) { err.textContent = ex.message; } finally { btn.disabled = false; }
});
$('#logout').addEventListener('click', async () => { try { await api('/logout', { method: 'POST' }); } catch { /* تجاهل */ } location.hash = ''; showLogin(); });

/* ---- الصفحات ---- */
const head = (title, sub, ...actions) => h('div', { class: 'page-head' }, h('div', {}, h('h1', { text: title }), sub && h('p', { text: sub })), h('div', {}, actions));

async function renderPulse() {
  const d = await api('/pulse'); const m = d.metrics;
  const color = m.score >= 80 ? 'var(--teal)' : m.score >= 50 ? 'var(--gold)' : 'var(--red)';
  const verdict = m.total === 0 ? 'لا توجد مستندات بعد' : m.score >= 80 ? 'أرشيف في حالة جيدة' : m.score >= 50 ? 'يحتاج إلى متابعة' : 'يحتاج إلى تدخل';
  const C = 2 * Math.PI * 54;
  const ring = svg('svg', { class: 'ring', viewBox: '0 0 132 132', role: 'img', 'aria-label': 'مؤشر الصحة ' + m.score + ' بالمئة' },
    svg('circle', { cx: 66, cy: 66, r: 54, fill: 'none', stroke: 'var(--soft)', 'stroke-width': 12 }),
    svg('circle', { cx: 66, cy: 66, r: 54, fill: 'none', stroke: color, 'stroke-width': 12, 'stroke-linecap': 'round', 'stroke-dasharray': `${C * m.score / 100} ${C}`, transform: 'rotate(90 66 66) scale(1 -1) translate(0 -132)' }),
    (() => { const t = svg('text', { x: 66, y: 76, 'text-anchor': 'middle' }); t.textContent = m.score; return t; })());
  const kpi = (label, value, warn) => h('div', { class: 'card kpi' + (warn && value > 0 ? ' warn' : '') }, h('small', { text: label }), h('b', { text: nf.format(value) }));
  const bars = (title, rows) => h('section', { class: 'card' }, h('h2', { text: title }),
    h('div', { class: 'bars' }, rows.length ? rows.map(r => h('div', { class: 'bar-row' }, h('span', { text: r.name }), h('div', { class: 'bar' }, h('i', { style: `width:${Math.max(3, r.count * 100 / (m.total || 1))}%` })), h('span', { class: 'num', text: nf.format(r.count) }))) : h('p', { class: 'empty', text: 'لا توجد بيانات' })));
  const pct = (label, v) => h('div', { class: 'card kpi' }, h('small', { text: label }), h('b', { text: v + '%' }));
  const sevText = s => s === 0 ? 'عاجل' : s === 1 ? 'قريب' : 'مراجعة';
  const rows = d.findings.map(f => h('tr', { class: 'click', tabindex: 0, onclick: () => openDoc(f.documentId), onkeydown: e => e.key === 'Enter' && openDoc(f.documentId) },
    h('td', {}, h('span', { class: 'pill s' + Math.min(f.severity, 2), text: f.kind })), h('td', { text: f.title }), h('td', { text: f.reason }), h('td', {}, h('span', { class: 'pill', text: sevText(f.severity) }))));
  const reportBtn = state.me.permissions.includes('export') ? h('a', { class: 'btn', href: '/api/report', download: '', text: 'تقرير الصحة (HTML)' }) : null;
  return h('div', {},
    head('نبض الأرشيف', 'ما يحتاج انتباهك الآن، محسوباً من سجلاتك الفعلية.', reportBtn),
    h('section', { class: 'card hero' }, ring, h('div', {}, h('h2', { text: verdict }), h('p', { text: m.total ? `يعتمد المؤشر على اكتمال النص والتصنيف مع خصم للمواعيد المتأخرة والنسخ المطابقة. آخر تحديث للفهرس: ${new Date(d.loadedAt).toLocaleTimeString('ar-SA-u-nu-latn')}.` : 'ابدأ بإدراج مستندات من التطبيق المكتبي لتظهر المؤشرات هنا.' }))),
    h('div', { class: 'grid kpis' }, kpi('إجمالي المستندات', m.total), pct('نصوص قابلة للبحث', m.textPercent), pct('مصنّفة', m.categoryPercent), kpi('مواعيد متأخرة', m.overdue, true), kpi('مستندات حساسة', m.sensitive), kpi('نسخ مطابقة', m.duplicates)),
    h('div', { class: 'grid two', style: 'margin-top:14px' }, bars('حسب التصنيف', d.categories), bars('حسب الحالة', d.statuses)),
    h('section', { class: 'card', style: 'margin-top:14px;padding:14px 8px' }, h('h2', { style: 'padding:0 12px 6px', text: `ما يحتاج انتباهاً (${nf.format(d.findingsTotal)})` }),
      rows.length ? h('div', { class: 'tablewrap' }, h('table', {}, h('thead', {}, h('tr', {}, ['النوع', 'المستند', 'السبب', 'الأولوية'].map(t => h('th', { text: t })))), h('tbody', {}, rows))) : h('p', { class: 'empty', text: 'لا توجد تنبيهات. الأرشيف نظيف.' })));
}

function docCard(it) {
  const meta = [it.archiveNumber, it.category, it.status, it.source && 'من: ' + it.source, it.due && 'الموعد: ' + it.due].filter(Boolean);
  return h('button', { class: 'doc', type: 'button', onclick: () => openDoc(it.id) },
    h('h3', {}, highlight(it.title, it.matches)),
    h('div', { class: 'meta' }, meta.map(x => h('span', { text: x })), it.sensitive && h('span', { class: 'pill lock', text: it.restricted ? 'محتوى مقيّد' : 'حساس' }), !it.hasText && h('span', { class: 'pill s1', text: 'بلا نص' })),
    it.excerpt && h('p', { class: 'excerpt' }, highlight(it.excerpt, it.matches)));
}
let searchCtl;
async function renderDocs() {
  const list = h('div', { class: 'list', 'aria-live': 'polite' });
  const status = h('p', { class: 'meta', 'aria-live': 'polite' });
  async function run() {
    searchCtl?.abort(); searchCtl = new AbortController();
    const qs = new URLSearchParams({ q: state.query, category: state.category, filter: state.filter, limit: '50' });
    try {
      const d = await api('/documents?' + qs, { signal: searchCtl.signal });
      status.textContent = d.items.length ? `${nf.format(d.items.length)} نتيجة` : '';
      list.replaceChildren(...(d.items.length ? d.items.map(docCard) : [h('div', { class: 'card empty', text: state.query ? 'لا نتائج مطابقة. جرّب كلمة أخرى أو رقم المعاملة.' : 'لا توجد مستندات ضمن هذا التصفية.' })]));
    } catch (e) { if (e.name !== 'AbortError' && e.message !== 'auth') list.replaceChildren(h('div', { class: 'card empty', text: e.message })); }
  }
  let t; const input = h('input', { class: 'grow', type: 'search', placeholder: 'ابحث بالعنوان أو الكلمات أو رقم المعاملة…', 'aria-label': 'بحث', value: state.query, maxlength: 200,
    oninput: e => { state.query = e.target.value; clearTimeout(t); t = setTimeout(run, 250); } });
  const cat = h('select', { 'aria-label': 'التصنيف', onchange: e => { state.category = e.target.value; run(); } }, h('option', { value: '', text: 'كل التصنيفات' }), state.categories.map(c => h('option', { value: c, text: c, selected: c === state.category })));
  const chips = h('div', { class: 'chips', role: 'group', 'aria-label': 'التصفية' });
  const drawChips = () => chips.replaceChildren(...FILTERS.map(([k, l]) => h('button', { class: 'chip', type: 'button', 'aria-pressed': String(state.filter === k), text: l, onclick: () => { state.filter = k; drawChips(); run(); } })));
  drawChips(); run();
  return h('div', {}, head('المستندات', 'بحث عربي بالأدلة، يتجاهل التشكيل والتطويل ويفهم صيغ الجمع الشائعة.'), h('div', { class: 'toolbar' }, input, cat), chips, status, list);
}

async function renderAsk() {
  const out = h('div', { 'aria-live': 'polite' });
  const input = h('input', { class: 'grow', type: 'search', placeholder: 'مثال: ما شروط عقد الصيانة؟', 'aria-label': 'سؤال', maxlength: 300 });
  async function ask() {
    const q = input.value.trim(); if (!q) return;
    out.replaceChildren(h('div', { class: 'skeleton' }));
    try {
      const d = await api('/ask?' + new URLSearchParams({ q }));
      if (!d.found) return out.replaceChildren(h('div', { class: 'card empty', text: 'لم أجد نصوصاً مناسبة. جرّب اسم الجهة أو رقم المعاملة، أو تأكد من استخراج نص المستند.' }));
      out.replaceChildren(h('p', { class: 'meta', text: 'مقتطفات أصلية مرتبطة بسؤالك. افتح أي مصدر للتحقق من سياقه الكامل.' }),
        ...d.evidence.map(e => h('div', { class: 'src' },
          h('b', { text: `[${e.number}] ${e.title}` }), h('span', { class: 'meta', text: ' ' + (e.reference || '') }),
          e.restricted ? h('p', { class: 'q' }, h('span', { class: 'pill lock', text: 'مصدر حساس — المحتوى مقيّد لدورك' })) : h('p', { class: 'q' }, highlight(e.quote, q.split(/\s+/).join('،'))),
          h('button', { class: 'btn ghost', type: 'button', text: 'فتح المستند', onclick: () => openDoc(e.documentId) }))));
    } catch (e) { if (e.message !== 'auth') out.replaceChildren(h('div', { class: 'card empty', text: e.message })); }
  }
  input.addEventListener('keydown', e => e.key === 'Enter' && ask());
  return h('div', {}, head('اسأل بصيرة', 'إجابات من نصوص مستنداتك المحلية فقط، مع رقم مصدر لكل مقتطف؛ لا شيء يُرسل إلى خدمات خارجية.'),
    h('div', { class: 'toolbar' }, input, h('button', { class: 'btn primary', type: 'button', text: 'ابحث عن الأدلة', onclick: ask })), out);
}

async function renderAudit() {
  const d = await api('/audit?take=100');
  const rows = d.entries.map(e => h('tr', {}, h('td', { class: 'num', text: String(e.sequence) }), h('td', { text: new Date(e.timestampUtc).toLocaleString('ar-SA-u-nu-latn') }), h('td', { text: e.actor }), h('td', { text: e.action }), h('td', { text: e.detail })));
  return h('div', {}, head('سجل التدقيق', 'كل دخول وبحث وعرض مستند يُسجَّل في سلسلة تجزئة؛ أي تعديل لاحق على السجل يُكتشف.'),
    h('section', { class: 'card', style: 'margin-bottom:14px' }, h('span', { class: 'pill ' + (d.valid ? 'ok' : 's0'), text: d.valid ? 'السلسلة سليمة' : 'السلسلة مكسورة' }), ' ', h('span', { text: d.message + ` (${nf.format(d.count)} سجل)` }),
      h('p', { class: 'meta', style: 'direction:ltr;text-align:right;word-break:break-all', text: 'آخر تجزئة: ' + d.lastHash })),
    h('section', { class: 'card', style: 'padding:14px 8px' }, rows.length ? h('div', { class: 'tablewrap' }, h('table', {}, h('thead', {}, h('tr', {}, ['#', 'الوقت', 'المستخدم', 'الإجراء', 'التفاصيل'].map(t => h('th', { text: t })))), h('tbody', {}, rows))) : h('p', { class: 'empty', text: 'لا توجد سجلات.' })));
}

/* ---- تفاصيل المستند ---- */
let lastFocus;
async function openDoc(id) {
  lastFocus = document.activeElement;
  const dr = $('#drawer'), body = $('#drawer-body'); dr.hidden = false; body.replaceChildren(h('div', { class: 'skeleton' })); $('#drawer-close').focus();
  try {
    const d = await api('/documents/' + encodeURIComponent(id)); const s = d.summary;
    const kv = [['الرقم', s.archiveNumber], ['المرجع', s.referenceNumber], ['التصنيف', [s.category, s.subCategory].filter(Boolean).join(' / ')], ['الحالة', s.status], ['الجهة المصدِّرة', s.source], ['الجهة المستلمة', s.destination], ['الموعد', s.due], ['السرية', s.confidentiality], ['الوسوم', s.tags]].filter(x => x[1]);
    mount(body, h('h2', { id: 'drawer-title', text: s.title }),
      h('div', { class: 'meta' }, s.sensitive && h('span', { class: 'pill lock', text: d.restricted ? 'محتوى مقيّد لدورك' : 'حساس' }), !s.hasText && h('span', { class: 'pill s1', text: 'بلا نص مستخرج' })),
      s.summary && h('p', { text: s.summary }),
      h('dl', { class: 'kv' }, kv.flatMap(([k, v]) => [h('dt', { text: k }), h('dd', { text: v })])),
      d.entities && h('p', { class: 'meta', text: 'كيانات: ' + d.entities }),
      d.references?.length ? [h('h3', { text: 'الإشارات في نص المستند', style: 'margin:16px 0 6px' }), h('div', { class: 'refs' }, d.references.map(refItem))] : null,
      d.restricted ? h('div', { class: 'card empty', text: 'نص هذا المستند الحساس متاح للمراجعين ومديري الأرشيف فقط.' }) : d.text ? [h('h3', { text: 'النص المستخرج', style: 'margin:14px 0 6px' }), h('div', { class: 'textbox' }, markSpans(d.text, d.references || []))] : null,
      d.links.length ? [h('h3', { text: 'مستندات مرتبطة', style: 'margin:18px 0 6px' }), h('div', { class: 'list' }, d.links.map(l => h('button', { class: 'doc', type: 'button', onclick: () => openDoc(l.id) }, h('b', { text: l.title }), h('div', { class: 'meta' }, h('span', { text: l.archiveNumber }), h('span', { text: l.reason })))))] : null);
  } catch (e) { if (e.message !== 'auth') body.replaceChildren(h('div', { class: 'card empty', text: e.message })); }
}
const gregorian = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { year: 'numeric', month: 'long', day: 'numeric' });
function refItem(r) {
  const when = r.date ? (r.calendar === 'هجري' ? `${r.dateText} (الموافق ${gregorian.format(new Date(r.date))})` : r.dateText) : (r.dateText ? r.dateText + ' (تاريخ غير صالح)' : 'دون تاريخ');
  return h('div', { class: 'ref' },
    h('div', { class: 'meta' }, h('span', { class: 'pill ok', text: r.kindLabel }), h('b', { text: `${r.documentType ? r.documentType + ' ' : ''}رقم ${r.rawNumber}` }), h('span', { text: when }), h('span', { text: r.ownerLabel })),
    h('p', { class: 'q', text: '«' + r.evidence + '»' }));
}
/* يميّز مواضع الإشارات داخل النص الأصلي اعتماداً على مواضعها المحسوبة في الخادم. */
function markSpans(text, refs) {
  const frag = document.createDocumentFragment(); let pos = 0;
  for (const r of [...refs].sort((a, b) => a.offset - b.offset)) {
    if (r.offset < pos || r.offset + r.length > text.length) continue;
    frag.append(text.slice(pos, r.offset), h('mark', { text: text.slice(r.offset, r.offset + r.length), title: r.kindLabel }));
    pos = r.offset + r.length;
  }
  frag.append(text.slice(pos)); return frag;
}
function closeDrawer() { $('#drawer').hidden = true; lastFocus?.focus?.(); }
$('#drawer-close').addEventListener('click', closeDrawer);
$('#drawer').addEventListener('click', e => { if (e.target.id === 'drawer') closeDrawer(); });
document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && !$('#drawer').hidden) closeDrawer();
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k' && state.me) { e.preventDefault(); location.hash = '#/docs'; setTimeout(() => $('#view input')?.focus(), 150); }
});

boot();
