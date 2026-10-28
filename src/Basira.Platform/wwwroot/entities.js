// مرصد الجهات: خريطة العلاقات، ورادار المهل، وجدول الجهات، وملف كل جهة.
import { h, s, icon, api, mount, nf, fmt, toast, state, confirmDialog } from './core.js';
import { pageHead, empty, docRow, ring } from './views.js';

const LEVELS = { good: ['منتظمة', 'ok', '✓'], warn: ['تحتاج متابعة', 'warn', '!'], bad: ['متعثرة', 'danger', '×'] };
const RISK = { late: ['متأخر', 'danger', 'alert'], high: ['خطر مرتفع', 'danger', 'alert'], medium: ['خطر متوسط', 'warn', 'clock'], low: ['في الموعد', 'ok', 'check'] };
const entityLink = key => '#/entities/' + encodeURIComponent(key);
// عدد الأيام بصيغته العربية: يوم واحد، يومان، 3 أيام، 11 يوماً.
const dayWord = n => { n = Math.abs(Math.round(n)); return n === 0 ? 'أقل من يوم' : n === 1 ? 'يوم واحد' : n === 2 ? 'يومان' : n <= 10 ? `${nf.format(n)} أيام` : `${nf.format(n)} يوماً`; };
const days = v => v == null ? '—' : dayWord(v);
const pct = v => v == null ? '—' : `${nf.format(Math.round(v * 100))}%`;
const levelChip = level => { const [l, cls] = LEVELS[level]; return h('span', { class: 'chip ' + cls }, LEVELS[level][2], ' ', l); };
const MONTHS = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'];
const monthName = ym => MONTHS[+ym.slice(5, 7) - 1];

// تلميح يتبع المؤشر (خريطة العلاقات والرسم الشهري).
function tooltip() {
  const tip = h('div', { class: 'viz-tip', hidden: true, role: 'tooltip' });
  document.body.append(tip);
  return {
    show(e, ...content) { mount(tip, ...content); tip.hidden = false; this.move(e); },
    // فوق المؤشر إن اتسع المكان، فلا يغطي التلميح الأعمدة والعقد التي يصفها.
    move(e) { const w = tip.offsetWidth, hgt = tip.offsetHeight, x = Math.min(window.innerWidth - w - 8, Math.max(8, e.clientX - w / 2)); tip.style.left = x + 'px'; tip.style.top = (e.clientY - hgt - 16 > 8 ? e.clientY - hgt - 16 : e.clientY + 18) + 'px'; },
    hide() { tip.hidden = true; },
    remove() { tip.remove(); },
  };
}

