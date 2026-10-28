// بصيرة — الوحدة الأساسية: بناء العناصر بأمان (textContent فقط)، الأيقونات، الاتصال بالخادم، التواريخ، والأحداث الحية.
export const state = { me: null, org: '', meta: null, jobs: new Map(), listeners: new Set() };

export function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(props || {})) {
    if (v == null || v === false) continue;
    if (k === 'class') el.className = v;
    else if (k === 'text') el.textContent = v;
    else if (k === 'style') el.style.cssText = v;
    else if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
    else if (k === 'value') el.value = v;
    else el.setAttribute(k, v === true ? '' : v);
  }
  append(el, kids);
  return el;
}

export function append(el, kids) {
  for (const k of kids.flat(Infinity)) {
    if (k == null || k === false || k === '') continue;
    el.append(k.nodeType ? k : document.createTextNode(String(k)));
  }
  return el;
}

export const mount = (el, ...kids) => { el.replaceChildren(); return append(el, kids); };

const NS = 'http://www.w3.org/2000/svg';
export function s(tag, attrs, ...kids) {
  const el = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs || {})) el.setAttribute(k, v);
  for (const k of kids) if (k) el.append(k.nodeType ? k : document.createTextNode(String(k)));
  return el;
}

const ICONS = {
  home: [['p', 'M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z']],
  thread: [['c', 6, 18, 2.5], ['c', 18, 6, 2.5], ['c', 6, 6, 2.5], ['p', 'M6 8.5v7'], ['p', 'M18 8.5a9 9 0 0 1-9.5 9']],
  docs: [['p', 'M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z'], ['p', 'M14 3v5h5'], ['p', 'M9 13h6'], ['p', 'M9 17h4']],
  upload: [['p', 'M12 16V4'], ['p', 'M7 9l5-5 5 5'], ['p', 'M4 16v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3']],
  search: [['c', 11, 11, 7], ['p', 'M20 20l-3.5-3.5']],
  spark: [['p', 'M12 3l1.8 4.7 4.7 1.8-4.7 1.8L12 16l-1.8-4.7L5.5 9.5l4.7-1.8z'], ['p', 'M19 15l.7 1.8 1.8.7-1.8.7L19 20l-.7-1.8-1.8-.7 1.8-.7z']],
  sliders: [['p', 'M4 6h9'], ['p', 'M17 6h3'], ['c', 15, 6, 2], ['p', 'M4 12h3'], ['p', 'M11 12h9'], ['c', 9, 12, 2], ['p', 'M4 18h11'], ['p', 'M19 18h1'], ['c', 17, 18, 2]],
  clock: [['c', 12, 12, 9], ['p', 'M12 7v5l3 2']],
  alert: [['p', 'M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z'], ['p', 'M12 9v4'], ['p', 'M12 17h.01']],
  camera: [['p', 'M4 8h3l2-3h6l2 3h3a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z'], ['c', 12, 13, 4]],
  check: [['p', 'M5 12l5 5L20 7']],
  logout: [['p', 'M15 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4'], ['p', 'M10 17l-5-5 5-5'], ['p', 'M5 12h11']],
  moon: [['p', 'M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z']],
  sun: [['c', 12, 12, 4], ['p', 'M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4']],
  link: [['p', 'M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.7 1.7'], ['p', 'M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7l1.7-1.7']],
  ghost: [['p', 'M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z'], ['p', 'M10 12a2 2 0 1 1 2.6 1.9c-.4.2-.6.5-.6.9V16'], ['p', 'M12 19h.01']],
  inbound: [['p', 'M12 4v14'], ['p', 'M6 12l6 6 6-6']],
  outbound: [['p', 'M12 20V6'], ['p', 'M6 12l6-6 6 6']],
  internal: [['p', 'M4 12h16'], ['p', 'M8 8l-4 4 4 4'], ['p', 'M16 8l4 4-4 4']],
  shield: [['p', 'M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z'], ['p', 'M9 12l2 2 4-4']],
  users: [['c', 9, 8, 3.5], ['p', 'M2.5 20a6.5 6.5 0 0 1 13 0'], ['p', 'M16 4.5a3.5 3.5 0 0 1 0 7'], ['p', 'M21.5 20a6.5 6.5 0 0 0-4-6']],
  eye: [['p', 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z'], ['c', 12, 12, 3]],
  edit: [['p', 'M4 20h4L19 9l-4-4L4 16z'], ['p', 'M13.5 6.5l4 4']],
  refresh: [['p', 'M20 11a8 8 0 1 0-2.3 5.7'], ['p', 'M20 4v7h-7']],
  download: [['p', 'M12 4v12'], ['p', 'M7 11l5 5 5-5'], ['p', 'M4 20h16']],
  flag: [['p', 'M5 21V4'], ['p', 'M5 4h12l-2.5 4.5L17 13H5']],
  message: [['p', 'M4 5h16v11H9l-5 4z']],
  plus: [['p', 'M12 5v14'], ['p', 'M5 12h14']],
  x: [['p', 'M6 6l12 12'], ['p', 'M18 6L6 18']],
  calendar: [['r', 3, 5, 18, 16, 2], ['p', 'M3 10h18'], ['p', 'M8 3v4'], ['p', 'M16 3v4']],
  history: [['p', 'M3 12a9 9 0 1 0 3-6.7'], ['p', 'M3 4v5h5'], ['p', 'M12 8v4l3 2']],
  folder: [['p', 'M3 6a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z']],
  report: [['p', 'M4 20V10'], ['p', 'M10 20V4'], ['p', 'M16 20v-7'], ['p', 'M22 20H2']],
  arrow: [['p', 'M19 12H5'], ['p', 'M11 18l-6-6 6-6']],
  trash: [['p', 'M4 7h16'], ['p', 'M10 11v6'], ['p', 'M14 11v6'], ['p', 'M6 7l1 13h10l1-13'], ['p', 'M9 7V4h6v3']],
  restore: [['p', 'M3 12a9 9 0 1 0 3-6.7'], ['p', 'M3 4v5h5']],
  scanner: [['r', 3, 12, 18, 7, 2], ['p', 'M5 12l12-7'], ['p', 'M7 16h4']],
  key: [['c', 8, 15, 4], ['p', 'M11 12l9-9'], ['p', 'M17 6l3 3']],
  archive: [['r', 3, 4, 18, 4, 1], ['p', 'M5 8v11h14V8'], ['p', 'M10 12h4']],
  table: [['r', 3, 4, 18, 16, 2], ['p', 'M3 10h18'], ['p', 'M9 10v10']],
  user: [['c', 12, 8, 4], ['p', 'M4 21a8 8 0 0 1 16 0']],
  network: [['c', 12, 12, 3], ['c', 4.5, 6, 2], ['c', 19.5, 6, 2], ['c', 5, 19, 2], ['c', 19, 18.5, 2], ['p', 'M9.6 10.4L6.2 7.3'], ['p', 'M14.4 10.4l3.4-3.1'], ['p', 'M9.8 14.1L6.5 17.6'], ['p', 'M14.3 14l3.2 3.2']],
};

export function icon(name, cls = '') {
  const el = s('svg', { class: 'i ' + cls, viewBox: '0 0 24 24', 'aria-hidden': 'true' });
  for (const d of ICONS[name] || []) {
    if (d[0] === 'p') el.append(s('path', { d: d[1] }));
    else if (d[0] === 'c') el.append(s('circle', { cx: d[1], cy: d[2], r: d[3] }));
    else if (d[0] === 'r') el.append(s('rect', { x: d[1], y: d[2], width: d[3], height: d[4], rx: d[5] }));
  }
  return el;
}

export class ApiError extends Error { constructor(msg, status) { super(msg); this.status = status; } }

export async function api(path, { method = 'GET', body, form, signal } = {}) {
  const headers = { 'X-Requested-With': 'basira' };
  let payload;
  if (form) payload = form;
  else if (body !== undefined) { headers['Content-Type'] = 'application/json'; payload = JSON.stringify(body); }
  const res = await fetch('/api' + path, { method, headers, body: payload, signal, credentials: 'same-origin' });
  const isJson = (res.headers.get('content-type') || '').includes('json');
  const data = isJson ? await res.json() : null;
  if (res.status === 401 && path !== '/login' && path !== '/me') { window.dispatchEvent(new Event('basira:logout')); throw new ApiError('انتهت الجلسة.', 401); }
  if (!res.ok) throw new ApiError(data?.error || 'تعذر تنفيذ الطلب.', res.status);
  return data;
}

// ===== التواريخ =====
const hijriFmt = new Intl.DateTimeFormat('ar-SA-u-ca-islamic-umalqura-nu-latn', { day: 'numeric', month: 'long', year: 'numeric' });
const gregFmt = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { day: 'numeric', month: 'long', year: 'numeric' });
const timeFmt = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { hour: 'numeric', minute: '2-digit' });
export const nf = new Intl.NumberFormat('ar-SA-u-nu-latn');

