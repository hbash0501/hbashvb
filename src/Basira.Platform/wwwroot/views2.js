// بصيرة — الإدراج، الأسئلة، الإدارة، الدخول، ولوحة الأوامر.
import { h, icon, api, fmt, nf, mount, markTerms, toast, can, state, onEvent } from './core.js';
import { pageHead, empty, docRow } from './views.js';

// ======================= الإدراج =======================
const STAGE_DONE = new Set(['done', 'failed']);
function jobRow(j) {
  const cls = j.state === 'failed' ? 'danger' : j.state === 'done' ? 'ok' : '';
  return h('div', { class: 'job', 'data-job': j.id },
    h('div', { class: 'row' }, h('a', { href: '#/doc/' + encodeURIComponent(j.documentId), style: 'font-weight:600', text: j.title }), h('span', { class: 'chip ' + cls, text: j.state === 'queued' ? 'بانتظار الدور' : j.state === 'running' ? `${j.progress}%` : j.state === 'done' ? 'اكتملت' : 'تعذرت' })),
    h('div', { class: 'muted small', text: j.stage + (j.message ? ' — ' + j.message : '') }),
    STAGE_DONE.has(j.state) ? null : h('div', { class: 'bar', style: 'margin-top:6px' }, h('i', { style: `width:${Math.max(4, j.progress)}%` })));
}