// ===== خريطة العلاقات: جهتكم في المركز، والجهات حولها بحجم مراسلاتها ولون انتظامكم معها =====
function constellation(entities, org, tip) {
  const W = 820, H = 540, cx = W / 2, cy = H / 2;
  const top = [...entities].sort((a, b) => (b.incoming + b.outgoing) - (a.incoming + a.outgoing)).slice(0, 18);
  const max = Math.max(1, ...top.map(e => e.incoming + e.outgoing));
  const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, class: 'constellation', role: 'img', 'aria-label': 'خريطة العلاقات مع الجهات' });
  svg.append(s('ellipse', { cx, cy, rx: 205, ry: 205 * 0.78, class: 'orbit' }));
  // حتى عشر جهات على مدار واحد؛ وما زاد يتوزع على مدارين.
  const inner = top.length <= 10 ? top.length : 7;
  const single = inner === top.length;
  top.forEach((e, i) => {
    const onInner = i < inner;
    const n = onInner ? inner : top.length - inner;
    const k = onInner ? i : i - inner;
    const angle = -Math.PI / 2 + (2 * Math.PI * k) / n + (onInner ? 0 : Math.PI / n);
    const R = single ? 205 : onInner ? 165 : 245;
    const x = cx + R * Math.cos(angle), y = cy + R * Math.sin(angle) * 0.78;
    const vol = e.incoming + e.outgoing;
    const r = 15 + 21 * Math.sqrt(vol / max);
    const [label, cls, glyph] = LEVELS[e.healthLevel];
    svg.append(s('line', { x1: cx, y1: cy, x2: x, y2: y, class: 'link', 'stroke-width': 1 + 5 * (vol / max) }));
    const g = s('g', { class: 'node ' + cls, tabindex: 0, role: 'link', 'aria-label': `${e.name}: ${label}، ${vol} خطاب` });
    g.append(s('circle', { cx: x, cy: y, r: r + 10, class: 'hit' }), s('circle', { cx: x, cy: y, r, class: 'dot' }),
      s('text', { x, y: y + 5, class: 'glyph', 'text-anchor': 'middle' }, glyph),
      s('text', { x, y: y + r + 16, class: 'name', 'text-anchor': 'middle' }, e.name.length > 22 ? e.name.slice(0, 21) + '…' : e.name));
    if (e.weOweOverdue) g.append(s('circle', { cx: x + r * 0.75, cy: y - r * 0.75, r: 9, class: 'badge' }), s('text', { x: x + r * 0.75, y: y - r * 0.75 + 4, class: 'badge-t', 'text-anchor': 'middle' }, String(e.weOweOverdue)));
    const open = () => { tip.hide(); location.hash = entityLink(e.key); };
    g.addEventListener('click', open);
    g.addEventListener('keydown', ev => { if (ev.key === 'Enter') open(); });
    g.addEventListener('pointerenter', ev => tip.show(ev, h('b', { text: e.name }), h('div', { class: 'row', style: 'gap:6px;margin:4px 0' }, levelChip(e.healthLevel)),
      h('div', { text: `وارد ${nf.format(e.incoming)} • صادر ${nf.format(e.outgoing)}` }),
      h('div', { text: `علينا ${nf.format(e.weOwe)}${e.weOweOverdue ? ` (${nf.format(e.weOweOverdue)} متأخر)` : ''} • لنا ${nf.format(e.theyOwe)}` }),
      h('div', { text: `نرد خلال ${days(e.ourMedianDays)} • يردون خلال ${days(e.theirMedianDays)}` })));
    g.addEventListener('pointermove', ev => tip.move(ev));
    g.addEventListener('pointerleave', () => tip.hide());
    svg.append(g);
  });
  const center = s('g', { class: 'center' });
  center.append(s('circle', { cx, cy, r: 46, class: 'core' }), s('image', { href: '/brand/mark.svg', x: cx - 22, y: cy - 30, width: 44, height: 44 }),
    s('text', { x: cx, y: cy + 66, class: 'org', 'text-anchor': 'middle' }, org || 'جهتكم'));
  svg.append(center);
  return svg;
}

function riskItem(r, showEntity = true) {
  const [label, cls, ic] = RISK[r.level];
  return h('div', { class: 'risk-item ' + r.level },
    h('div', { class: 'row', style: 'justify-content:space-between;gap:8px' },
      h('span', { class: 'chip ' + cls }, icon(ic), label),
      h('b', { class: 'num small', text: r.daysLeft < 0 ? `متأخر ${dayWord(r.daysLeft)}` : r.daysLeft === 0 ? 'يحين اليوم' : `يتبقى ${dayWord(r.daysLeft)}` })),
    h('a', { class: 'risk-title', href: '#/doc/' + encodeURIComponent(r.documentId) }, h('span', { class: 'num muted', text: r.archiveNo + ' — ' }), r.title),
    showEntity ? h('a', { class: 'small', href: entityLink(r.entityKey), text: r.entity }) : null,
    r.level !== 'late' ? h('div', { class: 'risk-bar', title: `احتمال التأخر ${pct(r.risk)}` }, h('i', { style: `width:${Math.max(4, r.risk * 100)}%` })) : null,
    h('p', { class: 'muted small', text: r.reason }),
    h('div', { class: 'row', style: 'gap:6px;margin-top:6px' }, h('a', { class: 'btn sm', href: '#/reply/' + encodeURIComponent(r.documentId) }, icon('edit'), 'صياغة الرد')));
}

