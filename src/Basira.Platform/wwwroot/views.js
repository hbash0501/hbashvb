// بصيرة — الصفحات الرئيسية: لوحة القيادة، المعاملات، المستندات.
import { h, s, icon, api, fmt, nf, mount, highlight, markTerms, toast, can, state, dirClass, dirIcon, docChips, statusChip, dueChip, onEvent } from './core.js';
import { checkCard } from './review.js';
import { commitmentCard } from './commitments.js';

export const pageHead = (title, sub, ...actions) => h('div', { class: 'page-head' }, h('div', {}, h('h1', { text: title }), sub ? h('p', { text: sub }) : null), h('div', { class: 'actions' }, actions));
export const empty = (iconName, text, action) => h('div', { class: 'empty' }, icon(iconName), h('p', { text }), action ? h('div', { style: 'margin-top:12px' }, action) : null);
const docLink = id => '#/doc/' + encodeURIComponent(id);

// ======================= لوحة القيادة =======================
function ring(score) {
  const C = 2 * Math.PI * 26, color = score >= 80 ? 'var(--ok)' : score >= 50 ? 'var(--accent)' : 'var(--danger)';
  const t = s('text', { x: 32, y: 39, 'text-anchor': 'middle' }); t.textContent = score;
  return s('svg', { class: 'ring', viewBox: '0 0 64 64', role: 'img', 'aria-label': `مؤشر الصحة ${score}` },
    s('circle', { cx: 32, cy: 32, r: 26, fill: 'none', stroke: 'var(--line-2)', 'stroke-width': 7 }),
    s('circle', { cx: 32, cy: 32, r: 26, fill: 'none', stroke: color, 'stroke-width': 7, 'stroke-linecap': 'round', 'stroke-dasharray': `${C * score / 100} ${C}`, transform: 'rotate(-90 32 32)' }), t);
}

function activityChart(weeks) {
  const W = 640, H = 170, pad = 22, max = Math.max(1, ...weeks.map(w => Math.max(w.incoming, w.outgoing)));
  const bw = (W - pad * 2) / weeks.length;
  const svg = s('svg', { class: 'chart', viewBox: `0 0 ${W} ${H}`, preserveAspectRatio: 'none', role: 'img', 'aria-label': 'الوارد والصادر في آخر 12 أسبوعاً' });
  for (let i = 0; i <= 2; i++) svg.append(s('line', { x1: pad, x2: W - pad, y1: 10 + (H - 40) * i / 2, y2: 10 + (H - 40) * i / 2, stroke: 'var(--line-2)' }));
  weeks.forEach((w, i) => {
    const x = W - pad - (i + 1) * bw + bw * .18, bh = v => (H - 40) * v / max;
    const a = s('rect', { x, y: H - 30 - bh(w.incoming), width: bw * .3, height: bh(w.incoming), rx: 3, fill: 'var(--in)' });
    const b = s('rect', { x: x + bw * .34, y: H - 30 - bh(w.outgoing), width: bw * .3, height: bh(w.outgoing), rx: 3, fill: 'var(--out)' });
    const tt = s('title'); tt.textContent = `أسبوع ${fmt.greg(w.week)}: وارد ${w.incoming}، صادر ${w.outgoing}`;
    const g = s('g', {}, a, b, tt); svg.append(g);
    if (i % 3 === 0) { const t = s('text', { x: x + bw * .32, y: H - 10, 'text-anchor': 'middle' }); const d = new Date(w.week); t.textContent = `${d.getDate()}/${d.getMonth() + 1}`; svg.append(t); }
  });
  return svg;
}

const kpi = (href, iconName, label, value, sub, cls = '') => h('a', { class: 'card kpi ' + cls, href }, h('span', { class: 'label' }, icon(iconName), label), h('span', { class: 'value num', text: nf.format(value) }), sub ? h('span', { class: 'sub', text: sub }) : null);