export function parseDate(v) {
  if (!v) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v);
  const d = m ? new Date(+m[1], +m[2] - 1, +m[3]) : new Date(v);
  return isNaN(d) ? null : d;
}
export const fmt = {
  hijri: v => { const d = parseDate(v); return d ? hijriFmt.format(d).replace(' هـ', 'هـ') : ''; },
  greg: v => { const d = parseDate(v); return d ? gregFmt.format(d) : ''; },
  both: v => { const d = parseDate(v); return d ? `${hijriFmt.format(d).replace(' هـ', 'هـ')} — ${gregFmt.format(d)}` : ''; },
  time: v => { const d = parseDate(v); return d ? `${gregFmt.format(d)}، ${timeFmt.format(d)}` : ''; },
  ago: v => {
    const d = parseDate(v); if (!d) return '';
    const days = Math.round((startOfDay(new Date()) - startOfDay(d)) / 864e5);
    if (days === 0) return 'اليوم'; if (days === 1) return 'أمس'; if (days === -1) return 'غداً';
    if (days > 0) return `منذ ${nf.format(days)} ${days <= 10 ? 'أيام' : 'يوماً'}`;
    return `بعد ${nf.format(-days)} ${-days <= 10 ? 'أيام' : 'يوماً'}`;
  },
  days: v => { const d = parseDate(v); return d ? Math.round((startOfDay(d) - startOfDay(new Date())) / 864e5) : null; },
  size: b => b > 1048576 ? `${(b / 1048576).toFixed(1)} م.ب` : `${Math.max(1, Math.round(b / 1024))} ك.ب`,
};
function startOfDay(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }

// ===== تمييز مواضع داخل نص أصلي =====
export function highlight(text, ranges) {
  const frag = document.createDocumentFragment();
  let pos = 0;
  for (const r of [...ranges].sort((a, b) => a.offset - b.offset)) {
    if (r.offset < pos || r.offset + r.length > text.length) continue;
    frag.append(text.slice(pos, r.offset));
    const m = h('mark', { class: r.cls || '', title: r.title || null, tabindex: r.onclick ? 0 : null, onclick: r.onclick || null });
    m.textContent = text.slice(r.offset, r.offset + r.length);
    if (r.id) m.id = r.id;
    frag.append(m);
    pos = r.offset + r.length;
  }
  frag.append(text.slice(pos));
  return frag;
}

// تطبيع عربي (مطابق لمحرك البحث) لتمييز كلمات البحث في المقتطفات.
function normMap(t) {
  let out = ''; const map = [];
  for (let i = 0; i < t.length; i++) {
    let c = t[i]; const n = c.charCodeAt(0);
    if ((n >= 0x64b && n <= 0x65f) || n === 0x670 || n === 0x640) continue;
    if ('أإآٱ'.includes(c)) c = 'ا'; else if (c === 'ى') c = 'ي'; else if (c === 'ة') c = 'ه';
    out += c.toLowerCase(); map.push(i);
  }
  return { out, map };
}
export function markTerms(text, terms) {
  const list = (terms || '').split('،').map(x => normMap(x.trim()).out).filter(x => x.length > 1);
  if (!text || !list.length) return document.createTextNode(text || '');
  const { out, map } = normMap(text); const ranges = [];
  for (const t of list) { let i = out.indexOf(t); while (i !== -1) { ranges.push({ offset: map[i], length: map[i + t.length - 1] + 1 - map[i] }); i = out.indexOf(t, i + t.length); } }
  const merged = [];
  for (const r of ranges.sort((a, b) => a.offset - b.offset)) { const last = merged[merged.length - 1]; if (last && r.offset < last.offset + last.length) continue; merged.push(r); }
  return highlight(text, merged);
}

// ===== تنبيهات قصيرة =====
/**
 * نافذة تأكيد: عنوان ونص، ومع input حقل نصي (سبب مثلاً). تعيد قيمة الحقل (أو '') عند التأكيد، وnull عند الإلغاء.
 * danger: زر التأكيد بلون التحذير.
 */