export async function renderEntities(view) {
  const o = await api('/entities');
  const tip = tooltip();
  const off = () => { if (!document.body.contains(view.firstChild)) { tip.remove(); window.removeEventListener('hashchange', off); } };
  window.addEventListener('hashchange', off);
  if (!o.entities.length) {
    mount(view, pageHead('مرصد الجهات', 'علاقتكم بكل جهة تراسلونها.'), h('section', { class: 'card pad' }, empty('network', 'لا جهات بعد. يُبنى المرصد من حقلي «الجهة المرسلة» و«الجهة المستقبلة» في المستندات، وتملؤهما القراءة الآلية أو التحرير.')));
    return;
  }
  const kpi = (ic, label, value, sub, cls = '') => h('div', { class: 'card kpi ' + cls }, h('span', { class: 'label' }, icon(ic), label), h('div', { class: 'value', text: value }), h('span', { class: 'sub', text: sub }));
  // جدول الجهات بالبحث والترتيب.
  let sort = 'weOweOverdue', dir = -1, q = '';
  const tbody = h('tbody');
  const cols = [['name', 'الجهة'], ['incoming', 'وارد'], ['outgoing', 'صادر'], ['weOwe', 'علينا'], ['theyOwe', 'لنا'], ['ourMedianDays', 'زمن ردنا'], ['theirMedianDays', 'زمن ردهم'], ['onTimeRate', 'الالتزام بالمهل'], ['lastContact', 'آخر تواصل'], ['health', 'الحالة']];
  const thead = h('thead');
  const drawTable = () => {
    mount(thead, h('tr', {}, cols.map(([k, l]) => h('th', { 'aria-sort': k === sort ? (dir < 0 ? 'descending' : 'ascending') : 'none' },
      h('button', { class: 'th-sort', type: 'button', onclick: () => { dir = k === sort ? -dir : (k === 'name' ? 1 : -1); sort = k; drawTable(); } }, l, k === sort ? (dir < 0 ? ' ↓' : ' ↑') : '')))));
    const rows = o.entities.filter(e => !q || e.name.includes(q) || e.variants.some(v => v.includes(q)))
      .sort((a, b) => { const x = a[sort] ?? -1, y = b[sort] ?? -1; return (typeof x === 'string' ? x.localeCompare(y, 'ar') : x - y) * dir || (b.incoming + b.outgoing) - (a.incoming + a.outgoing); });
    mount(tbody, rows.map(e => h('tr', { class: 'click', onclick: () => location.hash = entityLink(e.key) },
      h('td', {}, h('a', { href: entityLink(e.key), text: e.name, onclick: ev => ev.stopPropagation() }), e.variants.length > 1 ? h('div', { class: 'muted small', text: `${nf.format(e.variants.length)} صيغ للاسم` }) : null),
      h('td', { class: 'num', text: nf.format(e.incoming) }), h('td', { class: 'num', text: nf.format(e.outgoing) }),
      h('td', { class: 'num' }, nf.format(e.weOwe), e.weOweOverdue ? h('span', { class: 'chip danger', style: 'margin-inline-start:6px', text: `${nf.format(e.weOweOverdue)} متأخر` }) : null),
      h('td', { class: 'num' }, nf.format(e.theyOwe), e.theyOweOverdue ? h('span', { class: 'chip warn', style: 'margin-inline-start:6px', text: `${nf.format(e.theyOweOverdue)} متأخر` }) : null),
      h('td', { class: 'num', text: days(e.ourMedianDays) }), h('td', { class: 'num', text: days(e.theirMedianDays) }), h('td', { class: 'num', text: pct(e.onTimeRate) }),
      h('td', { class: 'small', text: e.lastContact ? fmt.hijri(e.lastContact) : '—' }), h('td', {}, levelChip(e.healthLevel)))));
  };
  drawTable();
  const search = h('input', { type: 'search', placeholder: 'ابحث عن جهة…', 'aria-label': 'بحث في الجهات', style: 'max-width:280px', oninput: e => { q = e.target.value.trim(); drawTable(); } });
  const lateCount = o.risks.filter(r => r.level === 'late').length, highCount = o.risks.filter(r => r.level === 'high').length;
  mount(view,
    pageHead('مرصد الجهات', 'علاقتكم بكل جهة تراسلونها، من الأرشيف نفسه: من ينتظر ردكم ومن تنتظرون رده، وكم تستغرق الردود، وأي رد مرشح للتأخر.'),
    h('div', { class: 'kpis four' },
      kpi('alert', 'ردود علينا', nf.format(o.weOwe), o.weOweOverdue ? `تجاوز موعده منها: ${nf.format(o.weOweOverdue)}` : 'لا شيء متأخر', o.weOweOverdue ? 'alert' : ''),
      kpi('arrow', 'ردود لنا', nf.format(o.theyOwe), o.theyOweOverdue ? `تجاوز مهلته عند الجهات: ${nf.format(o.theyOweOverdue)}` : 'طلبات بمهلة لم يُرد عليها بعد'),
      kpi('clock', 'زمن ردنا المعتاد', days(o.ourMedianDays), 'الوسيط من تاريخ الوارد إلى تاريخ ردنا'),
      kpi('check', 'الالتزام بالمهل', pct(o.onTimeRate), 'ردود في موعدها من كل ما حلّ موعده')),
    h('div', { class: 'grid observatory' },
      h('section', { class: 'card pad map-card' },
        h('div', { class: 'row', style: 'justify-content:space-between' }, h('h3', {}, icon('network'), 'خريطة العلاقات'),
          h('div', { class: 'legend small' }, ['good', 'warn', 'bad'].map(l => h('span', { class: 'lg ' + LEVELS[l][1] }, h('i', { text: LEVELS[l][2] }), LEVELS[l][0])), h('span', { class: 'muted', text: 'الحجم = عدد المراسلات' }))),
        constellation(o.entities, o.organization, tip),
        o.entities.length > 18 ? h('p', { class: 'muted small', text: `تظهر أكثر 18 جهة مراسلةً؛ الجهات كلها في الجدول أدناه.` }) : null),
      h('section', { class: 'card pad radar' },
        h('h3', {}, icon('alert'), 'رادار المهل'),
        h('p', { class: 'muted small', style: 'margin:4px 0 10px', text: lateCount || highCount ? `متأخرة: ${nf.format(lateCount)} • مرشحة للتأخر: ${nf.format(highCount)}. التقدير من زمن ردودكم الفعلي على كل جهة.` : 'الردود المطلوبة منكم مرتبة بخطر تأخرها، محسوباً من زمن ردودكم الفعلي.' }),
        o.risks.length ? h('div', { class: 'stack', style: 'gap:10px' }, o.risks.slice(0, 12).map(r => riskItem(r))) : empty('check', 'لا ردود مطلوبة منكم الآن.'))),
    h('section', { class: 'card', style: 'margin-top:16px' },
      h('div', { class: 'card-head' }, h('h2', {}, icon('table'), `الجهات (${nf.format(o.entities.length)})`), search),
      h('div', { class: 'table-wrap' }, h('table', { class: 'entities-table' }, thead, tbody)),
      o.unassigned ? h('p', { class: 'muted small', style: 'padding:10px 20px 16px', text: `${nf.format(o.unassigned)} مستنداً وارداً أو صادراً بلا جهة محددة لا يدخل المرصد؛ أكمل جهته من «البيانات» في صفحته.` }) : null));
}