export async function renderDashboard(view) {
  const d = await api('/dashboard');
  const now = new Date();
  const hour = now.getHours();
  const greet = hour < 12 ? 'صباح الخير' : 'مساء الخير';
  const name = (state.me.fullName || '').split(' ')[0];
  const awaiting = d.awaiting.length ? d.awaiting.map(a => h('a', { class: 'list-item', href: docLink(a.doc.id) },
    h('div', { class: 'badge-days' + (a.days >= d.replyDays * 2 ? ' danger' : '') }, nf.format(a.days), h('small', { text: 'يوماً' })),
    h('div', { class: 'grow' }, h('span', { class: 't', text: a.doc.title }), h('span', { class: 'muted small', text: [a.doc.sourceEntity || 'جهة غير محددة', a.doc.externalNo ? 'رقم ' + a.doc.externalNo : '', a.doc.documentDate ? fmt.hijri(a.doc.documentDate) : ''].filter(Boolean).join(' • ') })),
    can('import') ? h('span', { class: 'btn sm', role: 'link', tabindex: '0', title: 'فتح استوديو الرد', onclick: e => { e.preventDefault(); e.stopPropagation(); location.hash = '#/reply/' + encodeURIComponent(a.doc.id); },
      onkeydown: e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); location.hash = '#/reply/' + encodeURIComponent(a.doc.id); } } }, icon('edit'), 'صياغة الرد') : icon('arrow'))) : [empty('check', 'لا توجد خطابات واردة تنتظر الرد. كل وارد صدر ما يشير إليه.')];
  const due = d.dueSoon.length ? d.dueSoon.map(x => h('a', { class: 'list-item', href: docLink(x.doc.id) },
    h('div', { class: 'badge-days' + (x.days < 0 ? ' danger' : '') }, x.days < 0 ? nf.format(-x.days) : nf.format(x.days), h('small', { text: x.days < 0 ? 'متأخر' : x.days === 0 ? 'اليوم' : 'يوماً' })),
    h('div', { class: 'grow' }, h('span', { class: 't', text: x.doc.title }), h('span', { class: 'muted small', text: `${fmt.both(x.doc.due)}${x.doc.dueSource.startsWith('مستخرج') ? ' • من نص الخطاب' : ''}` })), icon('arrow'))) : [empty('calendar', 'لا مواعيد مستحقة خلال الأسبوع.')];
  const findings = d.findings.map(f => h('tr', { class: 'click', onclick: () => location.hash = docLink(f.documentId) },
    h('td', {}, h('span', { class: 'chip ' + (f.severity === 0 ? 'danger' : f.severity === 1 ? 'warn' : ''), text: f.kind })), h('td', { text: f.title }), h('td', { class: 'muted', text: f.reason })));
  const m = d.metrics;
  mount(view,
    h('div', { class: 'greet' }, h('h1', { text: `${greet}${name ? '، ' + name : ''}` }), h('p', { class: 'date', text: `${fmt.hijri(now.toISOString().slice(0, 10))} — ${fmt.greg(now.toISOString().slice(0, 10))}${state.org ? ' • ' + state.org : ''} • ${nf.format(d.total)} مستند، ${nf.format(d.open)} قيد العمل` })),
    h('div', { class: 'kpis' },
      kpi('#/docs?filter=awaiting', 'alert', 'بانتظار الرد', d.awaitingTotal, `وارد بلا رد منذ ${d.replyDays} أيام فأكثر`, d.awaitingTotal ? 'warn' : ''),
      kpi('#/docs?filter=overdue', 'clock', 'مواعيد متأخرة', m.overdue, 'مسجلة أو مستخرجة من النص', m.overdue ? 'alert' : ''),
      kpi('#/threads', 'thread', 'معاملات نشطة', d.activeThreads, `من ${nf.format(d.threadsTotal)} معاملة مترابطة`),
      kpi('#/threads?missing=1', 'ghost', 'خطابات غير مؤرشفة', d.ghostsTotal, 'مشار إليها ولا توجد في الأرشيف'),
      kpi('#/commitments?state=overdue', 'flag', 'التزامات متأخرة', d.commitments.ours.overdue + d.commitments.theirs.overdue,
        `علينا ${nf.format(d.commitments.ours.overdue)} • لنا ${nf.format(d.commitments.theirs.overdue)}`, d.commitments.ours.overdue ? 'alert' : d.commitments.theirs.overdue ? 'warn' : ''),
      h('a', { class: 'card kpi', href: '#/admin/report' }, h('span', { class: 'label' }, icon('shield'), 'صحة الأرشيف'), h('div', { class: 'health' }, ring(m.score), h('span', { class: 'sub', text: `نص ${m.textPercent}% • تصنيف ${m.categoryPercent}%` })))),
    h('div', { class: 'grid cols-2' },
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', {}, icon('alert'), 'خطابات تنتظر الرد'), h('a', { href: '#/docs?filter=awaiting', class: 'small', text: 'عرض الكل' })), h('div', { class: 'card-body' }, awaiting)),
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', {}, icon('calendar'), 'مواعيد هذا الأسبوع'), h('a', { href: '#/docs?filter=week', class: 'small', text: 'عرض الكل' })), h('div', { class: 'card-body' }, due))),
    h('div', { class: 'grid cols-3', style: 'margin-top:16px' },
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', {}, icon('report'), 'الوارد والصادر'), h('div', { class: 'legend' }, h('span', {}, h('i', { style: 'background:var(--in)' }), 'وارد'), h('span', {}, h('i', { style: 'background:var(--out)' }), 'صادر'))), h('div', { class: 'card-body' }, activityChart(d.activity))),
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', { text: 'حسب التصنيف' })), h('div', { class: 'card-body' },
        d.categories.length ? d.categories.map(c => h('div', { class: 'hbar' }, h('span', { text: c.name }), h('div', { class: 'bar' }, h('i', { style: `width:${Math.max(4, c.count * 100 / Math.max(1, d.total))}%` })), h('span', { class: 'num muted', text: nf.format(c.count) }))) : empty('docs', 'لا مستندات بعد')))),
    findings.length ? h('section', { class: 'card', style: 'margin-top:16px' }, h('div', { class: 'card-head' }, h('h2', {}, icon('spark'), 'ما يحتاج انتباهاً')),
      h('div', { class: 'table-wrap' }, h('table', {}, h('thead', {}, h('tr', {}, h('th', { text: 'النوع' }), h('th', { text: 'المستند' }), h('th', { text: 'السبب' }))), h('tbody', {}, findings)))) : null,
    d.total === 0 ? h('section', { class: 'card pad', style: 'margin-top:16px' }, empty('upload', 'الأرشيف فارغ. ابدأ بإدراج خطاباتك، أو رحّل أرشيف النسخة السابقة من الإدارة.', h('div', { class: 'row', style: 'justify-content:center' }, h('a', { class: 'btn primary', href: '#/inbox' }, icon('upload'), 'إدراج ملفات'), state.me.isAdmin ? h('a', { class: 'btn', href: '#/admin/migrate' }, 'ترحيل النسخة السابقة') : null))) : null);
}