export async function renderInbox(view) {
  if (!can('import')) { mount(view, empty('shield', 'لا تملك صلاحية الإدراج.')); return; }
  const meta = state.meta || {};
  let direction = 'وارد';
  const dirSeg = h('div', { class: 'seg', role: 'group', 'aria-label': 'الاتجاه' });
  const drawDir = () => mount(dirSeg, (meta.directions || ['وارد', 'صادر', 'داخلي']).map(d => h('button', { type: 'button', 'aria-pressed': String(d === direction), text: d, onclick: () => { direction = d; drawDir(); } })));
  drawDir();
  const category = h('input', { list: 'inbox-cats', placeholder: 'تلقائي من المحتوى', 'aria-label': 'التصنيف' });
  const conf = h('select', { 'aria-label': 'السرية' }, (meta.confidentialities || ['عادي']).map(c => h('option', { value: c, text: c })));
  const fileInput = h('input', { type: 'file', multiple: true, hidden: true, accept: '.pdf,.png,.jpg,.jpeg,.tif,.tiff,.bmp,.gif,.webp,.docx,.txt' });
  const results = h('div');
  const jobsBox = h('div');

  async function upload(files) {
    if (!files.length) return;
    const form = new FormData();
    for (const f of files) form.append('files', f, f.name);
    form.append('direction', direction); form.append('category', category.value); form.append('confidentiality', conf.value);
    mount(results, h('div', { class: 'skeleton', style: 'min-height:40px' }));
    try {
      const r = await api('/inbox', { method: 'POST', form });
      mount(results, r.map(x => h('div', { class: 'list-item' }, x.error ? icon('alert') : x.duplicate ? icon('link') : icon('check'),
        h('div', { class: 'grow' }, h('span', { class: 't', text: x.name }), h('span', { class: 'muted small', text: x.error ? x.error : x.duplicate ? `موجود مسبقاً بالرقم ${x.archiveNo} — لم يُكرر` : `أُدرج بالرقم ${x.archiveNo}، والمعالجة جارية` })),
        x.id ? h('a', { class: 'btn sm', href: '#/doc/' + encodeURIComponent(x.id) }, 'فتح') : null)));
      loadJobs();
    } catch (e) { mount(results, h('p', { class: 'error', text: e.message })); }
  }
  fileInput.addEventListener('change', () => { upload([...fileInput.files]); fileInput.value = ''; });
  const drop = h('div', { class: 'drop', tabindex: 0, role: 'button', 'aria-label': 'اختر ملفات أو أفلتها هنا', onclick: () => fileInput.click(), onkeydown: e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); fileInput.click(); } } },
    icon('upload'), h('h2', { text: 'أفلت الخطابات هنا أو اخترها من جهازك' }), h('p', { class: 'muted', text: 'PDF، صور ممسوحة، Word، أو نص. يُستخرج النص والرقم والتاريخ والموضوع والإشارات والمهل تلقائياً، ويُرفض المكرر.' }));
  drop.addEventListener('dragover', e => { e.preventDefault(); drop.classList.add('over'); });
  drop.addEventListener('dragleave', () => drop.classList.remove('over'));
  drop.addEventListener('drop', e => { e.preventDefault(); drop.classList.remove('over'); upload([...e.dataTransfer.files]); });

  // ===== الكاميرا =====
  const camBox = h('div', { class: 'camera', hidden: true });
  let stream;
  async function openCamera() {
    try {
      stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment', width: { ideal: 2480 }, height: { ideal: 3508 } } });
      const video = h('video', { autoplay: true, playsinline: true, muted: true });
      video.srcObject = stream;
      mount(camBox, video, h('div', { class: 'row', style: 'margin-top:10px' },
        h('button', { class: 'btn primary', onclick: () => {
          const c = document.createElement('canvas'); c.width = video.videoWidth; c.height = video.videoHeight;
          c.getContext('2d').drawImage(video, 0, 0);
          c.toBlob(b => { upload([new File([b], `التقاط-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.jpg`, { type: 'image/jpeg' })]); }, 'image/jpeg', 0.92);
        } }, icon('camera'), 'التقاط وإدراج'),
        h('button', { class: 'btn', onclick: closeCamera }, 'إغلاق الكاميرا')));
      camBox.hidden = false;
    } catch (e) { toast('تعذر فتح الكاميرا: ' + (e.message || e.name), { error: true }); }
  }
  function closeCamera() { stream?.getTracks().forEach(t => t.stop()); stream = null; camBox.hidden = true; mount(camBox); }

  async function loadJobs() {
    const jobs = await api('/jobs?take=20');
    mount(jobsBox, jobs.length ? jobs.map(jobRow) : empty('clock', 'لا مهام معالجة بعد.'));
  }
  const off = onEvent(msg => {
    if (msg.type !== 'job' || !document.body.contains(jobsBox)) { if (!document.body.contains(jobsBox)) { off(); closeCamera(); } return; }
    const row = jobsBox.querySelector(`[data-job="${msg.payload.id}"]`);
    if (row) row.replaceWith(jobRow(msg.payload)); else { jobsBox.querySelector('.empty')?.remove(); jobsBox.prepend(jobRow(msg.payload)); }
  });

  mount(view, pageHead('الإدراج', 'أدخل الخطابات مرة واحدة؛ تتكفل بصيرة بقراءتها وربطها بمعاملاتها.'),
    h('div', { class: 'grid cols-3' },
      h('div', { class: 'stack' },
        h('section', { class: 'card pad' }, h('div', { class: 'form' },
          h('div', {}, h('label', { class: 'f', text: 'الاتجاه' }), dirSeg),
          h('div', {}, h('label', { class: 'f', text: 'السرية' }), conf),
          h('div', { class: 'full' }, h('label', { class: 'f', text: 'التصنيف' }), category, h('datalist', { id: 'inbox-cats' }, (meta.categories || []).map(c => h('option', { value: c })))))),
        drop, fileInput,
        h('div', { class: 'row' }, h('button', { class: 'btn', onclick: openCamera }, icon('camera'), 'التقاط بالكاميرا'), h('span', { class: 'muted small', text: 'للسكانر: اضبطه ليحفظ في «مجلد الإدراج» المحدد في الإدارة، فتُدرج الملفات تلقائياً.' })),
        camBox, results),
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', {}, icon('refresh'), 'المعالجة')), h('div', { class: 'card-body' }, jobsBox))));
  await loadJobs();
}

// ======================= اسأل بصيرة =======================
export async function renderAsk(view, params) {
  const q = h('input', { type: 'search', placeholder: 'مثال: ما الذي طلبته وزارة المالية بشأن المشاريع المتعثرة؟', 'aria-label': 'سؤال', value: params.get('q') || '' });
  const out = h('div');
  async function ask() {
    const text = q.value.trim(); if (!text) return;
    mount(out, h('div', { class: 'skeleton' }));
    try {
      const r = await api('/ask?' + new URLSearchParams({ q: text }));
      mount(out, r.found ? [h('p', { class: 'muted', style: 'margin-bottom:12px', text: 'مقتطفات أصلية مرتبطة بسؤالك، مرقّمة بمصادرها. افتح المصدر للتحقق من سياقه.' }),
        ...r.evidence.map(e => h('section', { class: 'card pad', style: 'margin-bottom:12px' },
          h('div', { class: 'row' }, h('span', { class: 'chip ok', text: `[${e.number}]` }), h('a', { href: '#/doc/' + encodeURIComponent(e.documentId), style: 'font-weight:700', text: e.title }), h('span', { class: 'muted small num', text: e.archiveNo }),
            e.thread ? h('a', { class: 'btn sm', href: '#/thread/' + encodeURIComponent(e.thread), style: 'margin-inline-start:auto' }, icon('thread'), 'المعاملة') : null),
          e.restricted ? h('p', { class: 'muted', style: 'margin-top:8px' }, icon('shield'), ' مصدر حساس؛ محتواه مقيّد لدورك.') : h('p', { style: 'margin-top:8px;white-space:pre-wrap;line-height:1.9' }, markTerms(e.quote, text.split(/\s+/).join('،')))))]
        : empty('search', 'لم أجد نصوصاً مناسبة. جرّب اسم الجهة أو رقم المعاملة أو كلمة من الموضوع.'));
    } catch (e) { mount(out, empty('alert', e.message)); }
  }
  q.addEventListener('keydown', e => { if (e.key === 'Enter') ask(); });
  mount(view, pageHead('اسأل بصيرة', 'إجابات من نصوص أرشيفك وحده، بأدلة مرقّمة، دون إرسال أي شيء إلى خدمة خارجية.'),
    h('div', { class: 'filters' }, h('div', { class: 'search' }, icon('spark'), q), h('button', { class: 'btn primary', onclick: ask }, 'ابحث عن الأدلة')), out);
  q.focus();
  if (q.value) ask();
}

// ======================= الإدارة =======================
export async function renderAdmin(view, tab) {
  const tabs = [['users', 'المستخدمون', 'users'], ['settings', 'الإعدادات', 'sliders'], ['taxonomy', 'التصنيفات', 'folder'], ['migrate', 'ترحيل النسخة السابقة', 'folder'], ['audit', 'سجل التدقيق', 'shield'], ['report', 'تقرير الصحة', 'report']]
    .filter(([k]) => state.me.isAdmin || k === 'audit' && can('audit') || k === 'report' && can('export') || k === 'taxonomy' && can('manage_categories'));
  if (!tabs.length) { mount(view, empty('shield', 'لا تملك صلاحيات إدارية.')); return; }
  if (!tabs.some(t => t[0] === tab)) tab = tabs[0][0];
  const body = h('div');
  mount(view, pageHead('الإدارة', 'المستخدمون والإعدادات والترحيل والتدقيق.'),
    h('div', { class: 'seg', style: 'margin-bottom:16px' }, tabs.map(([k, l, i]) => h('button', { type: 'button', 'aria-pressed': String(k === tab), onclick: () => location.hash = '#/admin/' + k }, icon(i), ' ', l))), body);
  if (tab === 'users') await adminUsers(body);
  else if (tab === 'settings') await adminSettings(body);
  else if (tab === 'migrate') adminMigrate(body);
  else if (tab === 'taxonomy') await adminTaxonomy(body);
  else if (tab === 'audit') await adminAudit(body);
  else adminReport(body);
}

// ===== التصنيفات: الشجرة، والدقة الفعلية من قرارات المستخدمين، وتصنيف الأرشيف =====
async function adminTaxonomy(body) {
  const o = await api('/classification/overview');
  const tax = o.taxonomy.map(c => ({ name: c.name, keywords: c.keywords.join('، '), subs: c.subs.map(s => ({ name: s.name, keywords: s.keywords.join('، ') })) }));
  const split = v => v.split(/[،,;\n]/).map(x => x.trim()).filter(Boolean);
  const editor = h('div', { class: 'stack' });
  const err = h('p', { class: 'error', role: 'alert' });
  const draw = () => mount(editor, tax.map((c, ci) => h('div', { class: 'card pad tax-cat' },
    h('div', { class: 'row', style: 'gap:8px' }, h('input', { value: c.name, 'aria-label': 'اسم التصنيف العام', style: 'font-weight:700;flex:1', oninput: e => { c.name = e.target.value; } }),
      h('button', { class: 'btn sm ghost', type: 'button', title: 'حذف التصنيف', onclick: () => { if (confirm(`حذف «${c.name}» من الشجرة؟ لا يمس المستندات المصنفة به.`)) { tax.splice(ci, 1); draw(); } } }, icon('x'))),
    h('label', { class: 'f', text: 'كلمات عامة دالة' }), h('input', { value: c.keywords, placeholder: 'كلمات أو عبارات مفصولة بفاصلة', oninput: e => { c.keywords = e.target.value; } }),
    h('div', { class: 'tax-subs' }, c.subs.map((s, si) => h('div', { class: 'tax-sub' },
      h('input', { value: s.name, 'aria-label': 'اسم التصنيف الفرعي', oninput: e => { s.name = e.target.value; } }),
      h('input', { value: s.keywords, placeholder: 'الكلمات الدالة؛ «!» قبل الكلمة القوية', 'aria-label': 'كلمات التصنيف الفرعي', oninput: e => { s.keywords = e.target.value; } }),
      h('button', { class: 'btn sm ghost', type: 'button', title: 'حذف', onclick: () => { c.subs.splice(si, 1); draw(); } }, icon('x'))))),
    h('button', { class: 'btn sm', type: 'button', onclick: () => { c.subs.push({ name: '', keywords: '' }); draw(); } }, icon('plus'), 'تصنيف فرعي'))));
  draw();
  const save = async () => {
    err.textContent = '';
    try {
      await api('/taxonomy', { method: 'PUT', body: { categories: tax.map(c => ({ name: c.name, keywords: split(c.keywords), subs: c.subs.map(s => ({ name: s.name, keywords: split(s.keywords) })) })) } });
      state.meta = await api('/meta').catch(() => state.meta);
      toast('حُفظت شجرة التصنيف');
    } catch (e) { err.textContent = e.message; }
  };
  const run = async scope => {
    try { const r = await api('/classification/reclassify', { method: 'POST', body: { scope } }); toast(`صُنّف ${nf.format(r.changed)} مستنداً`); renderAdmin(body.parentElement, 'taxonomy'); }
    catch (e) { toast(e.message, { error: true }); }
  };
  const fb = o.feedback;
  const stat = (n, label) => h('div', { class: 'card kpi' }, h('span', { class: 'label', text: label }), h('div', { class: 'value', text: n == null ? '—' : typeof n === 'number' ? nf.format(n) : n }));
  mount(body,
    h('div', { class: 'card pad', style: 'margin-bottom:14px' },
      h('h3', {}, icon('spark'), 'التصنيف الآلي'),
      h('p', { class: 'muted small', text: 'تُصنَّف المستندات عند إدراجها بالكلمات الدالة في هذه الشجرة، وبما تتعلمه المنصة من المستندات التي صنّفتموها أو أكدتم تصنيفها. التصنيف غير الواثق يظهر «مقترحاً» حتى يُؤكد.' }),
      h('div', { class: 'kpis', style: 'margin-top:10px' },
        stat(o.counts.total, 'مستند'), stat(o.counts.unclassified, 'غير مصنّف'), stat(o.counts.auto, 'تصنيف آلي لم يُراجع'), stat(o.counts.training, 'مثال يتعلم منه'),
        stat(fb.accuracy == null ? null : fb.accuracy + '%', `دقة التصنيف الآلي حسب مراجعاتكم (${nf.format(fb.reviewed)})`),
        stat(fb.confidentAccuracy == null ? null : fb.confidentAccuracy + '%', `دقة التصنيف الواثق (${nf.format(fb.confidentReviewed)})`)),
      h('div', { class: 'row', style: 'gap:8px;margin-top:12px;flex-wrap:wrap' },
        h('button', { class: 'btn primary', onclick: () => run('unclassified') }, icon('spark'), 'صنّف المستندات غير المصنفة'),
        h('button', { class: 'btn', onclick: () => run('auto') }, icon('refresh'), 'أعد التصنيف الآلي غير المؤكد'),
        h('button', { class: 'btn', title: 'للعناوين الآلية وما بقي باسم الملف؛ لا يمس عنواناً كتبه مستخدم', onclick: async () => {
          try { const r = await api('/titles/rebuild', { method: 'POST' }); toast(`حُدّث ${nf.format(r.changed)} عنواناً من المحتوى`); }
          catch (e) { toast(e.message, { error: true }); }
        } }, icon('edit'), 'أعد توليد العناوين من المحتوى'),
        h('a', { class: 'btn ghost', href: '#/docs?filter=autocat' }, icon('eye'), 'مراجعة التصنيفات المقترحة'))),
    o.unmapped.length ? h('div', { class: 'card pad', style: 'margin-bottom:14px' },
      h('h3', {}, icon('alert'), 'تصنيفات في الأرشيف خارج الشجرة'),
      h('p', { class: 'muted small', text: 'لا يتعلم منها المصنِّف حتى تُضاف إلى الشجرة (أو تُوحَّد مع تصنيف قائم بتعديل المستندات).' }),
      h('div', { class: 'chips' }, o.unmapped.map(u => h('button', { class: 'chip', title: 'إضافة إلى الشجرة', onclick: () => { tax.push({ name: u.name, keywords: '', subs: [] }); draw(); toast(`أُضيف «${u.name}»؛ احفظ الشجرة`); } }, icon('plus'), `${u.name} (${nf.format(u.count)})`)))) : null,
    state.me.isAdmin ? semanticPanel() : null,
    h('div', { class: 'row', style: 'justify-content:space-between;margin-bottom:10px' }, h('h3', { text: 'شجرة التصنيف' }),
      h('div', { class: 'row', style: 'gap:8px' }, h('button', { class: 'btn', onclick: () => { tax.push({ name: 'تصنيف جديد', keywords: '', subs: [] }); draw(); } }, icon('plus'), 'تصنيف عام'), h('button', { class: 'btn primary', onclick: save }, icon('check'), 'حفظ الشجرة'))),
    err, editor);
}

// ===== النموذج الدلالي المحلي: مكانه وحالته وتنزيله ومعايرته =====
function semanticPanel() {
  const box = h('div', { class: 'card pad', style: 'margin-bottom:14px' });
  let timer = null;
  const pct = x => x == null ? '—' : x + '%';
  const draw = st => {
    clearTimeout(timer);
    const cal = st.calibration?.current ? st.calibration.cal : null;
    const dl = st.download;
    const busy = Boolean(dl.running || st.activity);
    const act = async (path, msg) => {
      try { const r = await api('/admin/semantic/' + path, { method: 'POST' }); if (msg) toast(msg); load(r.loaded !== undefined ? r : null); }
      catch (e) { toast(e.message, { error: true }); load(); }
    };
    mount(box,
      h('h3', {}, icon('spark'), 'النموذج الدلالي (فهم المعنى)'),
      h('p', { class: 'muted small', text: 'نموذج يعمل على هذا الجهاز وحده يفهم معنى الخطاب لا كلماته فقط، فيصنّف ما خلا من الكلمات الدالة. يُدمج مع القواعد بوزن تحدده المعايرة على عينات مصنفة، ولا يُستخدم إن لم يرفع الدقة.' }),
      st.loaded
        ? h('div', { class: 'stack', style: 'gap:6px;margin-top:8px' },
            h('div', {}, h('span', { class: 'chip ok', text: 'محمّل' }), ' ', h('b', { text: st.model.name }), h('span', { class: 'muted small', text: ` — ${st.model.file}، ${nf.format(st.model.dimension)} بُعداً` })),
            h('div', { class: 'muted small ltr', text: st.model.folder }))
        : h('div', { class: 'stack', style: 'gap:6px;margin-top:8px' },
            h('div', {}, h('span', { class: 'chip warn', text: 'غير موجود' }), ' ', h('span', { class: 'small', text: 'التصنيف يعمل بالقواعد وما يتعلمه من الأرشيف. ضع ملفات النموذج (‎.onnx وtokenizer.json) في أحد المجلدات التالية، أو نزّله:' })),
            h('ul', { class: 'small ltr', style: 'margin:0' }, st.candidates.map(c => h('li', { text: c })))),
      st.error ? h('p', { class: 'error', text: st.error }) : null,
      st.preparationError ? h('p', { class: 'error', text: st.preparationError }) : null,
      st.activity ? h('p', { class: 'small' }, icon('refresh'), ' ', st.activity) : null,
      dl.running ? h('p', { class: 'small', text: `تنزيل ${dl.file}: ${nf.format(Math.round(dl.received / 1048576))}${dl.total ? ' / ' + nf.format(Math.round(dl.total / 1048576)) : ''} ميجابايت` }) : null,
      dl.error ? h('p', { class: 'error', text: dl.error }) : null,
      cal ? h('div', { style: 'margin-top:10px' },
        h('div', { class: 'table-wrap' }, h('table', {},
          h('thead', {}, h('tr', {}, [`على ${nf.format(cal.heldOutCount)} عينة محجوزة`, 'الكلمات الدالة وحدها', 'مع النموذج الدلالي'].map(x => h('th', { text: x })))),
          h('tbody', {}, [
            ['دقة التصنيف العام', cal.heldOutRules, cal.heldOutFused],
            ['دقة التصنيف الفرعي', cal.heldOutSubRules, cal.heldOutSubFused],
            ['دقة التصنيف الواثق', cal.confidentPrecisionRules, cal.confidentPrecisionFused],
            ['نسبة التصنيف الواثق', cal.confidentCoverageRules, cal.confidentCoverageFused],
          ].map(([label, a, b]) => h('tr', {}, h('td', { text: label }), h('td', { text: pct(a) }), h('td', {}, h('b', { text: pct(b) }))))))),
        h('p', { class: 'muted small', style: 'margin-top:8px', text: `وزن النموذج في الدمج ${cal.weight} — معايَر على ${nf.format(cal.tuneCount)} عينة، ومقيس على عينات لم تدخل المعايرة. ${cal.note || ''}` })) : null,
      h('div', { class: 'row', style: 'gap:8px;margin-top:12px;flex-wrap:wrap' },
        !st.loaded ? h('button', { class: 'btn primary', disabled: busy, onclick: () => act('download', 'بدأ التنزيل') }, icon('download'), 'نزّل النموذج الموصى به (~120 ميجابايت)') : null,
        st.loaded ? h('button', { class: 'btn', disabled: busy, onclick: () => act('calibrate', 'اكتملت المعايرة') }, icon('refresh'), 'أعد المعايرة والقياس') : null,
        h('button', { class: 'btn ghost', disabled: busy, onclick: () => act('reload') }, icon('search'), 'أعد البحث عن النموذج')),
      !st.loaded ? h('p', { class: 'hint' }, 'يُنزَّل إلى ', h('bdi', { class: 'ltr', text: st.downloadTarget }), ' من ', h('bdi', { class: 'ltr', text: dl.source })) : null);
    if (busy) timer = setTimeout(() => box.isConnected && load(), 1500);
  };
  const load = st => (st ? Promise.resolve(st) : api('/admin/semantic')).then(draw).catch(e => mount(box, h('p', { class: 'error', text: e.message })));
  mount(box, h('p', { class: 'muted small', text: 'جارٍ فحص النموذج الدلالي…' }));
  load();
  return box;
}

async function adminUsers(body) {
  const users = await api('/admin/users');
  const roles = state.meta?.roles || [];
  const err = h('p', { class: 'error', role: 'alert' });
  const f = { userName: h('input', { required: true, autocomplete: 'off' }), fullName: h('input', { required: true }), role: h('select', {}, roles.map(r => h('option', { value: r, text: r, selected: r === 'موظف أرشفة' }))), password: h('input', { type: 'password', autocomplete: 'new-password', required: true }) };
  const form = h('form', { class: 'form' },
    h('div', {}, h('label', { class: 'f', text: 'اسم المستخدم' }), f.userName), h('div', {}, h('label', { class: 'f', text: 'الاسم الكامل' }), f.fullName),
    h('div', {}, h('label', { class: 'f', text: 'الدور' }), f.role), h('div', {}, h('label', { class: 'f', text: 'كلمة المرور الأولى' }), f.password, h('p', { class: 'hint', text: '10 أحرف فأكثر، غير شائعة، ولا تتضمن اسم المستخدم.' })),
    h('div', { class: 'full row' }, h('button', { class: 'btn primary', type: 'submit' }, icon('plus'), 'إضافة مستخدم'), err));
  form.addEventListener('submit', async e => {
    e.preventDefault();
    try { await api('/admin/users', { method: 'POST', body: Object.fromEntries(Object.entries(f).map(([k, v]) => [k, v.value])) }); toast('أُضيف المستخدم'); adminUsers(body); }
    catch (ex) { err.textContent = ex.message; }
  });
  const update = async (u, patch, msg) => { try { await api('/admin/users/' + u.id, { method: 'PUT', body: patch }); toast(msg); adminUsers(body); } catch (ex) { toast(ex.message, { error: true }); } };
  mount(body, h('div', { class: 'grid cols-3' },
    h('section', { class: 'card' }, h('div', { class: 'table-wrap' }, h('table', {}, h('thead', {}, h('tr', {}, ['المستخدم', 'الدور', 'آخر دخول', 'الحالة', ''].map(x => h('th', { text: x })))),
      h('tbody', {}, users.map(u => h('tr', {},
        h('td', {}, h('b', { text: u.fullName }), h('div', { class: 'muted small ltr', text: u.userName })),
        h('td', {}, h('select', { 'aria-label': 'الدور', onchange: e => update(u, { role: e.target.value }, 'تغيّر الدور') }, roles.map(r => h('option', { value: r, text: r, selected: r === u.role })))),
        h('td', { class: 'small', text: u.lastLogin ? fmt.time(u.lastLogin) : '—' }),
        h('td', {}, h('span', { class: 'chip ' + (u.active ? 'ok' : 'danger'), text: u.active ? 'نشط' : 'موقوف' })),
        h('td', {}, h('div', { class: 'row' }, h('button', { class: 'btn sm', onclick: () => update(u, { active: !u.active }, u.active ? 'أُوقف الحساب' : 'فُعّل الحساب') }, u.active ? 'إيقاف' : 'تفعيل'),
          h('button', { class: 'btn sm', onclick: () => { const p = prompt('كلمة المرور الجديدة لـ ' + u.fullName + ':'); if (p) update(u, { password: p }, 'تغيّرت كلمة المرور وأُنهيت جلساته'); } }, 'كلمة المرور'))))))))),
    h('section', { class: 'card pad' }, h('h2', { style: 'margin-bottom:12px', text: 'مستخدم جديد' }), form)));
}

async function adminSettings(body) {
  const r = await api('/admin/settings');
  const s = r.settings;
  const fields = [
    ['organization', 'اسم الجهة', 'يظهر في الواجهة والتقارير'],
    ['reply_days', 'مهلة الرد (أيام)', 'بعدها يُعد الوارد «بانتظار الرد» إن لم يصدر ما يشير إليه'],
    ['commitment_days', 'المهلة الافتراضية للالتزامات (أيام)', 'موعد الوعد الذي لم يُذكر له موعد («سنفيدكم بما يستجد»)، من تاريخ الخطاب'],
    ['default_direction', 'الاتجاه الافتراضي لمجلد الإدراج', 'وارد أو صادر أو داخلي'],
    ['inbox_folder', 'مجلد الإدراج (السكانر)', r.detected.inboxExists ? 'المجلد موجود ويُراقَب كل بضع ثوانٍ' : 'اضبط السكانر ليحفظ هنا؛ تُدرج الملفات تلقائياً'],
    ['tesseract_path', 'مسار Tesseract', r.detected.tesseract ? 'مكتشف: ' + r.detected.tesseract : 'غير مكتشف — مطلوب لقراءة الصور والملفات المصورة'],
    ['pdftoppm_path', 'مسار pdftoppm (Poppler)', r.detected.pdftoppm ? 'مكتشف: ' + r.detected.pdftoppm : 'غير مكتشف — مطلوب لملفات PDF المصورة'],
    ['ocr_languages', 'لغات OCR', 'مثل ara+eng'],
    ['semantic_model_path', 'مجلد النموذج الدلالي', 'اختياري: اتركه فارغاً ليُبحث عنه في models\\semantic بجوار البرنامج'],
    ['semantic_model_url', 'مصدر تنزيل النموذج', 'اختياري: مرآة داخلية للمؤسسة بدل المصدر الافتراضي'],
  ];
  const inputs = {};
  const err = h('p', { class: 'error', role: 'alert' });
  const form = h('form', { class: 'form card pad' }, fields.map(([k, label, hint]) => h('div', { class: k === 'organization' || k === 'inbox_folder' ? 'full' : '' }, h('label', { class: 'f', text: label }), inputs[k] = h('input', { value: s[k] || '', class: k.endsWith('path') || k.endsWith('url') || k === 'inbox_folder' ? 'ltr' : '' }), h('p', { class: 'hint', text: hint }))),
    h('div', { class: 'full row' }, h('button', { class: 'btn primary', type: 'submit' }, icon('check'), 'حفظ الإعدادات'), err));
  form.addEventListener('submit', async e => {
    e.preventDefault();
    try { await api('/admin/settings', { method: 'PUT', body: Object.fromEntries(Object.entries(inputs).map(([k, v]) => [k, v.value])) }); toast('حُفظت الإعدادات'); state.org = inputs.organization.value; adminSettings(body); }
    catch (ex) { err.textContent = ex.message; }
  });
  mount(body, form, ocrPanel());
}

// ===== محرك القراءة: Tesseract، والمحرك المرئي (محلي أو سحابي)، وقياسهما على العينات المضمّنة =====
function ocrPanel() {
  const box = h('section', { class: 'card pad', style: 'margin-top:16px' });
  let timer = null;
  const load = () => api('/admin/ocr').then(draw).catch(e => mount(box, h('p', { class: 'error', text: e.message })));
  function draw(st) {
    clearTimeout(timer);
    const f = {
      mode: h('select', {}, [['auto', 'تلقائي: المحرك المرئي إن توفر، مع Tesseract للتحقق'], ['tesseract', 'Tesseract وحده']].map(([v, l]) => h('option', { value: v, text: l, selected: v === st.mode }))),
      url: h('input', { class: 'ltr', value: st.url || '', placeholder: 'http://127.0.0.1:11434/v1' }),
      model: h('input', { class: 'ltr', value: st.model || '', placeholder: 'qwen3-vl:4b', list: 'ocr-models' }),
      prompt: h('textarea', { class: 'ltr', rows: 3, placeholder: st.defaultPrompt, value: st.prompt || '' }),
      side: h('select', {}, [['1024', '1024 بكسل (أسرع)'], ['1280', '1280 بكسل (موصى به)'], ['1600', '1600 بكسل'], ['2000', '2000 بكسل (أدق وأبطأ)']].map(([v, l]) => h('option', { value: v, text: l, selected: v === String(st.maxSide) }))),
      timeout: h('input', { type: 'number', min: 30, max: 3600, value: st.timeout }),
      provider: h('select', {}, [['local', 'نموذج محلي (على هذا الجهاز أو خادم داخلي)'], ['gemini', 'Google Gemini (سحابي)'], ['claude', 'Anthropic Claude (سحابي)']].map(([v, l]) => h('option', { value: v, text: l, selected: v === st.provider }))),
      key: h('input', { type: 'password', class: 'ltr', autocomplete: 'off' }),
      cloudModel: h('input', { class: 'ltr', value: st.cloudModel || '', list: 'ocr-models' }),
      consent: h('input', { type: 'checkbox', style: 'width:auto;flex:none;margin-top:3px' }),
    };
    const cloudOf = p => st.cloud.find(c => c.provider === p);
    const localBox = h('div', { class: 'form full', style: 'padding:0' });
    const cloudBox = h('div', { class: 'form full', style: 'padding:0' });
    const consentNote = h('div', { class: 'full' });
    const keyActions = h('span');
    const withdraw = async p => {
      if (!confirm('سحب الإقرار يوقف الإرسال إلى هذه الخدمة ويعيد القراءة إلى المحرك المحلي. متابعة؟')) return;
      try { await api('/admin/ocr/cloud', { method: 'POST', body: { provider: p, consent: false } }); toast('سُحب الإقرار'); load(); } catch (e) { toast(e.message, { error: true }); }
    };
    const removeKey = async p => {
      try { await api('/admin/ocr/cloud', { method: 'POST', body: { provider: p, key: '' } }); toast('حُذف المفتاح'); load(); } catch (e) { toast(e.message, { error: true }); }
    };
    const showProvider = () => {
      const p = f.provider.value, c = cloudOf(p);
      localBox.hidden = p !== 'local'; cloudBox.hidden = p === 'local';
      if (!c) return;
      f.key.placeholder = c.hasKey ? `محفوظ (${c.keyHint}) — اكتب مفتاحاً جديداً لاستبداله` : (p === 'gemini' ? 'AIza…' : 'sk-ant-…');
      mount(keyActions, c.hasKey ? h('button', { class: 'btn sm ghost', type: 'button', onclick: () => removeKey(p) }, icon('x'), 'حذف المفتاح المحفوظ') : null);
      // اسم نموذج المزوّد الآخر لا يصلح لهذا.
      if (f.cloudModel.value && !f.cloudModel.value.startsWith(p)) f.cloudModel.value = '';
      f.cloudModel.placeholder = c.defaultModel;
      f.consent.checked = !!c.consentBy; f.consent.disabled = !!c.consentBy;
      mount(consentNote, h('label', { class: 'row', style: 'gap:8px;align-items:flex-start;flex-wrap:nowrap' }, f.consent,
        h('span', { class: 'small' }, `أقرّ بأن صور صفحات المستندات المصنفة «عادي» سترسل عبر الإنترنت إلى ${c.label} لقراءتها، وبأن ذلك متوافق مع سياسة تصنيف البيانات في جهتنا. المستندات المصنفة «مقيد» أو «سري» أو «سري للغاية» لا تُرسل أبداً وتُقرأ على هذا الجهاز.`,
          c.consentBy ? h('span', { class: 'muted' }, ` — أقرّه ${c.consentBy} في ${fmt.time(c.consentAt)} `, h('button', { class: 'btn sm ghost', type: 'button', onclick: () => withdraw(p) }, 'سحب الإقرار')) : null)));
    };
    f.provider.addEventListener('change', showProvider);
    const models = h('datalist', { id: 'ocr-models' });
    const probeOut = h('span', { class: 'small' });
    const err = h('p', { class: 'error', role: 'alert' });
    const probe = async () => {
      mount(probeOut, 'جارٍ الفحص…');
      try {
        if (f.provider.value !== 'local' && f.key.value.trim()) await saveCloud(false);
        const r = await api('/admin/ocr/probe', { method: 'POST', body: { url: f.url.value, provider: f.provider.value } });
        mount(models, r.models.map(m => h('option', { value: m })));
        mount(probeOut, r.ok ? h('span', { class: 'chip ok' }, icon('check'), `${f.provider.value === 'local' ? 'الخادم متاح' : 'المفتاح صالح'} — ${nf.format(r.models.length)} نموذج`) : h('span', { class: 'chip danger', text: r.message }));
      } catch (e) { mount(probeOut, h('span', { class: 'chip danger', text: e.message })); }
    };
    // المفتاح والإقرار يُحفظان أولاً (المفتاح مشفّراً ولا يعود إلى المتصفح)، ثم بقية الإعدادات.
    const saveCloud = async withConsent => {
      const p = f.provider.value, key = f.key.value.trim(), c = cloudOf(p);
      if (!key && !(withConsent && f.consent.checked && !c.consentBy)) return;
      const r = await api('/admin/ocr/cloud', { method: 'POST', body: { provider: p, key: key || null, consent: withConsent && f.consent.checked ? true : null } });
      Object.assign(c, r); f.key.value = '';
    };
    const save = async () => {
      err.textContent = '';
      try {
        const p = f.provider.value;
        if (p !== 'local') {
          if (!f.consent.checked) { err.textContent = 'اختيار خدمة سحابية يتطلب الإقرار أدناه.'; return; }
          await saveCloud(true);
          if (!cloudOf(p).hasKey) { err.textContent = 'أدخل مفتاح API للخدمة.'; return; }
        }
        await api('/admin/settings', { method: 'PUT', body: { ocr_engine: f.mode.value, vision_provider: p, vision_cloud_model: f.cloudModel.value.trim(), vision_ocr_url: f.url.value.trim(), vision_ocr_model: f.model.value.trim(), vision_ocr_prompt: f.prompt.value.trim(), vision_ocr_max_side: f.side.value, vision_ocr_timeout: String(f.timeout.value) } });
        toast('حُفظت إعدادات القراءة'); load();
      }
      catch (e) { err.textContent = e.message; }
    };
    const bench = async () => { try { await api('/admin/ocr/benchmark', { method: 'POST' }); toast('بدأ القياس'); load(); } catch (e) { toast(e.message, { error: true }); } };
    const b = st.benchmark;
    const pct = x => x == null ? '—' : nf.format(x) + '%';
    const levels = { clean: 'نظيف', mild: 'خفيف', moderate: 'متوسط', severe: 'شديد' };
    mount(box,
      h('h3', {}, icon('eye'), 'محرك القراءة (OCR)', state.version ? h('span', { class: 'chip', style: 'margin-inline-start:8px;font-weight:400', text: 'الإصدار ' + state.version }) : null),
      h('p', { class: 'muted small', text: 'Tesseract ضعيف في المسح الرديء. نماذج الرؤية الحديثة أدق بكثير في قراءة العربية: محلياً عبر Ollama أو llama.cpp (يحتاج جهازاً قوياً، ويفضَّل بطاقة رسومية)، أو عبر خدمة سحابية (Gemini أو Claude) بمفتاح API، وهي الأدق والأسرع دون عتاد خاص. في الوضع التلقائي يُقرأ النص بالنموذج المرئي، ويقرأ Tesseract الصفحة قراءةً ثانية: ما اتفقا عليه من رقم وتاريخ وموضوع وإشارات ومهل يُعتمد آلياً، وما اختلفا فيه يُعرض للتحقق.' }),
      h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap;margin:8px 0' },
        h('span', { class: 'chip ' + (st.tesseract ? 'ok' : 'warn'), text: st.tesseract ? 'Tesseract: مثبت' : 'Tesseract: غير مثبت' }),
        h('span', { class: 'chip ' + (st.vision ? 'ok' : st.url || st.provider !== 'local' ? 'danger' : ''), text: st.vision ? `المحرك المرئي: ${st.vision.model}${st.vision.cloud ? ' — ' + st.vision.label : ''}` : st.url || st.provider !== 'local' ? 'المحرك المرئي: غير متاح' : 'المحرك المرئي: لم يُضبط' }),
        h('span', { class: 'chip ' + (st.digitReader.available ? 'ok' : 'danger'), title: st.digitReader.problem, text: st.digitReader.available ? 'قارئ الأرقام: يعمل' : 'قارئ الأرقام: معطل' }),
        st.visionNote ? h('span', { class: 'small muted', text: st.visionNote }) : null),
      st.digitReader.problem ? h('p', { class: 'error small', text: st.digitReader.problem }) : null,
      h('div', { class: 'form' },
        h('div', {}, h('label', { class: 'f', text: 'الوضع' }), f.mode),
        h('div', {}, h('label', { class: 'f', text: 'المحرك المرئي' }), f.provider),
        mount(localBox,
          h('div', {}, h('label', { class: 'f', text: 'عنوان الخادم المحلي' }), f.url, h('p', { class: 'hint', text: 'Ollama: http://127.0.0.1:11434/v1 — llama.cpp: http://127.0.0.1:8080/v1. لا يُقبل هنا عنوان على الإنترنت.' })),
          h('div', {}, h('label', { class: 'f', text: 'النموذج' }), f.model, h('p', { class: 'hint', text: 'اسمه في الخادم، مثل qwen3-vl:4b. «فحص الخادم» يعرض المتوفر.' }))),
        mount(cloudBox,
          h('div', {}, h('label', { class: 'f', text: 'مفتاح API' }), f.key, h('p', { class: 'hint', text: 'يُحفظ مشفّراً على هذا الخادم ولا يُعرض بعد حفظه. Gemini: من aistudio.google.com — Claude: من console.anthropic.com.' }), keyActions),
          h('div', {}, h('label', { class: 'f', text: 'النموذج السحابي' }), f.cloudModel, h('p', { class: 'hint', text: 'اتركه فارغاً للموصى به. «فحص الخادم» يتحقق من المفتاح ويعرض النماذج المتاحة له.' })),
          consentNote),
        models,
        h('div', {}, h('label', { class: 'f', text: 'دقة الصورة المرسلة' }), f.side, h('p', { class: 'hint', text: 'كلما كبرت زادت الدقة والزمن. 1280 تكفي غالباً لخطاب A4.' })),
        h('div', {}, h('label', { class: 'f', text: 'مهلة الصفحة (ثانية)' }), f.timeout, h('p', { class: 'hint', text: 'بعدها تُقرأ الصفحة بـ Tesseract. تجاوزها مرتين متتاليتين يوقف المحرك المرئي 20 دقيقة.' })),
        h('details', { class: 'full' }, h('summary', { class: 'small', text: 'موجّه القراءة (متقدم)' }), f.prompt, h('p', { class: 'hint', text: 'اتركه فارغاً للموجّه الافتراضي. نماذج PaddleOCR-VL تستخدم «OCR:» تلقائياً.' }))),
      h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap;margin-top:12px;align-items:center' },
        h('button', { class: 'btn primary', type: 'button', onclick: save }, icon('check'), 'حفظ'),
        h('button', { class: 'btn', type: 'button', onclick: probe }, icon('search'), 'فحص الخادم'), probeOut,
        h('button', { class: 'btn', type: 'button', disabled: st.running || !st.tesseract && !st.vision, onclick: bench, style: 'margin-inline-start:auto' }, icon('report'), 'قياس المحركات على العينات')),
      err,
      st.running ? h('p', { class: 'small' }, icon('refresh'), ' ', st.progress || 'جارٍ القياس…', st.progressSeconds ? ` — ${nf.format(st.progressSeconds)} ثانية` : '', ' — قد يستغرق دقائق مع نموذج على المعالج') : null,
      st.error ? h('p', { class: 'error', text: st.error }) : null,
      b ? h('div', { style: 'margin-top:12px' },
        h('div', { class: 'table-wrap' }, h('table', {},
          h('thead', {}, h('tr', {}, ['المحرك', 'خطأ الحروف', ...Object.values(levels), 'حقول اعتُمدت صحيحة', 'خطأ صامت', 'للتحقق', 'لم تُقرأ', 'ثانية/صفحة'].map(x => h('th', { text: x })))),
          h('tbody', {}, b.engines.map(e => e.incomplete ? h('tr', {},
            h('td', {}, h('b', { text: 'تلقائي' }), h('div', { class: 'muted small ltr', text: e.engine })),
            h('td', { colspan: 9 }, h('span', { class: 'chip danger', text: `لم يكتمل: تعذرت القراءة المرئية في الصفحة ${nf.format(e.pagesDone + 1)}` }), h('div', { class: 'small error', style: 'margin-top:4px', text: e.note })),
            h('td', { text: nf.format(e.secondsPerPage) })) : h('tr', {},
            h('td', {}, h('b', { text: e.mode === 'auto' ? 'تلقائي' : 'Tesseract وحده' }), h('div', { class: 'muted small ltr', text: e.engine }), e.note ? h('div', { class: 'small error', text: e.note }) : null),
            h('td', {}, h('b', { text: pct(e.cer) })),
            ...Object.keys(levels).map(k => h('td', { text: pct(e.cerByLevel[k]) })),
            h('td', { text: `${nf.format(e.autoCorrect)} من ${nf.format(e.fields)}` }),
            h('td', {}, h('span', { class: 'chip ' + (e.silentWrong ? 'danger' : 'ok'), text: nf.format(e.silentWrong) })),
            h('td', { text: nf.format(e.review) }), h('td', { text: nf.format(e.missing) }), h('td', { text: nf.format(e.secondsPerPage) })))))),
        h('p', { class: 'muted small', style: 'margin-top:6px', text: `قيس ${fmt.time(b.at)} على ${nf.format(b.pages)} صفحات اصطناعية بعيوب مسح متدرجة. «خطأ صامت»: حقل اعتُمد آلياً وهو خطأ، وهو الأخطر. العينات مؤشر للمقارنة على هذا الجهاز؛ الحكم النهائي على مراسلاتكم.` })) : null,
      h('details', { style: 'margin-top:12px' }, h('summary', { class: 'small', text: 'كيف أستخدم خدمة سحابية؟' }),
        h('ol', { class: 'small' },
          h('li', {}, 'Gemini (الأدق في قياسات العربية المنشورة، وكلفته منخفضة): أنشئ مفتاحاً من aistudio.google.com، واختر «Google Gemini» والصق المفتاح وأقرّ ثم «حفظ».'),
          h('li', {}, 'Claude: أنشئ مفتاحاً من console.anthropic.com واختر «Anthropic Claude».'),
          h('li', {}, 'لا تُرسل المستندات المصنفة «مقيد» أو «سري» أو «سري للغاية» أبداً؛ صنّف المستند قبل رفعه إن كان كذلك. كل إرسال يُسجل في سجل التدقيق («قراءة سحابية»).'),
          h('li', {}, 'تأكد أن سياسة جهتك تسمح بإرسال المراسلات العادية إلى خدمة خارجية، ثم «قياس المحركات على العينات» للمقارنة بـ Tesseract.'))),
      h('details', { style: 'margin-top:12px' }, h('summary', { class: 'small', text: 'كيف أشغّل نموذجاً محلياً؟' }),
        h('ol', { class: 'small' },
          h('li', {}, 'Ollama (الأسهل): ثبّته من ollama.com، ثم في سطر الأوامر: ', h('code', { class: 'ltr', text: 'ollama pull qwen3-vl:4b' }), '، والعنوان http://127.0.0.1:11434/v1 والنموذج qwen3-vl:4b.'),
          h('li', {}, 'llama.cpp مع PaddleOCR-VL (نموذج قراءة متخصص صغير يدعم العربية): ', h('code', { class: 'ltr', text: 'llama-server -m PaddleOCR-VL-1.5.gguf --mmproj PaddleOCR-VL-1.5-mmproj.gguf --temp 0' }), '، والعنوان http://127.0.0.1:8080/v1.'),
          h('li', {}, 'ثم «فحص الخادم» و«حفظ»، ثم «قياس المحركات على العينات» لمقارنته بـ Tesseract على هذا الجهاز. جرّب أكثر من نموذج واعتمد الأدق.'),
          h('li', {}, 'بطاقة رسومية تسرّع القراءة كثيراً؛ على المعالج وحده قد تستغرق الصفحة دقيقة أو أكثر.'))));
    showProvider();
    if (st.running) timer = setTimeout(() => box.isConnected && load(), 2000);
  }
  mount(box, h('p', { class: 'muted small', text: 'جارٍ فحص محركات القراءة…' }));
  load();
  return box;
}

