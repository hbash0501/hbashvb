// استوديو الرد: مسودة رد على خطاب وارد من المطلوب فيه وسوابق الجهة، بمعاينة حية وطباعة، وحفظ يربطها بالمعاملة.
import { h, icon, api, fmt, nf, mount, toast, state } from './core.js';
import { pageHead, empty } from './views.js';

const docLink = id => '#/doc/' + encodeURIComponent(id);

// توحيد عربي خفيف لقياس تغطية الطلبات في نص الرد.
const STOP = new Set(['نامل', 'نرجو', 'يرجي', 'ارجو', 'امل', 'التكرم', 'تزويدنا', 'موافاتنا', 'افادتنا', 'لديكم', 'لدينا', 'وذلك', 'الذي', 'التي', 'هذا', 'هذه', 'حسب', 'خلال', 'منذ', 'بخصوص', 'تاريخه', 'موعد', 'اقصاه']);
const norm = w => w.replace(/[ً-ٰٟـ]/g, '').replace(/[أإآ]/g, 'ا').replace(/ى/g, 'ي').replace(/ة/g, 'ه');
const stem = w => { w = norm(w); for (const p of ['وال', 'بال', 'فال', 'لل', 'ال']) if (w.startsWith(p) && w.length - p.length >= 3) { w = w.slice(p.length); break; } if (w.length >= 5 && 'وبفل'.includes(w[0])) w = w.slice(1); return w.slice(0, 5); };
const words = t => (t.match(/[ء-ي٠-٩ٰ-ۿ]+/g) || []).map(norm).filter(w => w.length >= 4 && !STOP.has(w)).map(stem);

// النقطة مغطاة حين يتناولها سطر مكتمل؛ سطر ما زال فيه حقل بين قوسين لا يُحسب.
function covered(ask, body) {
  const need = [...new Set(words(ask.gist))];
  if (!need.length) return false;
  const have = new Set(words(body.split('\n').filter(l => !/\[[^\[\]\n]{1,60}\]/.test(l)).join('\n')));
  return need.filter(w => have.has(w)).length / need.length >= 0.5;
}

const placeholders = t => [...new Set(t.match(/\[[^\[\]\n]{1,60}\]/g) || [])];
const store = { get: k => { try { return localStorage.getItem(k) || ''; } catch { return ''; } }, set: (k, v) => { try { localStorage.setItem(k, v); } catch { /* تخزين غير متاح */ } } };