// ======================= المعاملات =======================
export async function renderThreads(view, params) {
  let st = params.get('state') || '';
  const showMissing = params.get('missing') === '1';
  const list = h('div', { class: 'grid cols-2' });
  const qInput = h('input', { type: 'search', placeholder: 'ابحث في المعاملات بالموضوع أو الجهة أو الرقم…', 'aria-label': 'بحث في المعاملات' });
  const seg = h('div', { class: 'seg', role: 'group', 'aria-label': 'حالة المعاملة' });
  async function load() {
    const qs = new URLSearchParams({ state: st, q: qInput.value.trim() });
    const items = await api('/threads?' + qs);
    mount(list, items.length ? items.map(t => h('a', { class: 'card thread-card', href: '#/thread/' + encodeURIComponent(t.id) },
      h('div', { class: 'row' }, h('span', { class: 'chip ' + (t.state === 'بانتظار رد' ? 'warn' : t.state === 'منجزة' ? 'ok' : ''), text: t.state }), h('span', { class: 'muted small', text: `${nf.format(t.documents)} مستند${t.missing ? ` • ${nf.format(t.missing)} غير مؤرشف` : ''}` })),
      h('h3', { text: t.subject }),
      h('p', { class: 'muted small', text: [t.parties.slice(0, 3).join('، '), t.lastActivity ? 'آخر نشاط ' + fmt.ago(t.lastActivity) : ''].filter(Boolean).join(' • ') }),
      h('div', { class: 'dots', 'aria-hidden': 'true' }, t.directions.map(d => h('i', { class: dirClass(d) })), Array.from({ length: t.missing }, () => h('i', { class: 'ghost' })))))
      : [h('div', { class: 'card pad full', style: 'grid-column:1/-1' }, empty('thread', st || qInput.value ? 'لا معاملات مطابقة.' : 'لا معاملات بعد. تتكوّن المعاملة تلقائياً عندما يشير خطاب إلى آخر برقمه.'))]);
  }
  const draw = () => mount(seg, ['', 'بانتظار رد', 'مفتوحة', 'منجزة'].map(v => h('button', { type: 'button', 'aria-pressed': String(st === v), text: v || 'الكل', onclick: () => { st = v; draw(); load(); } })));
  draw();
  let t; qInput.addEventListener('input', () => { clearTimeout(t); t = setTimeout(load, 250); });
  const missingCard = h('section', { class: 'card', style: 'margin-top:18px' });
  async function loadMissing() {
    const gh = await api('/missing');
    mount(missingCard, h('div', { class: 'card-head' }, h('h2', {}, icon('ghost'), 'خطابات مشار إليها وغير موجودة في الأرشيف'), h('span', { class: 'muted small', text: nf.format(gh.length) })),
      h('div', { class: 'card-body' }, gh.length ? h('div', { class: 'table-wrap' }, h('table', {}, h('thead', {}, h('tr', {}, ['المستند', 'الرقم', 'التاريخ', 'الجهة', 'أشار إليه'].map(x => h('th', { text: x })))),
        h('tbody', {}, gh.map(g => h('tr', {}, h('td', { text: g.kind }), h('td', { class: 'num', text: g.number }), h('td', { text: g.dateText || '—' }), h('td', { text: g.owner }),
          h('td', {}, g.citedBy.map(c => h('div', {}, h('a', { href: docLink(c.id), text: `${c.archiveNo} — ${c.title}` }))))))))) : empty('check', 'كل الخطابات المشار إليها موجودة في الأرشيف.')));
  }
  mount(view, pageHead('المعاملات', 'خيوط تتكوّن تلقائياً من إشارات الخطابات بعضها إلى بعض، مرتبة زمنياً.'), h('div', { class: 'filters' }, h('div', { class: 'search' }, icon('search'), qInput), seg), list, missingCard);
  await Promise.all([load(), loadMissing()]);
  if (showMissing) missingCard.scrollIntoView({ behavior: 'smooth' });
}

function timelineItem(doc, current, linkText) {
  return h('div', { class: `tl-item ${dirClass(doc.direction)}${current ? ' current' : ''}` },
    h('a', { class: 'card tl-card', href: docLink(doc.id) },
      h('div', { class: 'head' }, h('span', { class: 'chip ' + dirClass(doc.direction) }, dirIcon(doc.direction), doc.direction), h('b', { class: 'num', text: doc.archiveNo }),
        h('span', { class: 'muted small', text: doc.documentDate ? fmt.both(doc.documentDate) : 'دون تاريخ' })),
      h('h3', { text: doc.title }),
      h('p', { class: 'muted small', text: [doc.sourceEntity && 'من ' + doc.sourceEntity, doc.destinationEntity && 'إلى ' + doc.destinationEntity, doc.externalNo && 'رقم الجهة ' + doc.externalNo, doc.outgoingNo && 'صادر ' + doc.outgoingNo].filter(Boolean).join(' • ') }),
      h('div', { class: 'row', style: 'margin-top:8px' }, statusChip(doc), dueChip(doc), doc.awaitingDays != null ? h('span', { class: 'chip warn' }, `بانتظار رد منذ ${nf.format(doc.awaitingDays)} يوماً`) : null)),
    linkText ? h('div', { class: 'tl-link' }, icon('link'), linkText) : null);
}

export async function renderThread(view, id, currentDoc) {
  const t = await api('/threads/' + encodeURIComponent(id));
  const byId = Object.fromEntries(t.documents.map(d => [d.id, d]));
  const outgoing = {};
  for (const e of t.edges) (outgoing[e.from] ||= []).push(e);
  const items = [];
  for (const d of t.documents) {
    const links = (outgoing[d.id] || []).map(e => byId[e.to] ? `${e.kind} ${byId[e.to].archiveNo}` : `${e.kind} خطاب غير مؤرشف`).join(' • ');
    items.push(timelineItem(d, d.id === currentDoc, links));
  }
  const ghosts = t.missing.map(g => h('div', { class: 'tl-item ghost' }, h('div', { class: 'card tl-card ghost' },
    h('div', { class: 'head' }, h('span', { class: 'chip' }, icon('ghost'), 'غير مؤرشف'), h('b', { class: 'num', text: `${g.kind} رقم ${g.number}` }), g.dateText ? h('span', { class: 'muted small', text: g.dateText }) : null),
    h('p', { class: 'muted small', style: 'margin-top:6px', text: `${g.owner} • أشار إليه ${g.citedBy.map(c => byId[c]?.archiveNo).filter(Boolean).join('، ')}` }),
    can('import') ? h('a', { class: 'btn sm', style: 'margin-top:10px', href: '#/inbox' }, icon('upload'), 'إدراج هذا الخطاب') : null)));
  const node = h('div', {},
    h('div', { class: 'page-head' }, h('div', {},
      h('div', { class: 'row' }, h('span', { class: 'chip ' + (t.state === 'بانتظار رد' ? 'warn' : t.state === 'منجزة' ? 'ok' : ''), text: t.state }), h('span', { class: 'muted small', text: `${nf.format(t.documents.length)} مستند • بدأت ${fmt.hijri(t.started)} • آخر نشاط ${fmt.ago(t.lastActivity)}` })),
      h('h1', { style: 'margin-top:6px', text: t.subject }), t.parties.length ? h('p', { text: 'الأطراف: ' + t.parties.join('، ') }) : null),
      h('div', { class: 'actions' }, h('a', { class: 'btn', href: '#/threads' }, 'كل المعاملات'))),
    h('div', { class: 'timeline' }, items, ghosts));
  if (view) mount(view, node);
  return node;
}