export function confirmDialog({ title, message, confirm = 'تأكيد', danger = false, input = null }) {
  return new Promise(resolve => {
    const field = input ? h('textarea', { placeholder: input, rows: 2, style: 'margin-top:12px', 'aria-label': input }) : null;
    const close = v => { overlay.remove(); document.removeEventListener('keydown', onKey); resolve(v); };
    const onKey = e => { if (e.key === 'Escape') close(null); };
    const ok = h('button', { class: 'btn ' + (danger ? 'danger-solid' : 'primary'), type: 'button', onclick: () => close(field ? field.value.trim() : '') }, confirm);
    const overlay = h('div', { class: 'overlay', onclick: e => { if (e.target === overlay) close(null); } },
      h('div', { class: 'modal', role: 'alertdialog', 'aria-modal': 'true', 'aria-label': title },
        h('h3', { text: title }), h('p', { class: 'muted', style: 'margin-top:8px', text: message }), field,
        h('div', { class: 'row', style: 'gap:8px;justify-content:flex-end;margin-top:18px' }, h('button', { class: 'btn', type: 'button', onclick: () => close(null) }, 'إلغاء'), ok)));
    document.addEventListener('keydown', onKey);
    document.body.append(overlay);
    (field || ok).focus();
  });
}

export function toast(msg, opts = {}) {
  const t = h('div', { class: 'toast' + (opts.error ? ' err' : '') }, msg, opts.link ? [' ', h('a', { href: opts.link, style: 'color:inherit;text-decoration:underline', text: opts.linkText || 'فتح' })] : null,
    opts.action ? [' ', h('button', { class: 'toast-action', type: 'button', onclick: async () => { t.remove(); try { await opts.action.run(); } catch (e) { toast(e.message, { error: true }); } } }, opts.action.label)] : null);
  document.getElementById('toasts').append(t);
  setTimeout(() => t.remove(), opts.error ? 6000 : opts.action ? 8000 : 4200);
}

// ===== الأحداث الحية =====
let source;
export function connectEvents() {
  if (source) return;
  source = new EventSource('/api/events');
  source.onmessage = e => {
    let msg; try { msg = JSON.parse(e.data); } catch { return; }
    if (msg.type === 'job') state.jobs.set(msg.payload.id, msg.payload);
    for (const fn of state.listeners) fn(msg);
  };
}
export function disconnectEvents() { source?.close(); source = null; }
export function onEvent(fn) { state.listeners.add(fn); return () => state.listeners.delete(fn); }

export const can = p => !!state.me?.permissions?.includes(p);
export const dirClass = d => d === 'صادر' ? 'out' : d === 'داخلي' ? 'internal' : 'in';
export const dirIcon = d => icon(d === 'صادر' ? 'outbound' : d === 'داخلي' ? 'internal' : 'inbound');

export function statusChip(doc) {
  if (doc.closed) return h('span', { class: 'chip ok' }, icon('check'), doc.status);
  return h('span', { class: 'chip' }, doc.status);
}
export function dueChip(doc) {
  if (!doc.due || doc.closed) return null;
  const days = fmt.days(doc.due);
  if (days === null) return null;
  const cls = days < 0 ? 'danger' : days <= 3 ? 'warn' : '';
  const label = days < 0 ? `متأخر ${nf.format(-days)} يوماً` : days === 0 ? 'يستحق اليوم' : `يستحق بعد ${nf.format(days)} يوماً`;
  return h('span', { class: 'chip ' + cls, title: `${fmt.both(doc.due)}${doc.dueSource ? ' • ' + doc.dueSource : ''}` }, icon('clock'), label);
}
export function docChips(doc) {
  return [
    h('span', { class: 'chip ' + dirClass(doc.direction) }, doc.direction),
    statusChip(doc),
    dueChip(doc),
    doc.awaitingDays != null ? h('span', { class: 'chip warn' }, icon('alert'), `بلا رد منذ ${nf.format(doc.awaitingDays)} يوماً`) : null,
    doc.sensitive ? h('span', { class: 'chip lock' }, icon('shield'), doc.confidentiality) : null,
    doc.processing === 'queued' || doc.processing === 'processing' ? h('span', { class: 'chip' }, icon('refresh'), 'قيد المعالجة') : null,
    doc.processing === 'failed' ? h('span', { class: 'chip danger' }, 'تعذرت المعالجة') : null,
    doc.processing === 'needs_text' ? h('span', { class: 'chip warn' }, 'بلا نص') : null,
    doc.pendingChecks ? h('span', { class: 'chip warn', title: 'حقول قرأها التعرف الضوئي بانتظار تأكيد مراجع' }, icon('eye'), `${nf.format(doc.pendingChecks)} بانتظار التحقق`) : null,
    doc.scanIssues ? h('span', { class: 'chip danger', title: 'جودة المسح دون الحد المقبول' }, icon('alert'), 'مسح ضعيف') : null,
  ];
}
