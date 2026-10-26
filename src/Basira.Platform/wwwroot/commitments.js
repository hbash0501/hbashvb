// سجل الالتزامات: ما وعدنا به في صادرنا («علينا») وما وُعدنا به في واردهم («لنا»)، بمواعيدها وحالتها.
import { h, icon, api, fmt, nf, mount, toast, can } from './core.js';
import { pageHead, empty } from './views.js';

const docLink = id => '#/doc/' + encodeURIComponent(id);
const STATES = { open: ['قائم', ''], overdue: ['متأخر', 'danger'], fulfilled: ['موفّى', 'ok'], dismissed: ['ليس التزاماً', ''] };

// الجملة كاملة وعبارة الوعد منها مظللة.
export function promiseText(c) {
  const i = c.sentence.indexOf(c.promise.replace(/…$/, ''));
  if (i < 0) return h('span', { text: c.sentence });
  const end = i + c.promise.replace(/…$/, '').length;
  return h('span', {}, c.sentence.slice(0, i), h('mark', { class: 'due', text: c.sentence.slice(i, end) }), c.sentence.slice(end));
}

export function commitmentCard(c, onChange) {
  const [label, cls] = STATES[c.state];
  const decide = async (state, note) => {
    try { await api('/commitments/' + encodeURIComponent(c.key), { method: 'POST', body: { state, note } }); toast(state === 'fulfilled' ? 'سُجّل الوفاء' : state === 'dismissed' ? 'استُبعد من السجل' : 'أُعيد قائماً'); onChange?.(); }
    catch (e) { toast(e.message, { error: true }); }
  };
  const active = c.state === 'open' || c.state === 'overdue';
  const days = c.daysLeft;
  // التعقيب على وعدهم يُصاغ على خطابهم؛ والوفاء بوعدنا يُصاغ على الوارد الذي كان خطابنا رداً عليه.
  const studio = c.side === 'لنا' ? '#/reply/' + encodeURIComponent(c.doc.id) + '?commitment=' + encodeURIComponent(c.key)
    : c.respondsTo ? '#/reply/' + encodeURIComponent(c.respondsTo.id) + '?commitment=' + encodeURIComponent(c.key) : null;
  return h('div', { class: 'card pad pledge ' + c.state },
    h('div', { class: 'pledge-head' },
      active ? h('div', { class: 'badge-days' + (days < 0 ? ' danger' : '') }, nf.format(Math.abs(days)), h('small', { text: days < 0 ? 'متأخر' : days === 0 ? 'اليوم' : 'يوماً' }))
        : h('div', { class: 'badge-days muted' }, icon(c.state === 'fulfilled' ? 'check' : 'x')),
      h('div', { class: 'grow' },
        h('p', { class: 'pledge-text' }, promiseText(c)),
        h('div', { class: 'muted small row', style: 'gap:6px;flex-wrap:wrap;margin-top:6px' },
          h('span', { class: 'chip ' + cls, text: label }),
          c.party ? h('span', { class: 'chip ' + (c.side === 'لنا' ? 'in' : 'out'), text: c.party }) : null,
          h('a', { href: docLink(c.doc.id) }, h('bdi', { class: 'num', text: c.doc.archiveNo }), ' — ', c.doc.title),
          h('span', { title: c.dueSource }, icon('clock'), ' ', fmt.hijri(c.due), c.defaultDue ? ' (افتراضي)' : '')),
        c.fulfilledReason ? h('div', { class: 'small', style: 'margin-top:6px' }, icon('check'), ' ', c.fulfilledBy ? h('a', { href: docLink(c.fulfilledBy.id), text: c.fulfilledReason }) : c.fulfilledReason) : null)),
    can('workflow') ? h('div', { class: 'row', style: 'gap:6px;flex-wrap:wrap;margin-top:10px;justify-content:flex-end' },
      active && studio && can('import') ? h('a', { class: 'btn sm primary', href: studio }, icon('edit'), c.side === 'لنا' ? 'خطاب تعقيب' : 'صياغة الوفاء') : null,
      active ? h('button', { class: 'btn sm', type: 'button', onclick: () => { const n = prompt('كيف تم الوفاء؟ (اختياري)'); if (n !== null) decide('fulfilled', n); } }, icon('check'), 'تم الوفاء') : null,
      c.state === 'fulfilled' && c.fulfilledBy ? h('button', { class: 'btn sm ghost', type: 'button', title: 'الخطاب اللاحق لم يفِ بهذا الوعد', onclick: () => decide('open', '') }, 'لم يُوفَ بعد') : null,
      c.state === 'fulfilled' && !c.fulfilledBy || c.state === 'dismissed' ? h('button', { class: 'btn sm ghost', type: 'button', onclick: () => decide('open', '') }, icon('refresh'), 'إعادة فتح') : null,
      active ? h('button', { class: 'btn sm ghost', type: 'button', title: 'العبارة ليست وعداً يُتابع', onclick: () => decide('dismissed', '') }, icon('x'), 'ليس التزاماً') : null) : null);
}

export async function renderCommitments(view, params) {
  let side = params.get('side') === 'theirs' ? 'theirs' : 'ours';
  let filter = params.get('state') || 'active';
  const sideSeg = h('div', { class: 'seg', role: 'tablist' });
  const stateSeg = h('div', { class: 'seg' });
  const list = h('div', { class: 'stack', style: 'gap:10px' });
  const go = () => history.replaceState(null, '', `#/commitments?side=${side}&state=${filter}`);

  async function load() {
    const r = await api(`/commitments?side=${side}&state=${filter}`);
    const n = r.counts[side];
    mount(sideSeg, [['ours', 'علينا', 'ما وعدنا به في صادرنا', r.counts.ours], ['theirs', 'لنا', 'ما وعدتنا به الجهات في واردها', r.counts.theirs]].map(([k, l, t, c]) =>
      h('button', { type: 'button', role: 'tab', 'aria-pressed': String(k === side), title: t, onclick: () => { side = k; go(); load(); } }, l, ' ', h('span', { class: 'chip' + (c.overdue ? ' danger' : ''), text: nf.format(c.open + c.overdue) }))));
    mount(stateSeg, [['active', 'القائمة', n.open + n.overdue], ['overdue', 'المتأخرة', n.overdue], ['fulfilled', 'الموفّاة', n.fulfilled], ['dismissed', 'المستبعدة', n.dismissed]].map(([k, l, c]) =>
      h('button', { type: 'button', 'aria-pressed': String(k === filter), onclick: () => { filter = k; go(); load(); } }, `${l} (${nf.format(c)})`)));
    mount(list, r.items.length ? r.items.map(c => commitmentCard(c, load))
      : empty(filter === 'active' || filter === 'overdue' ? 'check' : 'flag', side === 'ours'
        ? (filter === 'overdue' ? 'لا التزامات متأخرة علينا.' : 'لا التزامات هنا. ترصد المنصة الوعود في خطاباتكم الصادرة («سنوافيكم…»، «سيتم تزويدكم…») تلقائياً.')
        : (filter === 'overdue' ? 'لا وعود متأخرة لنا عند الجهات.' : 'لا التزامات هنا. ترصد المنصة وعود الجهات في خطاباتها الواردة تلقائياً.')));
  }

  mount(view,
    pageHead('الالتزامات', 'ما وعدنا به وما وُعدنا به في المراسلات، ومتى يحين، وهل وُفّي. يُرصد من نصوص الخطابات، ويُرجَّح الوفاء بخطاب لاحق من الواعد في المعاملة نفسها.'),
    h('div', { class: 'filters' }, sideSeg, stateSeg),
    list);
  await load();
}