// ======================= المستندات =======================
const FILTERS = [['all', 'الكل'], ['awaiting', 'بانتظار رد'], ['overdue', 'متأخرة'], ['week', 'هذا الأسبوع'], ['review', 'للمراجعة'], ['autocat', 'تصنيف مقترح'], ['unclassified', 'غير مصنّف'], ['ocr', 'بلا نص'], ['sensitive', 'حساسة']];

export function docRow(d) {
  return h('a', { class: 'doc-row', href: docLink(d.id) },
    h('div', { class: 'doc-ico ' + dirClass(d.direction), title: d.direction }, dirIcon(d.direction)),
    h('div', { style: 'min-width:0' },
      h('div', { class: 't' }, d.matches ? markTerms(d.title, d.matches) : d.title),
      h('div', { class: 'm' }, h('span', { class: 'num', text: d.archiveNo }), d.documentDate ? h('span', { text: fmt.hijri(d.documentDate) }) : null, d.sourceEntity ? h('span', { text: d.sourceEntity }) : null, d.category ? h('span', { title: d.categoryAuto ? (d.categoryConfident ? 'تصنيف آلي' : 'تصنيف مقترح يحتاج تأكيداً') : 'التصنيف' }, (d.subCategory ? d.category + ' / ' + d.subCategory : d.category) + (d.categoryAuto && !d.categoryConfident ? ' (مقترح)' : '')) : null,
        d.references ? h('span', {}, icon('link'), ` ${nf.format(d.references)} إشارة`) : null),
      d.excerpt ? h('div', { class: 'x' }, markTerms(d.excerpt, d.matches)) : d.summary ? h('div', { class: 'x', text: d.summary }) : null),
    h('div', { class: 'side-info' }, docChips(d).filter(Boolean).slice(0, 4)));
}

export async function renderDocs(view, params) {
  const st = { q: params.get('q') || '', filter: params.get('filter') || 'all', category: '', status: '', direction: '', page: 1 };
  const list = h('div', { class: 'card' });
  const count = h('span', { class: 'muted small' });
  const q = h('input', { type: 'search', value: st.q, placeholder: 'ابحث بالعنوان أو النص أو رقم المعاملة أو الجهة…', 'aria-label': 'بحث في المستندات' });
  const sel = (label, values, key) => h('select', { 'aria-label': label, onchange: e => { st[key] = e.target.value; st.page = 1; load(); } }, h('option', { value: '', text: label }), values.map(v => h('option', { value: v, text: v })));
  const seg = h('div', { class: 'seg', role: 'group', 'aria-label': 'تصفية' });
  const drawSeg = () => mount(seg, FILTERS.map(([k, l]) => h('button', { type: 'button', 'aria-pressed': String(st.filter === k), text: l, onclick: () => { st.filter = k; st.page = 1; drawSeg(); load(); } })));
  drawSeg();
  let ctl;
  async function load() {
    ctl?.abort(); ctl = new AbortController();
    const qs = new URLSearchParams({ q: st.q, filter: st.filter, category: st.category, status: st.status, direction: st.direction, page: st.page, pageSize: 25 });
    try {
      const r = await api('/documents?' + qs, { signal: ctl.signal });
      count.textContent = `${nf.format(r.total)} مستند`;
      const pages = Math.ceil(r.total / r.pageSize);
      mount(list, r.items.length ? r.items.map(docRow) : empty('search', st.q ? 'لا نتائج. جرّب كلمة أخرى أو رقم المعاملة.' : 'لا مستندات ضمن هذا التصفية.'),
        pages > 1 ? h('div', { class: 'pager' }, h('button', { class: 'btn sm', disabled: st.page <= 1, onclick: () => { st.page--; load(); } }, 'السابق'), h('span', { class: 'muted small', style: 'align-self:center', text: `${nf.format(st.page)} من ${nf.format(pages)}` }), h('button', { class: 'btn sm', disabled: st.page >= pages, onclick: () => { st.page++; load(); } }, 'التالي')) : null);
    } catch (e) { if (e.name !== 'AbortError') mount(list, empty('alert', e.message)); }
  }
  let t; q.addEventListener('input', () => { st.q = q.value.trim(); st.page = 1; clearTimeout(t); t = setTimeout(load, 220); });
  const meta = state.meta || {};
  mount(view, pageHead('المستندات', 'بحث عربي يتجاهل التشكيل والهمزات ويفهم صيغ الجمع، مع تمييز الكلمات المطابقة.', count, can('import') ? h('a', { class: 'btn primary', href: '#/inbox' }, icon('upload'), 'إدراج') : null),
    h('div', { class: 'filters' }, h('div', { class: 'search' }, icon('search'), q), sel('كل التصنيفات', meta.categories || [], 'category'), sel('كل الحالات', meta.statuses || [], 'status'), sel('كل الاتجاهات', meta.directions || [], 'direction')),
    h('div', { style: 'margin-bottom:14px' }, seg), list);
  setTimeout(() => q.focus(), 0);
  await load();
}