function adminMigrate(body) {
  const folder = h('input', { class: 'ltr', placeholder: 'C:\\Users\\...\\AppData\\Roaming\\SaeedAIDocArchive' });
  const out = h('div', { style: 'margin-top:12px' });
  const run = h('button', { class: 'btn primary', onclick: async () => {
    run.disabled = true; mount(out, h('div', { class: 'skeleton', style: 'min-height:40px' }));
    try {
      const r = await api('/admin/import-legacy', { method: 'POST', body: { folder: folder.value.trim() } });
      mount(out, h('div', { class: 'card pad' }, h('h3', {}, icon('check'), ' اكتمل الترحيل'), h('p', { text: `مستندات: ${nf.format(r.documents)} (تخطي ${nf.format(r.skipped)} موجودة مسبقاً) • ملفات: ${nf.format(r.files)}${r.missingFiles ? ` (${nf.format(r.missingFiles)} مفقودة)` : ''} • مستخدمون: ${nf.format(r.users)}` })));
    } catch (e) { mount(out, h('p', { class: 'error', text: e.message })); }
    run.disabled = false;
  } }, icon('folder'), 'ترحيل الآن');
  mount(body, h('section', { class: 'card pad', style: 'max-width:760px' },
    h('h2', { text: 'ترحيل أرشيف النسخة السابقة' }),
    h('p', { class: 'muted', style: 'margin:6px 0 14px', text: 'ينسخ المستندات ونصوصها وملفاتها الأصلية والمستخدمين بكلمات مرورهم من مجلد بيانات التطبيق المكتبي (الذي يحوي archive-db.xml). لا يعدّل المصدر، ويمكن تكراره بأمان؛ ما رُحّل سابقاً يُتخطى.' }),
    h('label', { class: 'f', text: 'مجلد بيانات النسخة السابقة' }), folder, h('div', { class: 'row', style: 'margin-top:12px' }, run), out));
}