// ===== الرسم الشهري: وارد وصادر لكل شهر بمحور واحد =====
function monthlyChart(e, months, tip) {
  const W = 1100, H = 230, padB = 26, padT = 12, padX = 8, padL = 34;
  const max = Math.max(1, ...e.monthlyIn, ...e.monthlyOut);
  const step = (W - padX - padL) / months.length, bw = Math.min(16, (step - 12) / 2);
  const y = v => H - padB - (H - padB - padT) * (v / max);
  const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, class: 'month-chart', role: 'img', 'aria-label': 'المراسلات الشهرية: وارد وصادر' });
  // خطوط مرجعية بأعداد صحيحة، وأرقامها في الطرف الأيسر خارج منطقة الأعمدة.
  for (const v of [...new Set([Math.round(max / 2), max])].filter(v => v > 0)) svg.append(s('line', { x1: padL, x2: W - padX, y1: y(v), y2: y(v), class: 'grid' }), s('text', { x: padL - 8, y: y(v) + 4, class: 'tick', 'text-anchor': 'end' }, nf.format(v)));
  svg.append(s('line', { x1: padL, x2: W - padX, y1: H - padB, y2: H - padB, class: 'axis' }));
  months.forEach((m, i) => {
    // من اليمين إلى اليسار: أقدم شهر يميناً كاتجاه القراءة.
    const x0 = W - padX - step * (i + 1) + (step - bw * 2 - 2) / 2;
    const bar = (v, x, cls) => v ? s('rect', { x, y: y(v), width: bw, height: Math.max(0, H - padB - y(v)), rx: 3, class: cls }) : null;
    const g = s('g', { class: 'month' });
    g.append(s('rect', { x: W - padX - step * (i + 1), y: padT, width: step, height: H - padB - padT, class: 'hit' }));
    for (const b of [bar(e.monthlyIn[i], x0 + bw + 2, 'in'), bar(e.monthlyOut[i], x0, 'out')]) if (b) g.append(b);
    if (i % 2 === 0 || months.length <= 6) g.append(s('text', { x: W - padX - step * i - step / 2, y: H - 8, class: 'tick', 'text-anchor': 'middle' }, monthName(m)));
    g.addEventListener('pointerenter', ev => tip.show(ev, h('b', { text: `${monthName(m)} ${m.slice(0, 4)}` }), h('div', {}, h('i', { class: 'sw in' }), ` وارد: ${nf.format(e.monthlyIn[i])}`), h('div', {}, h('i', { class: 'sw out' }), ` صادر: ${nf.format(e.monthlyOut[i])}`)));
    g.addEventListener('pointermove', ev => tip.move(ev));
    g.addEventListener('pointerleave', () => tip.hide());
    svg.append(g);
  });
  return svg;
}