// ======================= صفحة المستند =======================
export async function renderDoc(view, id, onCount) {
  const d = await api('/documents/' + encodeURIComponent(id));
  const doc = d.document;
  let version = doc.version;
  const pendingChecks = (d.checks || []).filter(k => k.state === 'pending');
  const tabsBody = h('div');
  const tabs = h('div', { class: 'tabs', role: 'tablist' });
  const tabDefs = [['text', 'النص'], ...(d.can.file ? [['original', 'الأصل']] : []), ...(d.can.edit && !d.restricted ? [['edit', 'البيانات']] : []), ['history', `السجل (${nf.format(d.revisions.length)})`]];
  let current = 'text';
  const showTab = k => { current = k; mount(tabs, tabDefs.map(([key, label]) => h('button', { role: 'tab', 'aria-selected': String(key === current), text: label, onclick: () => showTab(key) }))); mount(tabsBody, tabContent(k)); };

  function textTab() {
    if (d.restricted) return empty('shield', 'نص هذا المستند الحساس متاح للمراجعين ومديري الأرشيف فقط.');
    if (!d.text) return empty('docs', doc.processing === 'queued' ? 'المستند قيد المعالجة؛ يظهر النص هنا عند اكتمالها.' : 'لا يوجد نص مستخرج. يمكن إعادة المعالجة أو إدخال النص من «البيانات».');
    const ranges = [
      ...d.references.map((r, i) => ({ offset: r.offset, length: r.length, cls: 'ref', id: 'ref-' + i, title: `${r.kindLabel} ${r.documentType} رقم ${r.rawNumber}${r.target ? ' ← ' + r.target.archiveNo : r.pending ? ' (بانتظار التحقق)' : ' (غير مؤرشف)'}`, onclick: r.target ? () => location.hash = docLink(r.target.id) : null })),
      ...d.deadlines.map(x => ({ offset: x.offset, length: x.length, cls: 'due', title: x.due ? `مهلة: ${fmt.both(x.due)}` : 'مهلة' })),
    ];
    // الحقول غير المؤكدة: تُميَّز في موضعها (أو يُضاف التمييز إلى الإشارة أو المهلة المتداخلة معها).
    for (const k of pendingChecks) {
      if (k.offset < 0 || !k.length) continue;
      const hit = ranges.find(r => r.offset < k.offset + k.length && k.offset < r.offset + r.length);
      if (hit) { hit.cls += ' pending'; hit.title = 'بانتظار التحقق • ' + hit.title; }
      else ranges.push({ offset: k.offset, length: k.length, cls: 'check pending', title: `${k.fieldLabel}: بانتظار التحقق (${k.reason})` });
    }
    return h('div', {},
      ranges.length ? h('div', { class: 'legend', style: 'padding:12px 22px 0' }, d.references.length ? h('span', {}, h('i', { style: 'background:var(--brand)' }), 'إشارة إلى مستند (انقر للانتقال)') : null, d.deadlines.length ? h('span', {}, h('i', { style: 'background:var(--accent)' }), 'مهلة مطلوبة') : null, pendingChecks.length ? h('span', {}, h('i', { class: 'pending-swatch' }), 'قراءة بانتظار التحقق') : null) : null,
      h('div', { class: 'textview' }, highlight(d.text, ranges)));
  }
  function originalTab() {
    const url = `/api/documents/${encodeURIComponent(id)}/file`;
    const img = ['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp'].includes(doc.fileType);
    return h('div', {}, h('div', { class: 'row', style: 'padding:12px 16px' }, h('span', { class: 'muted small', text: `${d.fileName} • ${fmt.size(d.fileSize)}${d.pageCount ? ' • ' + nf.format(d.pageCount) + ' صفحة' : ''}${d.ocrQuality ? ' • جودة النص ' + d.ocrQuality + '%' : ''}` }), h('a', { class: 'btn sm', href: url + '?download=true', style: 'margin-inline-start:auto' }, icon('download'), 'تنزيل')),
      img ? h('img', { class: 'preview', src: url, alt: doc.title }) : ['.pdf', '.txt'].includes(doc.fileType) ? h('iframe', { class: 'preview', src: url, title: 'الأصل' }) : empty('docs', 'لا تتوفر معاينة لهذا النوع؛ نزّل الملف لفتحه.'));
  }
  function editTab() {
    const meta = state.meta || {};
    const f = (key, label, el, full) => h('div', { class: full ? 'full' : '' }, h('label', { class: 'f', for: 'f-' + key, text: label }), Object.assign(el, { id: 'f-' + key, name: key }));
    const inp = (key, val, attrs = {}) => h('input', { value: val || '', ...attrs });
    const select = (key, val, values) => h('select', {}, values.map(v => h('option', { value: v, text: v, selected: v === val })));
    const err = h('p', { class: 'error', role: 'alert' });
    // التصنيفات الفرعية تتبع التصنيف العام المختار من شجرة التصنيف.
    const subsList = h('datalist', { id: 'subs' });
    const fillSubs = name => mount(subsList, ((meta.taxonomy || []).find(t => t.name === name)?.subs || []).map(x => h('option', { value: x })));
    fillSubs(doc.category);
    const form = h('form', { class: 'form', style: 'padding:18px 20px' },
      f('title', 'العنوان', inp('title', doc.title, { required: true }), true),
      f('direction', 'الاتجاه', select('direction', doc.direction, meta.directions || [])),
      f('confidentiality', 'السرية', select('confidentiality', doc.confidentiality, meta.confidentialities || [])),
      f('category', 'التصنيف', inp('category', doc.category, { list: 'cats' })),
      f('subCategory', 'التصنيف الفرعي', inp('subCategory', doc.subCategory, { list: 'subs' })),
      f('sourceEntity', 'الجهة المرسلة', inp('sourceEntity', doc.sourceEntity, { list: 'ents' })),
      f('destinationEntity', 'الجهة المستقبلة', inp('destinationEntity', doc.destinationEntity, { list: 'ents' })),
      f('externalNo', 'رقم الجهة المرسلة', inp('externalNo', doc.externalNo)),
      f('outgoingNo', 'رقم الصادر', inp('outgoingNo', doc.outgoingNo)),
      f('incomingNo', 'رقم القيد الوارد', inp('incomingNo', doc.incomingNo)),
      f('documentDate', 'تاريخ المستند', inp('documentDate', doc.documentDate, { placeholder: '1448/05/03هـ أو 2026-10-25' })),
      f('dueDate', 'الموعد (يدوي)', inp('dueDate', d.dueDateManual, { placeholder: 'يُستخرج من النص إن تُرك فارغاً' })),
      f('tags', 'الوسوم', inp('tags', d.tags)),
      f('summary', 'الملخص', h('textarea', { value: doc.summary }), true),
      f('text', 'النص', h('textarea', { value: d.text, style: 'min-height:220px' }), true),
      h('datalist', { id: 'cats' }, (meta.categories || []).map(c => h('option', { value: c }))),
      subsList,
      h('datalist', { id: 'ents' }, (meta.entities || []).map(c => h('option', { value: c }))),
      h('div', { class: 'full row' }, h('button', { class: 'btn primary', type: 'submit' }, icon('check'), 'حفظ التعديلات'), err));
    form.elements.category?.addEventListener('input', e => fillSubs(e.target.value));
    form.addEventListener('submit', async e => {
      e.preventDefault();
      const body = { version };
      for (const el of form.elements) if (el.name) body[el.name] = el.value;
      try {
        const r = await api('/documents/' + encodeURIComponent(id), { method: 'PUT', body });
        toast(r.changed.length ? 'حُفظ: ' + r.changed.join('، ') : 'لا تغييرات');
        renderDoc(view, id);
      } catch (ex) { err.textContent = ex.message; }
    });
    return form;
  }
  function historyTab() {
    const box = h('div', { style: 'padding:6px 20px 18px' });
    const showRevision = async r => {
      const v = await api(`/documents/${encodeURIComponent(id)}/revisions/${r.version}`);
      mount(box, h('div', { class: 'card pad' },
        h('h3', { text: `الإصدار ${v.version}: ${v.title}` }),
        h('p', { class: 'muted small', text: `${v.status} • ${v.category || 'بلا تصنيف'} • ${fmt.time(v.updatedAt)} • ${v.updatedBy}` }),
        h('div', { class: 'textview', style: 'max-height:300px;padding:10px 0', text: v.text || '(بلا نص)' })));
    };
    const rows = d.revisions.map(r => h('tr', {},
      h('td', { class: 'num', text: 'v' + r.version }),
      h('td', { text: fmt.time(r.changedAt) }),
      h('td', { text: r.changedBy }),
      h('td', { text: r.summary }),
      h('td', {}, d.restricted ? null : h('button', { class: 'btn sm', onclick: () => showRevision(r) }, icon('eye'), 'عرض'))));
    const head = h('thead', {}, h('tr', {}, ['الإصدار', 'الوقت', 'بواسطة', 'التغيير', ''].map(x => h('th', { text: x }))));
    return h('div', {}, h('div', { class: 'table-wrap' }, h('table', {}, head, h('tbody', {}, rows))), box);
  }
  const tabContent = k => k === 'text' ? textTab() : k === 'original' ? originalTab() : k === 'edit' ? editTab() : historyTab();

  // ===== اللوحة الجانبية =====
  const refItems = d.references.map((r, i) => h('div', { class: 'ref-item' + (r.target ? '' : r.pending ? ' pending' : ' missing') },
    h('div', { class: 'row' }, h('span', { class: 'chip ' + (r.target ? 'ok' : ''), text: r.kindLabel }), h('b', { text: `${r.documentType || 'مستند'} رقم ${r.rawNumber}` })),
    h('div', { class: 'muted small', style: 'margin-top:4px', text: [r.dateText && (r.calendar === 'هجري' && r.date ? `${r.dateText} (${fmt.greg(r.date)})` : r.dateText), r.ownerLabel].filter(Boolean).join(' • ') }),
    r.target ? h('a', { href: docLink(r.target.id), style: 'display:block;margin-top:6px;font-weight:600' }, icon('link'), ` ${r.target.archiveNo} — ${r.target.title}`)
      : r.pending ? h('div', { class: 'small', style: 'margin-top:6px;color:var(--accent);font-weight:600' }, icon('eye'), ' قراءتها بانتظار التحقق؛ لا تُربط حتى تُؤكد')
      : h('div', { class: 'muted small', style: 'margin-top:6px' }, icon('ghost'), ' غير موجود في الأرشيف'),
    r.suggestion ? h('a', { href: docLink(r.suggestion.id), class: 'small', style: 'display:block;margin-top:4px' }, icon('search'), ` ربما: ${r.suggestion.archiveNo} — ${r.suggestion.title} (${r.suggestion.reason})`) : null,
    h('button', { class: 'btn ghost sm', style: 'margin-top:4px', onclick: () => { showTab('text'); setTimeout(() => document.getElementById('ref-' + i)?.scrollIntoView({ behavior: 'smooth', block: 'center' }), 50); } }, 'موضعها في النص')));
  const cited = d.citedBy.map(c => h('a', { class: 'ref-item', style: 'display:block', href: docLink(c.id) }, h('span', { class: 'chip ok', text: c.kind }), ' ', h('b', { class: 'num', text: c.archiveNo }), h('div', { text: c.title })));
  const deadlines = d.deadlines.map(x => h('div', { class: 'ref-item' }, h('div', { class: 'row' }, h('span', { class: 'chip warn' }, icon('clock'), x.label), x.due ? h('b', { text: fmt.both(x.due) }) : h('span', { class: 'muted small', text: pendingChecks.some(k => k.field === 'date') ? 'يُحسب الاستحقاق بعد التحقق من تاريخ الخطاب' : 'حدد تاريخ المستند لحساب الاستحقاق' })), h('div', { class: 'q', text: '«' + x.evidence + '»' })));
  const comments = h('div', {}, d.comments.length ? d.comments.map(c => h('div', { class: 'comment' }, h('b', { text: c.author }), h('span', { class: 'muted small', text: ' • ' + fmt.time(c.createdAt) }), h('p', { text: c.body }))) : h('p', { class: 'muted small', text: 'لا ملاحظات بعد.' }));
  const cbox = h('textarea', { placeholder: 'أضف ملاحظة للفريق…', style: 'min-height:70px;margin-top:10px', 'aria-label': 'ملاحظة' });
  const addComment = h('button', { class: 'btn sm', style: 'margin-top:8px', onclick: async () => { if (!cbox.value.trim()) return; await api(`/documents/${encodeURIComponent(id)}/comments`, { method: 'POST', body: { body: cbox.value } }); renderDoc(view, id); } }, icon('message'), 'إضافة');

  // ===== التصنيف =====
  function classBox() {
    const c = d.classification;
    const setClass = async (action, category, subCategory) => {
      try { await api(`/documents/${encodeURIComponent(id)}/classification`, { method: 'POST', body: { action, category, subCategory, version } }); toast(action === 'confirm' ? 'أُكِّد التصنيف' : `التصنيف: ${category}`); renderDoc(view, id, onCount); }
      catch (ex) { toast(ex.message, { error: true }); }
    };
    const label = doc.category ? [doc.category, doc.subCategory].filter(Boolean).join(' / ') : 'غير مصنّف';
    const chip = !doc.categoryAuto ? null : doc.categoryConfident ? h('span', { class: 'chip ok', title: 'صنّفته المنصة آلياً بثقة عالية' }, icon('spark'), 'آلي') : h('span', { class: 'chip warn', title: 'تصنيف مقترح؛ أكده أو غيّره' }, icon('spark'), 'مقترح');
    const alts = doc.categoryAuto && c?.alternatives ? c.alternatives.filter(a => a.category !== doc.category) : [];
    return h('div', { class: 'class-box' },
      h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap' }, h('span', { class: 'muted small', text: 'التصنيف' }), h('b', { text: label }), chip,
        doc.categoryAuto && c ? h('span', { class: 'muted small', text: `ثقة ${Math.round(c.confidence * 100)}%` }) : null),
      doc.categoryAuto && c?.evidence?.length ? h('div', { class: 'muted small', style: 'margin-top:4px', text: 'لماذا: ' + c.evidence.join('، ') }) : null,
      d.can.edit && doc.categoryAuto ? h('div', { class: 'row', style: 'gap:6px;flex-wrap:wrap;margin-top:8px' },
        h('button', { class: 'btn sm primary', onclick: () => setClass('confirm') }, icon('check'), 'تأكيد التصنيف'),
        alts.map(a => h('button', { class: 'btn sm', title: 'اختيار هذا التصنيف', onclick: () => setClass('set', a.category, a.sub) }, [a.category, a.sub].filter(Boolean).join(' / '))),
        h('button', { class: 'btn sm ghost', onclick: () => showTab('edit') }, icon('edit'), 'تغيير')) : null,
      d.can.edit && !doc.category ? h('button', { class: 'btn sm', style: 'margin-top:8px', onclick: () => showTab('edit') }, icon('edit'), 'تصنيف') : null);
  }

  // ===== التحقق من القراءة الآلية =====
  let verifyPanel = null;
  if ((d.checks || []).length) {
    const autoCount = d.checks.filter(k => k.state === 'auto').length;
    const reviewed = d.checks.filter(k => k.state === 'confirmed' || k.state === 'corrected');
    const cards = d.can.edit ? pendingChecks.map(k => checkCard({ check: k, document: { id, archiveNo: doc.archiveNo, title: doc.title } }, { showDoc: false, onDone: n => { onCount?.(n); setTimeout(() => renderDoc(view, id, onCount), 700); } }))
      : pendingChecks.map(k => h('div', { class: 'ref-item pending' }, h('b', { text: k.fieldLabel }), h('div', { class: 'small', text: k.spanText }), h('div', { class: 'muted small', text: k.reason })));
    verifyPanel = h('section', { class: 'card pad panel verify' },
      h('h3', {}, icon('eye'), 'التحقق من القراءة الآلية'),
      h('p', { class: 'small', text: pendingChecks.length
        ? `${nf.format(pendingChecks.length)} من الحقول الحرجة بانتظار التحقق؛ لا تُبنى عليها روابط ولا مهل حتى تُحسم.`
        : 'كل الحقول الحرجة مؤكدة.' }),
      h('p', { class: 'muted small', text: [autoCount ? `${nf.format(autoCount)} أُكدت آلياً (اتفاق قراءتين مستقلتين بثقة عالية)` : '', reviewed.length ? `${nf.format(reviewed.length)} حسمها مراجع` : ''].filter(Boolean).join(' • ') }),
      cards);
  }
  const threadPanel = h('section', { class: 'card pad panel', hidden: !doc.thread });
  if (doc.thread) renderThread(null, doc.thread, id).then(node => { node.querySelector('.page-head')?.remove(); mount(threadPanel, h('h3', {}, icon('thread'), 'خيط المعاملة', h('a', { href: '#/thread/' + encodeURIComponent(doc.thread), class: 'small', style: 'margin-inline-start:auto;font-weight:600', text: 'فتح المعاملة' })), node); }).catch(() => {});

  // ===== شريط الإجراءات =====
  const statusMenu = h('div', { class: 'menu', hidden: true });
  const statusBtn = h('div', { class: 'status-menu' }, h('button', { class: 'btn', onclick: e => { e.stopPropagation(); statusMenu.hidden = !statusMenu.hidden; } }, icon('check'), 'تغيير الحالة'), statusMenu);
  mount(statusMenu, (state.meta?.statuses || []).map(st => h('button', { disabled: st === doc.status, onclick: async () => {
    statusMenu.hidden = true;
    const note = st === 'منجز' || st === 'مؤرشف' ? prompt('ملاحظة الإنجاز (اختيارية):') ?? null : '';
    if (note === null) return;
    try { await api(`/documents/${encodeURIComponent(id)}/status`, { method: 'POST', body: { version, status: st, note } }); toast('الحالة: ' + st); renderDoc(view, id); }
    catch (ex) { toast(ex.message, { error: true }); }
  } }, st === doc.status ? icon('check') : h('span', { style: 'width:18px' }), st)));
  document.addEventListener('click', () => { statusMenu.hidden = true; }, { once: true });

  mount(view,
    h('div', { class: 'page-head' },
      h('div', { class: 'doc-title' }, h('div', { class: 'doc-ico ' + dirClass(doc.direction), style: 'width:48px;height:48px' }, dirIcon(doc.direction)),
        h('div', {}, h('div', { class: 'muted small num', text: `${doc.archiveNo}${doc.documentDate ? ' • ' + fmt.both(doc.documentDate) : ''}` }), h('h1', { text: doc.title }), doc.titleAuto ? h('div', { class: 'muted small', title: 'استُخرج العنوان من محتوى المستند؛ عدّله من «البيانات» إن لزم', text: 'عنوان مستخرج من المحتوى' }) : null, h('div', { class: 'chips' }, docChips(doc)))),
      h('div', { class: 'actions' },
        doc.direction === 'وارد' && can('import') && !d.restricted ? h('a', { class: 'btn primary', href: '#/reply/' + encodeURIComponent(id) }, icon('edit'), 'صياغة الرد') : null,
        d.can.workflow ? statusBtn : null,
        d.can.edit && doc.hasFile ? h('button', { class: 'btn', onclick: async () => { await api(`/documents/${encodeURIComponent(id)}/reprocess`, { method: 'POST' }); toast('أُرسل المستند لإعادة المعالجة'); } }, icon('refresh'), 'إعادة المعالجة') : null,
        d.can.file ? h('a', { class: 'btn', href: `/api/documents/${encodeURIComponent(id)}/file?download=true` }, icon('download'), 'الأصل') : null)),
    doc.processing === 'failed' || d.processingError ? h('div', { class: 'card pad', style: 'margin-bottom:14px;border-color:var(--accent)' }, h('div', { class: 'row' }, icon('alert'), h('span', { text: d.processingError || 'تعذرت المعالجة.' }))) : null,
    d.scanQuality && !d.processingError ? h('div', { class: 'card pad', style: 'margin-bottom:14px;border-color:var(--accent)' }, h('div', { class: 'row' }, icon('alert'), h('span', { text: 'جودة المسح دون الحد المقبول: ' + d.scanQuality + '. يُنصح بإعادة المسح بدقة 300 نقطة/بوصة.' }))) : null,
    h('div', { class: 'doc-layout' },
      h('section', { class: 'card' }, tabs, tabsBody),
      h('aside', { class: 'stack' },
        h('section', { class: 'card pad panel' }, h('h3', {}, icon('docs'), 'البطاقة'), h('dl', { class: 'kv' },
          [['الاتجاه', doc.direction], ['من', doc.sourceEntity], ['إلى', doc.destinationEntity], ['رقم الجهة', doc.externalNo], ['رقم الصادر', doc.outgoingNo], ['القيد الوارد', doc.incomingNo], ['الموعد', doc.due ? fmt.both(doc.due) : ''], ['أُدرج', `${fmt.time(doc.createdAt)} • ${d.createdBy}`]]
            .filter(x => x[1]).flatMap(([k, v]) => [h('dt', { text: k }), h('dd', { text: v })])), classBox(), doc.summary ? h('p', { class: 'small', style: 'margin-top:10px', text: doc.summary }) : null),
        verifyPanel,
        threadPanel,
        refItems.length ? h('section', { class: 'card pad panel' }, h('h3', {}, icon('link'), `يشير إلى (${nf.format(refItems.length)})`), refItems) : null,
        cited.length ? h('section', { class: 'card pad panel' }, h('h3', {}, icon('arrow'), `أشار إليه (${nf.format(cited.length)})`), cited) : null,
        deadlines.length ? h('section', { class: 'card pad panel' }, h('h3', {}, icon('clock'), 'المهل في النص'), deadlines) : null,
        d.commitments?.length ? h('section', { class: 'card pad panel' }, h('h3', {}, icon('flag'), d.commitments[0].side === 'لنا' ? 'وعود الجهة في هذا الخطاب' : 'وعودنا في هذا الخطاب'),
          h('div', { class: 'stack', style: 'gap:8px' }, d.commitments.map(c => commitmentCard(c, () => renderDoc(view, id))))) : null,
        h('section', { class: 'card pad panel' }, h('h3', {}, icon('message'), 'ملاحظات الفريق'), comments, cbox, addComment))));
  showTab(current);
  const off = onEvent(msg => { if (msg.type === 'job' && msg.payload.documentId === id && (msg.payload.state === 'done' || msg.payload.state === 'failed') && document.body.contains(view.firstChild)) { off(); renderDoc(view, id, onCount); } });
}