async function adminAudit(body) {
  const r = await api('/audit?take=200');
  mount(body, h('section', { class: 'card pad', style: 'margin-bottom:14px' }, h('div', { class: 'row' }, h('span', { class: 'chip ' + (r.valid ? 'ok' : 'danger') }, icon(r.valid ? 'shield' : 'alert'), r.valid ? 'السلسلة سليمة' : 'السلسلة مكسورة'), h('span', { text: `${r.message} (${nf.format(r.count)} سجل)` })),
    h('p', { class: 'muted small ltr', style: 'margin-top:8px;word-break:break-all;text-align:right', text: 'آخر تجزئة: ' + r.lastHash })),
    h('section', { class: 'card' }, h('div', { class: 'table-wrap' }, h('table', {}, h('thead', {}, h('tr', {}, ['#', 'الوقت', 'المستخدم', 'الإجراء', 'التفاصيل'].map(x => h('th', { text: x })))),
      h('tbody', {}, r.entries.map(e => h('tr', {}, h('td', { class: 'num muted', text: e.seq }), h('td', { class: 'small', text: fmt.time(e.ts) }), h('td', { text: e.actor }), h('td', { text: e.action }), h('td', { class: 'muted', text: e.detail }))))))));
}

function adminReport(body) {
  mount(body, h('section', { class: 'card pad', style: 'max-width:760px' }, h('h2', { text: 'تقرير صحة الأرشيف' }),
    h('p', { class: 'muted', style: 'margin:6px 0 14px', text: 'تقرير HTML مستقل يُطبع ويُشارك: مؤشر الصحة، واكتمال النص والتصنيف، والمواعيد المتأخرة، والتوزيعات، والتنبيهات. يتضمن عناوين المستندات دون محتواها.' }),
    h('a', { class: 'btn primary', href: '/api/report' }, icon('download'), 'تنزيل التقرير')));
}