export async function renderReply(view, id) {
  const r = await api('/documents/' + encodeURIComponent(id) + '/reply');
  const inc = r.incoming;
  const signerKey = 'basira.signer.' + (state.me?.userName || '');
  const saved = (() => { try { return JSON.parse(store.get(signerKey) || '{}'); } catch { return {}; } })();
  const f = {
    addressee: h('input', { value: r.draft.addressee, 'aria-label': 'المخاطَب' }),
    subject: h('input', { value: r.draft.subject, 'aria-label': 'الموضوع' }),
    reference: h('textarea', { rows: 2, style: 'min-height:58px', 'aria-label': 'سطر الإشارة' }),
    body: h('textarea', { rows: 9, style: 'min-height:220px', 'aria-label': 'نص الرد' }),
    signerTitle: h('input', { value: saved.title || '', placeholder: 'مثل: مدير عام الموارد البشرية', 'aria-label': 'صفة الموقّع' }),
    signerName: h('input', { value: saved.name || '', placeholder: 'اختياري', 'aria-label': 'اسم الموقّع' }),
    outgoingNo: h('input', { class: 'ltr', placeholder: 'يُكتب عند القيد في الصادر', 'aria-label': 'رقم الصادر' }),
  };
  f.reference.value = r.draft.referenceLine;
  let intent = r.suggestedIntent;
  let edited = false;
  const intentBody = k => r.intents.find(i => i.key === k)?.body || '';
  f.body.value = intentBody(intent);

  const markDone = h('input', { type: 'checkbox', checked: true, style: 'width:auto' });
  const intentBar = h('div', { class: 'seg', role: 'radiogroup', 'aria-label': 'نوع الرد' });
  const drawIntents = () => mount(intentBar, r.intents.map(i => h('button', { type: 'button', 'aria-pressed': String(i.key === intent), onclick: () => {
    if (edited && f.body.value.trim() && !confirm('استبدال نص الرد الحالي بنص «' + i.label + '»؟')) return;
    intent = i.key; f.body.value = i.body; edited = false; drawIntents(); refresh();
  } }, i.label)));
  drawIntents();

  const holes = h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap;margin-top:6px' });
  const askList = h('div', { class: 'stack', style: 'gap:8px' });
  const preview = h('div', { class: 'letter-page', dir: 'rtl' });
  const issueBtn = h('button', { class: 'btn primary', type: 'button', onclick: () => save(true) }, icon('check'), 'إصدار الرد');
  const draftBtn = h('button', { class: 'btn', type: 'button', onclick: () => save(false) }, icon('history'), 'حفظ مسودة للاعتماد');

  // ينتقل إلى الحقل التالي بين قوسين في نص الرد ويحدده للكتابة فوقه.
  const nextHole = () => {
    const t = f.body.value; const from = f.body.selectionEnd || 0;
    const re = /\[[^\[\]\n]{1,60}\]/g; let m, first = null;
    while ((m = re.exec(t))) { first ??= m; if (m.index >= from) break; }
    const hit = m || first;
    if (!hit) return;
    f.body.focus(); f.body.setSelectionRange(hit.index, hit.index + hit[0].length);
  };

  const letterParts = () => ({
    outgoingNo: f.outgoingNo.value.trim(), dateHijri: r.draft.dateHijri, subject: f.subject.value.trim(), addressee: f.addressee.value.trim(),
    referenceLine: f.reference.value.trim(), body: f.body.value.trim(), signerTitle: f.signerTitle.value.trim(), signerName: f.signerName.value.trim(),
  });

  // المعاينة بالترتيب الذي يحفظ به الخادم الخطاب، والحقول الناقصة مظللة.
  const marked = t => t.split(/(\[[^\[\]\n]{1,60}\])/g).map(p => /^\[.*\]$/.test(p) ? h('mark', { text: p }) : p);
  function drawPreview() {
    const p = letterParts();
    const ref = p.referenceLine.replace(/[،,]\s*$/, '');
    const opening = !ref ? p.body : !p.body ? ref + '.' : ref + '، ' + p.body;
    mount(preview,
      h('div', { class: 'lp-head' }, h('b', { text: r.draft.organization || 'الجهة' }),
        h('div', { class: 'lp-meta' }, h('div', { text: 'الرقم: ' + (p.outgoingNo || '……') }), h('div', { text: 'التاريخ: ' + p.dateHijri }), h('div', {}, 'الموضوع: ', ...marked(p.subject)))),
      h('p', { class: 'lp-to' }, h('span', {}, ...marked(p.addressee)), h('span', { text: 'المحترم' })),
      h('p', { text: r.greeting }),
      ...opening.split('\n').map(line => h('p', { class: 'lp-body' }, ...marked(line))),
      h('p', { class: 'lp-close', text: r.closing }),
      h('div', { class: 'lp-sign' }, h('b', {}, ...marked(p.signerTitle || '[صفة الموقّع]')), p.signerName ? h('div', { text: p.signerName }) : null));
  }

  function refresh() {
    const all = Object.values(letterParts()).join('\n');
    const miss = placeholders(all + (f.signerTitle.value.trim() ? '' : ' [صفة الموقّع]'));
    mount(holes, miss.length
      ? [h('span', { class: 'chip warn' }, icon('alert'), `حقول تحتاج إكمالاً: ${nf.format(miss.length)}`), h('button', { class: 'btn sm', type: 'button', onclick: nextHole }, 'الحقل التالي'), ...miss.slice(0, 6).map(x => h('span', { class: 'chip', text: x }))]
      : [h('span', { class: 'chip ok' }, icon('check'), 'لا حقول ناقصة')]);
    issueBtn.disabled = miss.length > 0;
    issueBtn.title = miss.length ? 'أكمل الحقول المظللة قبل الإصدار' : '';
    const body = f.body.value;
    mount(askList, r.asks.length ? r.asks.map(a => {
      const ok = covered(a, body);
      return h('div', { class: 'ask' + (ok ? ' done' : '') }, h('span', { class: 'ask-mark', 'aria-label': ok ? 'مغطى في الرد' : 'لم يُغطَّ بعد' }, ok ? icon('check') : ''),
        h('div', {}, h('span', { class: 'chip', text: a.kind }), h('p', { class: 'small', style: 'margin:4px 0 0', text: a.text })));
    }) : h('p', { class: 'muted small', text: 'لم يُرصد في الخطاب طلب صريح؛ غالباً للإحاطة.' }));
    drawPreview();
  }
  for (const el of Object.values(f)) el.addEventListener('input', () => { if (el === f.body) edited = true; refresh(); });

  async function save(issue) {
    const p = letterParts();
    store.set(signerKey, JSON.stringify({ title: p.signerTitle, name: p.signerName }));
    if (!issue && placeholders(p.body).length && !confirm('في الرد حقول لم تُستكمل. حفظه مسودة للاعتماد؟')) return;
    try {
      const res = await api('/documents/' + encodeURIComponent(id) + '/reply', { method: 'POST', body: { ...p, issue, markDone: issue && markDone.checked } });
      toast(issue ? `صدر الرد ${res.archiveNo} وارتبط بالمعاملة` : `حُفظت المسودة ${res.archiveNo} بانتظار الاعتماد`);
      location.hash = docLink(res.id);
    } catch (e) { toast(e.message, { error: true }); }
  }

  // يُطبع الخطاب وحده: نسخة منه في أعلى الصفحة، وكل ما سواها مخفي أثناء الطباعة.
  const printLetter = () => {
    const root = h('div', { class: 'print-root' }, preview.cloneNode(true));
    document.body.append(root);
    document.body.classList.add('print-letter');
    const done = () => { document.body.classList.remove('print-letter'); root.remove(); window.removeEventListener('afterprint', done); };
    window.addEventListener('afterprint', done);
    window.print();
  };
  const copyText = async () => {
    try { await navigator.clipboard.writeText(preview.innerText); toast('نُسخ نص الخطاب'); } catch { toast('تعذر النسخ', { error: true }); }
  };

  const dl = r.deadline;
  const dueChip = dl ? h('span', { class: 'chip ' + (dl.daysLeft < 0 ? 'danger' : dl.daysLeft <= 3 ? 'warn' : 'ok'), title: dl.source },
    icon('clock'), dl.daysLeft < 0 ? `المهلة انقضت منذ ${nf.format(-dl.daysLeft)} يوماً` : dl.daysLeft === 0 ? 'المهلة تنتهي اليوم' : `تبقى ${nf.format(dl.daysLeft)} يوماً (${dl.hijri})`) : null;

  const precedentCard = p => h('div', { class: 'card pad precedent' },
    h('div', { class: 'row', style: 'justify-content:space-between;gap:8px' }, h('b', { class: 'small', text: p.incomingTitle }), h('span', { class: 'chip ok', title: 'تشابه الكلمات بين الخطابين', text: nf.format(p.similarity) + '%' })),
    h('div', { class: 'muted small', style: 'margin-top:2px' }, 'رُدّ عليه بـ ', h('bdi', {}, h('a', { href: docLink(p.replyId), class: 'num', text: p.replyArchiveNo })), p.replyDate ? h('bdi', { text: ' — ' + fmt.hijri(p.replyDate) }) : ''),
    p.sharedTerms.length ? h('div', { class: 'chips', style: 'margin-top:6px' }, p.sharedTerms.map(t => h('span', { class: 'chip', text: t }))) : null,
    h('p', { class: 'small precedent-body', text: p.body }),
    h('div', { class: 'row', style: 'gap:6px' },
      h('button', { class: 'btn sm primary', type: 'button', onclick: () => {
        if (edited && f.body.value.trim() && !confirm('استبدال نص الرد الحالي بصياغة هذه السابقة؟')) return;
        f.body.value = p.body; edited = false; intent = ''; drawIntents(); refresh(); f.body.focus();
      } }, icon('edit'), 'استخدم هذه الصياغة'),
      h('a', { class: 'btn sm ghost', href: docLink(p.incomingId) }, 'الخطاب السابق')));

  const field = (label, el, hint, cls = '') => h('div', { class: cls }, h('label', { class: 'f', text: label }), el, hint ? h('p', { class: 'hint', text: hint }) : null);

  mount(view,
    pageHead('استوديو الرد', `رد على ${inc.archiveNo}: ${inc.title}`, h('a', { class: 'btn', href: docLink(id) }, icon('arrow'), 'الخطاب الوارد')),
    h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap;margin-bottom:14px' },
      inc.entity ? h('span', { class: 'chip in' }, icon('inbound'), inc.entity) : null,
      inc.externalNo ? h('span', { class: 'chip num', text: 'رقمهم ' + inc.externalNo }) : null,
      inc.dateHijri ? h('span', { class: 'chip', text: inc.dateHijri }) : null,
      dueChip,
      inc.willRegister ? h('span', { class: 'chip warn', title: 'الخطاب بلا رقم؛ يُقيَّد برقم الأرشيف عند الحفظ ليرتبط به الرد', text: 'سيُقيَّد برقم ' + inc.archiveNo }) : null),
    r.existing.length ? h('div', { class: 'card pad', style: 'margin-bottom:14px;border-color:var(--accent)' }, h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap' }, icon('alert'),
      h('span', { text: 'لهذا الخطاب رد محفوظ:' }), r.existing.map(e => h('a', { class: 'chip ' + (e.pending ? 'warn' : 'ok'), href: docLink(e.id), text: `${e.archiveNo} — ${e.pending ? 'بانتظار الاعتماد' : e.status}` })))) : null,
    h('div', { class: 'studio' },
      h('div', { class: 'stack' },
        h('section', { class: 'card pad' },
          h('h3', {}, icon('edit'), 'الرد'),
          h('label', { class: 'f', text: 'نوع الرد' }), intentBar,
          h('div', { class: 'form', style: 'margin-top:10px' },
            field('المخاطَب', f.addressee, '', 'full'),
            field('الموضوع', f.subject, '', 'full'),
            field('سطر الإشارة', f.reference, 'يحمل رقم خطابهم وتاريخه؛ به يرتبط الرد بأصله في المعاملة ويسقط «بانتظار الرد».', 'full'),
            field('نص الرد', f.body, 'ما بين قوسين معقوفين حقول تُستكمل. يبدأ النص بعد سطر الإشارة مباشرة.', 'full'),
            h('div', { class: 'full' }, holes),
            field('صفة الموقّع', f.signerTitle, 'تُحفظ لردودك القادمة'),
            field('اسم الموقّع', f.signerName),
            field('رقم الصادر', f.outgoingNo)),
          h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap;margin-top:14px;align-items:center' },
            r.can.issue ? issueBtn : null, draftBtn,
            h('button', { class: 'btn ghost', type: 'button', onclick: printLetter }, icon('report'), 'طباعة'),
            h('button', { class: 'btn ghost', type: 'button', onclick: copyText }, icon('docs'), 'نسخ النص'),
            r.can.issue ? h('label', { class: 'small row', style: 'gap:6px;margin-inline-start:auto' }, markDone, 'تعليم الوارد «منجز» عند الإصدار') : null)),
        h('section', { class: 'card pad' }, h('h3', {}, icon('eye'), 'معاينة الخطاب'), preview)),
      h('aside', { class: 'stack' },
        h('section', { class: 'card pad panel' }, h('h3', {}, icon('check'), `ما يطلبه الخطاب (${nf.format(r.asks.length)})`),
          h('p', { class: 'muted small', text: 'تُعلَّم النقطة حين يتناولها نص الرد.' }), askList),
        h('section', { class: 'card pad panel' }, h('h3', {}, icon('history'), 'سوابق من أرشيفكم'),
          r.precedents.length
            ? h('div', { class: 'stack', style: 'gap:10px' }, h('p', { class: 'muted small', text: 'ردود أصدرتموها على خطابات مشابهة، مهيأة لهذا الخطاب.' }), r.precedents.map(precedentCard))
            : empty('history', 'لا توجد بعد ردود سابقة على خطابات مشابهة. كل رد تصدره من هنا يصبح سابقة لما يشبهه.')))));
  refresh();
}