export async function renderEntity(view, key) {
  const p = await api('/entities/' + encodeURIComponent(key));
  const e = p.entity;
  const o = await api('/entities');
  const tip = tooltip();
  const off = () => { if (!document.body.contains(view.firstChild)) { tip.remove(); window.removeEventListener('hashchange', off); } };
  window.addEventListener('hashchange', off);
  const stat = (label, value, sub) => h('div', { class: 'stat' }, h('span', { class: 'muted small', text: label }), h('b', { text: value }), sub ? h('span', { class: 'muted small', text: sub }) : null);
  const merge = async target => {
    const other = p.others.find(x => x.key === target);
    if (!other || await confirmDialog({ title: 'دمج الجهتين', message: `تُحسب كل مراسلات «${e.name}» ضمن «${other.name}». يمكن فك الدمج لاحقاً من صفحة «${other.name}».`, confirm: 'دمج' }) === null) return;
    try { const r = await api('/entities/merge', { method: 'POST', body: { from: e.key, into: target } }); toast('دُمجت الجهتان'); location.hash = entityLink(r.into); }
    catch (ex) { toast(ex.message, { error: true }); }
  };
  const unmerge = async k => {
    try { await api('/entities/unmerge', { method: 'POST', body: { from: k } }); toast('فُك الدمج'); renderEntity(view, key); }
    catch (ex) { toast(ex.message, { error: true }); }
  };
  const [lvl] = LEVELS[e.healthLevel];
  mount(view,
    h('div', { class: 'page-head' },
      h('div', {}, h('a', { class: 'small', href: '#/entities' }, '← مرصد الجهات'), h('h1', { text: e.name }),
        e.variants.length > 1 ? h('p', { class: 'muted small', text: 'تُكتب أيضاً: ' + e.variants.filter(v => v !== e.name).join('، ') }) : null),
      h('div', { class: 'actions' }, p.canMerge && p.others.length ? h('select', { 'aria-label': 'دمج مع جهة أخرى', style: 'width:auto', onchange: ev => { if (ev.target.value) merge(ev.target.value); ev.target.value = ''; } },
        h('option', { value: '', text: 'دمج مع جهة أخرى…' }), p.others.map(x => h('option', { value: x.key, text: x.name }))) : null)),
    p.merged.length ? h('div', { class: 'card pad', style: 'margin-bottom:14px' }, h('span', { class: 'small' }, 'دُمجت فيها: '), [...p.merged].map(k => h('span', { class: 'chip', style: 'margin-inline-end:6px' }, k, p.canMerge ? h('button', { class: 'chip-x', type: 'button', title: 'فك الدمج', 'aria-label': 'فك الدمج', onclick: () => unmerge(k) }, '×') : null))) : null,
    h('div', { class: 'grid entity-top' },
      h('section', { class: 'card pad entity-health' }, ring(e.health), h('div', {}, h('div', { class: 'row', style: 'gap:6px' }, levelChip(e.healthLevel)),
        h('p', { class: 'small', style: 'margin-top:6px', text: e.onTimeRate != null ? `رددتم في الموعد على ${pct(e.onTimeRate)} مما حلّ موعده.${e.weOweOverdue ? ` ردود متأخرة الآن: ${nf.format(e.weOweOverdue)}.` : ''}` : e.weOweOverdue ? `ردود متأخرة الآن: ${nf.format(e.weOweOverdue)}.` : 'لا ردود حلّ موعدها بعد.' }),
        h('p', { class: 'muted small', text: `الحالة «${lvl}» من الالتزام بالمهل والردود المتأخرة حالياً.` }))),
      h('section', { class: 'card pad stats' },
        stat('نرد عليهم خلال', days(e.ourMedianDays), e.ourReplies ? `وسيط ${nf.format(e.ourReplies)} رد` : 'لا ردود مسجلة'),
        stat('يردون علينا خلال', days(e.theirMedianDays), e.theirReplies ? `وسيط ${nf.format(e.theirReplies)} رد` : 'لا ردود مسجلة'),
        stat('علينا', nf.format(e.weOwe), e.weOweOverdue ? `${nf.format(e.weOweOverdue)} متأخر` : 'لا متأخر'),
        stat('لنا', nf.format(e.theyOwe), e.theyOweOverdue ? `${nf.format(e.theyOweOverdue)} تجاوز مهلته` : ''),
        stat('المراسلات', nf.format(e.incoming + e.outgoing), `${nf.format(e.incoming)} وارد • ${nf.format(e.outgoing)} صادر`),
        stat('خطابات غائبة', nf.format(e.missing), 'مشار إليها وليست في الأرشيف'))),
    h('section', { class: 'card pad', style: 'margin-top:16px' },
      h('div', { class: 'row', style: 'justify-content:space-between' }, h('h3', {}, icon('report'), 'المراسلات خلال 12 شهراً'),
        h('div', { class: 'legend small' }, h('span', {}, h('i', { class: 'sw in' }), 'وارد'), h('span', {}, h('i', { class: 'sw out' }), 'صادر'))),
      monthlyChart(e, o.months, tip)),
    h('div', { class: 'grid cols-2', style: 'margin-top:16px;align-items:start' },
      h('section', { class: 'card pad' }, h('h3', {}, icon('alert'), `ردود علينا (${nf.format(p.risks.length)})`),
        p.risks.length ? h('div', { class: 'stack', style: 'gap:10px;margin-top:10px' }, p.risks.map(r => riskItem(r, false))) : empty('check', 'لا ردود مطلوبة منكم لهذه الجهة.')),
      h('section', { class: 'card pad' }, h('h3', {}, icon('arrow'), `ردود لنا (${nf.format(p.theyOwe.length)})`),
        p.theyOwe.length ? h('div', { class: 'stack', style: 'gap:8px;margin-top:10px' }, p.theyOwe.map(t => h('div', { class: 'risk-item' + (t.daysLeft < 0 ? ' medium' : '') },
          h('div', { class: 'row', style: 'justify-content:space-between' }, h('span', { class: 'chip ' + (t.daysLeft < 0 ? 'warn' : '') }, icon('clock'), t.daysLeft < 0 ? `تجاوز مهلته بـ ${dayWord(t.daysLeft)}` : `مهلتهم ${fmt.hijri(t.due)}`)),
          h('a', { class: 'risk-title', href: '#/doc/' + encodeURIComponent(t.doc.id) }, h('span', { class: 'num muted', text: t.doc.archiveNo + ' — ' }), t.doc.title),
          t.daysLeft < 0 ? h('a', { class: 'btn sm', style: 'margin-top:6px', href: '#/reply/' + encodeURIComponent(t.doc.id) }, icon('edit'), 'خطاب تعقيب') : null)))
          : empty('arrow', 'لا طلبات بمهلة تنتظر رد الجهة.'),
        p.commitments.length ? h('div', { style: 'margin-top:14px' }, h('h4', { class: 'small', text: 'التزامات قائمة' }), p.commitments.map(c => h('a', { class: 'list-item', href: '#/doc/' + encodeURIComponent(c.documentId) },
          h('span', { class: 'chip ' + (c.side === 'لنا' ? 'in' : 'out'), text: c.side }), h('div', { class: 'grow', text: c.promise }), h('span', { class: 'muted small', text: c.daysLeft < 0 ? 'متأخر' : fmt.hijri(c.due) })))) : null)),
    h('section', { class: 'card', style: 'margin-top:16px' }, h('div', { class: 'card-head' }, h('h2', {}, icon('docs'), 'آخر المراسلات'),
      h('a', { class: 'small', href: '#/docs?q=' + encodeURIComponent(e.name), text: 'كل مراسلات الجهة في البحث' })), p.recent.slice(0, 12).map(docRow)));
}