// ======================= الدخول والإعداد =======================
const heroList = [['thread', 'معاملات تتكوّن تلقائياً', 'كل خطاب يشير إلى آخر يقع معه في خيط واحد، مرتب زمنياً، ومعه ما غاب عن الأرشيف.'],
  ['alert', 'لا وارد بلا رد', 'تنبيه بكل وارد لم يصدر ما يشير إليه خلال المهلة.'], ['clock', 'مهل تُقرأ من النص', '«خلال (15) يوماً من تاريخه» تصبح موعداً محسوباً بالهجري والميلادي.'],
  ['shield', 'سجل لا يُزوَّر', 'كل دخول وتعديل واطلاع في سلسلة تجزئة تكشف أي تلاعب.']];

export function renderAuth(root, mode, onDone) {
  const err = h('p', { class: 'error', role: 'alert' });
  const fields = mode === 'setup'
    ? { organization: h('input', { placeholder: 'مثال: الإدارة العامة للشؤون الإدارية' }), fullName: h('input', { required: true, autocomplete: 'name' }), userName: h('input', { required: true, value: 'admin', autocomplete: 'username', class: 'ltr' }), password: h('input', { type: 'password', required: true, autocomplete: 'new-password' }), confirm: h('input', { type: 'password', required: true, autocomplete: 'new-password' }) }
    : { userName: h('input', { required: true, autocomplete: 'username', autofocus: true, class: 'ltr' }), password: h('input', { type: 'password', required: true, autocomplete: 'current-password' }) };
  const labels = { organization: 'اسم الجهة', fullName: 'اسمك الكامل', userName: 'اسم المستخدم', password: 'كلمة المرور', confirm: 'تأكيد كلمة المرور' };
  const submit = h('button', { class: 'btn primary', type: 'submit', style: 'padding:11px' }, mode === 'setup' ? 'إنشاء حساب المدير والبدء' : 'دخول');
  const form = h('form', {}, h('div', {}, h('h2', { text: mode === 'setup' ? 'إعداد المنصة' : 'تسجيل الدخول' }), h('p', { class: 'muted', text: mode === 'setup' ? 'أول تشغيل: أنشئ حساب مدير النظام. لا توجد كلمات مرور افتراضية.' : 'مرحباً بعودتك.' })),
    Object.entries(fields).map(([k, el]) => h('div', {}, h('label', { class: 'f', text: labels[k] }), el, k === 'password' && mode === 'setup' ? h('p', { class: 'hint', text: '10 أحرف فأكثر، غير شائعة، ولا تتضمن اسم المستخدم.' }) : null)), err, submit);
  form.addEventListener('submit', async e => {
    e.preventDefault(); err.textContent = '';
    if (mode === 'setup' && fields.password.value !== fields.confirm.value) { err.textContent = 'تأكيد كلمة المرور غير مطابق.'; return; }
    submit.disabled = true;
    try {
      const body = Object.fromEntries(Object.entries(fields).filter(([k]) => k !== 'confirm').map(([k, v]) => [k, v.value]));
      await api(mode === 'setup' ? '/setup' : '/login', { method: 'POST', body });
      onDone();
    } catch (ex) { err.textContent = ex.message; submit.disabled = false; }
  });
  mount(root, h('div', { class: 'auth' },
    h('div', { class: 'hero' }, h('div', {}, h('div', { class: 'brand', style: 'padding:0' }, h('div', { class: 'logo', style: 'background:rgba(255,255,255,.12)' }, h('img', { src: '/favicon.svg', alt: '', width: 26, height: 26 })), h('b', { style: 'font-size:24px;color:#fff', text: 'بصيرة' })),
      h('h1', { style: 'margin-top:48px', text: 'أرشيف يفهم المعاملة، لا الملف فقط.' }), h('p', { text: 'منصة أرشفة عربية تقرأ خطاباتك وتربطها ببعضها وتنبهك بما ينتظر الرد.' }),
      h('ul', {}, heroList.map(([i, t, d]) => h('li', {}, icon(i), h('div', {}, h('b', { text: t }), h('div', { style: 'opacity:.8;font-size:14px', text: d })))))),
      h('p', { style: 'opacity:.6;font-size:13px', text: 'تعمل على جهازك أو خادم جهتك. لا يُرسل أي مستند إلى خدمة خارجية إلا بإقرار مدير النظام، ولا يُرسل المصنف «سري» أبداً.' })),
    h('div', { class: 'panel' }, form)));
  (fields.userName.value ? (fields.password || fields.fullName) : fields.userName).focus();
}

// ======================= لوحة الأوامر =======================
const COMMANDS = [['لوحة القيادة', '#/', 'home'], ['المعاملات', '#/threads', 'thread'], ['المعاملات بانتظار رد', '#/threads?state=بانتظار رد', 'alert'], ['المستندات', '#/docs', 'docs'],
  ['مستندات بانتظار الرد', '#/docs?filter=awaiting', 'alert'], ['مواعيد متأخرة', '#/docs?filter=overdue', 'clock'], ['إدراج ملفات', '#/inbox', 'upload'], ['اسأل بصيرة', '#/ask', 'spark'],
  ['خطابات غير مؤرشفة', '#/threads?missing=1', 'ghost'], ['الإدارة', '#/admin', 'sliders'], ['سجل التدقيق', '#/admin/audit', 'shield'], ['تقرير صحة الأرشيف', '#/admin/report', 'report']];

export function openPalette() {
  if (document.querySelector('.overlay')) return;
  const input = h('input', { placeholder: 'ابحث عن مستند أو اكتب أمراً…', 'aria-label': 'بحث سريع', autocomplete: 'off' });
  const results = h('div', { class: 'results', role: 'listbox' });
  let items = [], sel = 0, ctl;
  const close = () => { overlay.remove(); document.removeEventListener('keydown', keys, true); };
  const go = it => { close(); location.hash = it.href; };
  const draw = (cmds, docs, q) => {
    items = [...cmds, ...docs];
    sel = Math.min(sel, Math.max(0, items.length - 1));
    mount(results, cmds.length ? h('div', { class: 'group', text: 'انتقال' }) : null, cmds.map((c, i) => item(c, i)),
      docs.length ? h('div', { class: 'group', text: 'مستندات' }) : null, docs.map((d, i) => item(d, cmds.length + i, q)),
      !items.length ? h('p', { class: 'empty', text: 'لا نتائج' }) : null);
  };
  const item = (it, i, q) => h('div', { class: 'item', role: 'option', 'aria-selected': String(i === sel), onmousemove: () => { if (sel !== i) { sel = i; results.querySelectorAll('.item').forEach((n, j) => n.setAttribute('aria-selected', String(j === sel))); } }, onclick: () => go(it) },
    icon(it.icon), h('div', { class: 'grow' }, h('b', {}, q ? markTerms(it.title, it.matches) : it.title), it.sub ? h('span', { class: 'muted small', text: it.sub }) : null));
  async function search() {
    const q = input.value.trim();
    const cmds = COMMANDS.filter(([t]) => !q || t.includes(q)).slice(0, q ? 4 : 12).map(([title, href, ic]) => ({ title, href, icon: ic }));
    draw(cmds, [], q);
    if (q.length < 2) return;
    ctl?.abort(); ctl = new AbortController();
    try {
      const r = await api('/documents?' + new URLSearchParams({ q, pageSize: 8 }), { signal: ctl.signal });
      draw(cmds, r.items.map(d => ({ title: d.title, matches: d.matches, href: '#/doc/' + encodeURIComponent(d.id), icon: 'docs', sub: [d.archiveNo, d.direction, d.sourceEntity].filter(Boolean).join(' • ') })), q);
    } catch { /* أُلغي */ }
  }
  const keys = e => {
    if (e.key === 'Escape') { e.preventDefault(); close(); }
    else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); sel = (sel + (e.key === 'ArrowDown' ? 1 : -1) + items.length) % Math.max(1, items.length); results.querySelectorAll('.item').forEach((n, j) => { n.setAttribute('aria-selected', String(j === sel)); if (j === sel) n.scrollIntoView({ block: 'nearest' }); }); }
    else if (e.key === 'Enter' && items[sel]) { e.preventDefault(); go(items[sel]); }
  };
  let t; input.addEventListener('input', () => { sel = 0; clearTimeout(t); t = setTimeout(search, 150); });
  const overlay = h('div', { class: 'overlay', onclick: e => { if (e.target === overlay) close(); } }, h('div', { class: 'palette', role: 'dialog', 'aria-label': 'بحث سريع' }, h('div', { class: 'in' }, icon('search'), input, h('kbd', { text: 'Esc' })), results));
  document.body.append(overlay);
  document.addEventListener('keydown', keys, true);
  input.focus();
  search();
}
