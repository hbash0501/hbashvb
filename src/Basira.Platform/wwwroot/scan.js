// المسح الضوئي: اختيار الماسح وضبط خصائصه، والمعاينة وتحديد المنطقة، ومراجعة الصفحات (تدوير وحذف وترتيب)، ثم الإدراج.
import { h, icon, api, mount, toast, state, nf, confirmDialog } from './core.js';
import { pageHead, empty } from './views.js';

const SOURCES = { flatbed: 'الزجاج', feeder: 'وحدة التغذية', duplex: 'الوجهان' };
const COLORS = { color: 'ملون', gray: 'رمادي', bw: 'أبيض وأسود' };
const SIZES = [['A4', 'A4'], ['A5', 'A5'], ['A3', 'A3'], ['B5', 'B5'], ['Letter', 'Letter'], ['Legal', 'Legal'], ['max', 'كامل مساحة الماسح'], ['custom', 'المنطقة المحددة في المعاينة']];
const DEFAULTS = { source: 'flatbed', colorMode: 'gray', dpi: 300, pageSize: 'A4', brightness: 0, contrast: 0, maxPages: 0 };

const remember = (id, st) => { try { localStorage.setItem('basira-scan:' + id, JSON.stringify(st)); localStorage.setItem('basira-scan-device', id); } catch { /* تخزين غير متاح */ } };
const recall = id => { try { return { ...DEFAULTS, ...JSON.parse(localStorage.getItem('basira-scan:' + id) || '{}') }; } catch { return { ...DEFAULTS }; } };
const lastDevice = () => { try { return localStorage.getItem('basira-scan-device'); } catch { return null; } };

const seg = (options, value, onPick) => h('div', { class: 'seg' }, Object.entries(options).map(([k, l]) =>
  h('button', { type: 'button', 'aria-pressed': String(k === value), onclick: () => onPick(k) }, l)));

export async function renderScan(view) {
  const meta = state.meta || {};
  let info = await api('/scan/devices');
  let deviceId = info.devices.some(d => d.id === lastDevice()) ? lastDevice() : info.devices[0]?.id;
  let caps = null, st = recall(deviceId), region = null, job = null, poll = null;
  const settingsBox = h('section', { class: 'card pad' });
  const deviceBox = h('section', { class: 'card pad' });
  const previewBox = h('section', { class: 'card pad', hidden: true });
  const pagesBox = h('section', { class: 'card pad scan-pages' });
  const importBox = h('section', { class: 'card pad', hidden: true });

  // ===== الأجهزة =====
  function drawDevices() {
    const addr = h('input', { class: 'ltr', placeholder: '192.168.1.50 أو http://printer.local/eSCL', 'aria-label': 'عنوان الماسح الشبكي' });
    addr.style.flex = '1';
    const addForm = h('div', { class: 'row', hidden: true, style: 'gap:8px;margin-top:10px' }, addr,
      h('button', { class: 'btn primary', type: 'button', onclick: async () => {
        try { const d = await api('/scan/devices', { method: 'POST', body: { address: addr.value } }); toast(`أُضيف: ${d.name}`); info = await api('/scan/devices'); deviceId = d.id; st = recall(deviceId); drawDevices(); loadCaps(); }
        catch (e) { toast(e.message, { error: true }); }
      } }, icon('plus'), 'إضافة'));
    const current = info.devices.find(d => d.id === deviceId);
    mount(deviceBox,
      h('h3', {}, icon('scanner'), 'الماسح'),
      info.devices.length
        ? h('div', { class: 'row', style: 'gap:8px' },
          h('select', { 'aria-label': 'الماسح', style: 'flex:1;min-width:0', onchange: e => { deviceId = e.target.value; st = recall(deviceId); region = null; drawDevices(); loadCaps(); } },
            info.devices.map(d => h('option', { value: d.id, selected: d.id === deviceId, text: `${d.name} — ${d.driver === 'wia' ? 'Windows' : 'شبكي'}` }))),
          current?.driver === 'escl' && info.canManage ? h('button', { class: 'btn ghost icon-btn', type: 'button', title: 'إزالة الماسح الشبكي', 'aria-label': 'إزالة الماسح الشبكي', onclick: async () => {
            if (await confirmDialog({ title: 'إزالة الماسح', message: `إزالة «${current.name}» من قائمة الماسحات؟`, confirm: 'إزالة', danger: true }) === null) return;
            await api('/scan/devices/remove', { method: 'POST', body: { address: current.id } }); info = await api('/scan/devices'); deviceId = info.devices[0]?.id; drawDevices(); loadCaps();
          } }, icon('trash')) : null)
        : h('p', { class: 'muted small', text: info.windows
          ? 'لا ماسحات. وصّل الماسح بهذا الجهاز وثبّت تعريفه من الشركة المصنعة (يظهر في «الماسحات والطابعات» في Windows)، ثم حدّث الصفحة. أو أضف ماسحاً شبكياً بعنوانه.'
          : 'لا ماسحات. أضف ماسحاً شبكياً يدعم eSCL (AirScan) بعنوانه على الشبكة.' }),
      info.note ? h('p', { class: 'small error', text: info.note }) : null,
      info.canManage ? h('button', { class: 'btn sm ghost', type: 'button', style: 'margin-top:8px', onclick: () => { addForm.hidden = !addForm.hidden; addr.focus(); } }, icon('plus'), 'إضافة ماسح شبكي (eSCL / AirScan)') : null,
      addForm,
      h('p', { class: 'muted small', style: 'margin-top:8px', text: 'يمسح الجهاز الذي تعمل عليه بصيرة. الماسحات المتصلة بأجهزة المستخدمين الأخرى تُدرج عبر «مجلد الإدراج».' }));
  }

  async function loadCaps() {
    caps = null;
    if (!deviceId) { mount(settingsBox, empty('scanner', 'اختر ماسحاً أو أضف واحداً.')); return; }
    mount(settingsBox, h('p', { class: 'muted small' }, icon('refresh'), ' جارٍ قراءة قدرات الماسح…'));
    try { caps = await api('/scan/devices/caps?id=' + encodeURIComponent(deviceId)); }
    catch (e) { mount(settingsBox, h('p', { class: 'error', text: e.message }), h('button', { class: 'btn sm', type: 'button', onclick: loadCaps }, icon('refresh'), 'إعادة المحاولة')); return; }
    if (!caps.sources.includes(st.source)) st.source = caps.sources[0];
    if (!caps.resolutions.includes(+st.dpi)) st.dpi = caps.resolutions.includes(300) ? 300 : caps.resolutions[caps.resolutions.length - 1];
    drawSettings();
  }

  // ===== الإعدادات =====
  function drawSettings() {
    const set = (k, v) => { st[k] = v; remember(deviceId, st); drawSettings(); };
    const slider = (k, label, deviceSide) => {
      const out = h('output', { class: 'num', text: (st[k] > 0 ? '+' : '') + st[k] });
      return h('div', {}, h('label', { class: 'f' }, label, ' ', out),
        h('input', { type: 'range', min: -100, max: 100, step: 5, value: st[k], 'aria-label': label, oninput: e => { out.textContent = (+e.target.value > 0 ? '+' : '') + e.target.value; }, onchange: e => set(k, +e.target.value) }),
        h('p', { class: 'hint', text: deviceSide ? 'يضبطه الماسح نفسه.' : 'لا يدعمه الماسح؛ يُطبَّق على الصورة بعد المسح.' }));
    };
    const feeder = st.source !== 'flatbed';
    const scanBtn = h('button', { class: 'btn primary', type: 'button', disabled: job?.state === 'scanning', onclick: () => start(false) }, icon('scanner'), job?.pages?.length ? 'مسح دفعة جديدة' : 'مسح');
    mount(settingsBox,
      h('div', { class: 'row', style: 'justify-content:space-between;align-items:baseline' }, h('h3', {}, icon('sliders'), 'خصائص المسح'), h('span', { class: 'muted small', text: caps.model })),
      h('div', { class: 'form', style: 'margin-top:8px' },
        h('div', { class: 'full' }, h('label', { class: 'f', text: 'مصدر الورق' }), seg(Object.fromEntries(caps.sources.map(s => [s, SOURCES[s]])), st.source, v => set('source', v))),
        h('div', { class: 'full' }, h('label', { class: 'f', text: 'اللون' }), seg(Object.fromEntries(caps.colorModes.map(c => [c, COLORS[c]])), st.colorMode, v => set('colorMode', v)),
          h('p', { class: 'hint', text: st.colorMode === 'bw' ? 'أصغر حجماً وأوضح للنص؛ العتبة تُحسب تلقائياً لكل صفحة.' : st.colorMode === 'color' ? 'للأختام الملونة والتواقيع والصور؛ أكبر حجماً.' : 'الأنسب لأغلب الخطابات وقراءة النص.' })),
        h('div', {}, h('label', { class: 'f', text: 'الدقة' }), h('select', { onchange: e => set('dpi', +e.target.value) }, caps.resolutions.map(r => h('option', { value: r, selected: r === +st.dpi, text: `${r} نقطة/بوصة${r === 300 ? ' (موصى بها للقراءة)' : ''}` })))),
        h('div', {}, h('label', { class: 'f', text: 'المقاس' }), h('select', { onchange: e => set('pageSize', e.target.value) }, SIZES.map(([v, l]) => h('option', { value: v, selected: v === st.pageSize, disabled: v === 'custom' && !region, text: l })))),
        slider('brightness', 'السطوع', caps.deviceBrightness),
        slider('contrast', 'التباين', caps.deviceContrast),
        feeder ? h('div', {}, h('label', { class: 'f', text: 'عدد الأوراق' }), h('input', { type: 'number', min: 0, max: 300, value: st.maxPages, onchange: e => set('maxPages', Math.max(0, +e.target.value || 0)) }), h('p', { class: 'hint', text: '0 = كل ما في وحدة التغذية.' })) : null),
      h('div', { class: 'row', style: 'gap:8px;flex-wrap:wrap;margin-top:14px' },
        scanBtn,
        caps.sources.includes('flatbed') && st.source === 'flatbed' ? h('button', { class: 'btn', type: 'button', onclick: doPreview }, icon('eye'), 'معاينة وتحديد منطقة') : null,
        caps.nativeDialog ? h('button', { class: 'btn ghost', type: 'button', title: 'نافذة إعدادات الشركة المصنعة لكل خيارات الماسح الخاصة (تظهر على شاشة هذا الجهاز)', onclick: native }, icon('sliders'), 'نافذة الماسح الأصلية') : null));
  }

  function settingsBody() {
    const body = { ...st, dpi: +st.dpi, maxPages: +st.maxPages || 0 };
    if (st.pageSize === 'custom' && region) Object.assign(body, { leftMm: region.left, topMm: region.top, widthMm: region.width, heightMm: region.height });
    return body;
  }

  // ===== المعاينة وتحديد المنطقة =====
  async function doPreview() {
    previewBox.hidden = false;
    previewBox.scrollIntoView({ behavior: 'smooth', block: 'start' });
    mount(previewBox, h('h3', {}, icon('eye'), 'المعاينة'), h('div', { class: 'skeleton', style: 'min-height:260px' }), h('p', { class: 'muted small', text: 'جارٍ المسح السريع للزجاج…' }));
    try {
      const p = await api('/scan/preview', { method: 'POST', body: { deviceId } });
      const img = h('img', { src: p.image, alt: 'معاينة الزجاج', draggable: false });
      const sel = h('div', { class: 'sel', hidden: !region });
      const label = h('span', { class: 'muted small' });
      const frame = h('div', { class: 'preview-frame' }, img, sel);
      const showSel = () => {
        if (!region) { sel.hidden = true; label.textContent = 'اسحب على الصورة لتحديد منطقة المسح.'; return; }
        sel.hidden = false;
        Object.assign(sel.style, { left: region.left / p.widthMm * 100 + '%', top: region.top / p.heightMm * 100 + '%', width: region.width / p.widthMm * 100 + '%', height: region.height / p.heightMm * 100 + '%' });
        label.textContent = `المنطقة: ${nf.format(Math.round(region.width))} × ${nf.format(Math.round(region.height))} مم`;
      };
      let from = null;
      const at = e => { const r = img.getBoundingClientRect(); return { x: Math.min(Math.max(e.clientX - r.left, 0), r.width) / r.width * p.widthMm, y: Math.min(Math.max(e.clientY - r.top, 0), r.height) / r.height * p.heightMm }; };
      frame.addEventListener('pointerdown', e => { from = at(e); frame.setPointerCapture(e.pointerId); });
      frame.addEventListener('pointermove', e => {
        if (!from) return;
        const to = at(e);
        region = { left: Math.min(from.x, to.x), top: Math.min(from.y, to.y), width: Math.abs(to.x - from.x), height: Math.abs(to.y - from.y) };
        showSel();
      });
      frame.addEventListener('pointerup', () => {
        from = null;
        if (region && (region.width < 15 || region.height < 15)) region = null;
        else if (region) { st.pageSize = 'custom'; remember(deviceId, st); }
        showSel(); drawSettings();
      });
      mount(previewBox, h('div', { class: 'row', style: 'justify-content:space-between' }, h('h3', {}, icon('eye'), 'المعاينة'),
        h('button', { class: 'btn sm ghost', type: 'button', onclick: () => { region = null; if (st.pageSize === 'custom') st.pageSize = 'A4'; showSel(); drawSettings(); } }, icon('x'), 'إلغاء التحديد')), frame, label);
      showSel();
    } catch (e) { mount(previewBox, h('h3', {}, icon('eye'), 'المعاينة'), h('p', { class: 'error', text: e.message })); }
  }

  // ===== المسح =====
  async function start(append) {
    try {
      job = await api('/scan/jobs', { method: 'POST', body: { deviceId, settings: settingsBody(), appendTo: append && job ? job.id : null } });
      remember(deviceId, st);
      drawSettings(); drawPages(); watch();
    } catch (e) { toast(e.message, { error: true }); }
  }
  async function native() {
    try { job = await api('/scan/native', { method: 'POST', body: { deviceId, appendTo: job?.id || null } }); drawPages(); }
    catch (e) { toast(e.message, { error: true }); }
  }
  function watch() {
    clearTimeout(poll);
    if (job?.state !== 'scanning') return;
    poll = setTimeout(async () => {
      if (!document.body.contains(pagesBox)) return;
      try { job = await api('/scan/jobs/' + job.id); } catch (e) { toast(e.message, { error: true }); return; }
      drawPages(); drawSettings();
      if (job.state !== 'scanning') { if (job.state === 'failed') toast(job.message, { error: true }); } else watch();
    }, 700);
  }

  // ===== مراجعة الصفحات =====
  let arrangeTimer;
  const pushOrder = () => {
    clearTimeout(arrangeTimer);
    arrangeTimer = setTimeout(async () => {
      try { job = await api(`/scan/jobs/${job.id}/arrange`, { method: 'POST', body: { pages: job.pages.map(p => ({ id: p.id, rotation: p.rotation })) } }); }
      catch (e) { toast(e.message, { error: true }); }
    }, 250);
  };
  function drawPages() {
    if (!job) {
      importBox.hidden = true;
      mount(importBox);
      mount(pagesBox, h('h3', {}, icon('docs'), 'الصفحات'), empty('scanner', 'اضبط الخصائص ثم اضغط «مسح». تظهر الصفحات هنا لمراجعتها قبل الإدراج.'));
      return;
    }
    const scanning = job.state === 'scanning';
    let dragFrom = null;
    const move = (i, j) => { if (j < 0 || j >= job.pages.length) return; const [p] = job.pages.splice(i, 1); job.pages.splice(j, 0, p); drawPages(); pushOrder(); };
    const tiles = job.pages.map((p, i) => {
      const tile = h('figure', { class: 'page-tile' + (p.blank ? ' blank' : ''), draggable: !scanning },
        h('div', { class: 'thumb' }, h('img', { src: `/api/scan/jobs/${job.id}/pages/${p.id}`, alt: `الصفحة ${i + 1}`, style: `transform:rotate(${p.rotation}deg)`, loading: 'lazy' })),
        h('figcaption', {}, h('b', { class: 'num', text: nf.format(i + 1) }), p.blank ? h('span', { class: 'chip warn', text: 'فارغة' }) : null, h('span', { class: 'muted small', text: `${nf.format(p.kb)} ك.ب` })),
        scanning ? null : h('div', { class: 'tile-actions' },
          h('button', { type: 'button', class: 'btn ghost icon-btn', title: 'تدوير يساراً', 'aria-label': 'تدوير يساراً', onclick: () => { p.rotation = (p.rotation + 270) % 360; drawPages(); pushOrder(); } }, icon('restore')),
          h('button', { type: 'button', class: 'btn ghost icon-btn', title: 'تدوير يميناً', 'aria-label': 'تدوير يميناً', onclick: () => { p.rotation = (p.rotation + 90) % 360; drawPages(); pushOrder(); } }, icon('refresh')),
          h('button', { type: 'button', class: 'btn ghost icon-btn', title: 'تقديم', 'aria-label': 'تقديم', disabled: i === 0, onclick: () => move(i, i - 1) }, '›'),
          h('button', { type: 'button', class: 'btn ghost icon-btn', title: 'تأخير', 'aria-label': 'تأخير', disabled: i === job.pages.length - 1, onclick: () => move(i, i + 1) }, '‹'),
          h('button', { type: 'button', class: 'btn ghost icon-btn danger', title: 'حذف الصفحة', 'aria-label': 'حذف الصفحة', onclick: () => { job.pages.splice(i, 1); drawPages(); pushOrder(); } }, icon('trash'))));
      tile.addEventListener('dragstart', () => { dragFrom = i; tile.classList.add('dragging'); });
      tile.addEventListener('dragend', () => tile.classList.remove('dragging'));
      tile.addEventListener('dragover', e => e.preventDefault());
      tile.addEventListener('drop', e => { e.preventDefault(); if (dragFrom !== null && dragFrom !== i) move(dragFrom, i); dragFrom = null; });
      return tile;
    });
    mount(pagesBox,
      h('div', { class: 'row', style: 'justify-content:space-between;flex-wrap:wrap;gap:8px' },
        h('h3', {}, icon('docs'), `الصفحات (${nf.format(job.pages.length)})`),
        h('div', { class: 'row', style: 'gap:6px' },
          !scanning && caps ? h('button', { class: 'btn sm', type: 'button', onclick: () => start(true) }, icon('plus'), 'مسح المزيد إلى هذه المهمة') : null,
          !scanning ? h('button', { class: 'btn sm ghost', type: 'button', onclick: async () => { await api(`/scan/jobs/${job.id}/cancel`, { method: 'POST' }).catch(() => {}); job = null; drawPages(); drawSettings(); } }, icon('x'), 'تجاهل المهمة') : null)),
      scanning ? h('p', { class: 'scan-progress' }, h('span', { class: 'spinner' }), job.scanned ? `جارٍ المسح… استُلمت ${nf.format(job.scanned)} صفحة` : 'جارٍ المسح…') : job.message ? h('p', { class: 'muted small', text: job.message }) : null,
      tiles.length ? h('div', { class: 'page-grid' }, tiles) : scanning ? null : empty('scanner', 'لا صفحات.'),
      tiles.length > 1 && !scanning ? h('p', { class: 'muted small', text: 'اسحب الصفحات لترتيبها، أو استخدم الأسهم.' }) : null);
    importBox.hidden = scanning || !job.pages.length;
    if (!importBox.hidden && !importBox.firstChild) drawImport();
  }

  // ===== الإدراج =====
  function drawImport() {
    let direction = meta.directions?.[0] || 'وارد', split = 'one';
    const dirSeg = h('div');
    const drawDir = () => mount(dirSeg, seg(Object.fromEntries((meta.directions || ['وارد', 'صادر', 'داخلي']).map(d => [d, d])), direction, v => { direction = v; drawDir(); }));
    drawDir();
    const conf = h('select', {}, (meta.confidentialities || ['عادي']).map(c => h('option', { value: c, text: c })));
    const category = h('input', { list: 'scan-cats', placeholder: 'تلقائي من المحتوى' });
    const title = h('input', { placeholder: 'تلقائي من المحتوى' });
    const skipBlank = h('input', { type: 'checkbox', checked: true, style: 'width:auto' });
    const splitBox = h('div');
    const titleRow = h('div', { class: 'full' }, h('label', { class: 'f', text: 'العنوان' }), title);
    const drawSplit = () => {
      titleRow.hidden = split !== 'one';
      mount(splitBox, [['one', 'مستند واحد', 'كل الصفحات خطاب واحد.'], ['blank', 'فصل عند الورقة الفارغة', 'ضع ورقة بيضاء بين الخطابات وامسحها دفعة واحدة؛ كل مجموعة مستند.'], ['each', 'مستند لكل صفحة', 'لكل صفحة رقم أرشيف مستقل.']].map(([k, l, hint]) =>
        h('label', { class: 'choice' + (k === split ? ' on' : '') }, h('input', { type: 'radio', name: 'split', checked: k === split, onchange: () => { split = k; drawSplit(); } }), h('span', {}, h('b', { text: l }), h('small', { class: 'muted', text: hint })))));
    };
    drawSplit();
    const out = h('div');
    const go = h('button', { class: 'btn primary', type: 'button', onclick: async () => {
      go.disabled = true;
      try {
        const r = await api(`/scan/jobs/${job.id}/import`, { method: 'POST', body: { direction, confidentiality: conf.value, category: category.value, title: split === 'one' ? title.value : '', split, skipBlank: skipBlank.checked } });
        job = null;
        mount(importBox, h('h3', {}, icon('check'), r.length > 1 ? `أُدرج ${nf.format(r.length)} مستندات` : 'أُدرج المستند'),
          r.map(x => h('div', { class: 'list-item' }, icon(x.duplicate ? 'link' : 'check'), h('div', { class: 'grow' }, h('span', { class: 't', text: x.archiveNo }), h('span', { class: 'muted small', text: x.duplicate ? 'موجود مسبقاً — لم يُكرر' : 'المعالجة والقراءة جارية' })), h('a', { class: 'btn sm', href: '#/doc/' + encodeURIComponent(x.id) }, 'فتح'))),
          h('button', { class: 'btn', type: 'button', style: 'margin-top:10px', onclick: () => { mount(importBox); drawPages(); drawSettings(); } }, icon('scanner'), 'مسح خطاب آخر'));
        mount(pagesBox, h('h3', {}, icon('docs'), 'الصفحات'), empty('check', 'أُدرجت الصفحات. تابع المعالجة من «الإدراج».'));
        drawSettings();
      } catch (e) { toast(e.message, { error: true }); go.disabled = false; }
    } }, icon('upload'), 'إدراج في الأرشيف');
    mount(importBox,
      h('h3', {}, icon('upload'), 'الإدراج'),
      h('div', { class: 'form', style: 'margin-top:8px' },
        h('div', { class: 'full' }, h('label', { class: 'f', text: 'التقسيم' }), splitBox),
        h('div', {}, h('label', { class: 'f', text: 'الاتجاه' }), dirSeg),
        h('div', {}, h('label', { class: 'f', text: 'السرية' }), conf),
        h('div', { class: 'full' }, h('label', { class: 'f', text: 'التصنيف' }), category, h('datalist', { id: 'scan-cats' }, (meta.categories || []).map(c => h('option', { value: c })))),
        titleRow,
        h('label', { class: 'row full', style: 'gap:8px' }, skipBlank, h('span', { class: 'small', text: 'حذف الصفحات الفارغة (ظهر الأوراق في المسح على الوجهين)' }))),
      h('div', { class: 'row', style: 'margin-top:14px' }, go), out);
  }

  mount(view, pageHead('المسح الضوئي', 'امسح الخطابات مباشرة من الماسح بالخصائص التي تريدها، وراجع الصفحات، ثم أدرجها لتُقرأ وتُربط بمعاملاتها.',
    h('a', { class: 'btn', href: '#/inbox' }, icon('upload'), 'رفع ملفات')),
  h('div', { class: 'scan-layout' },
    h('div', { class: 'stack' }, deviceBox, settingsBox),
    h('div', { class: 'stack' }, previewBox, pagesBox, importBox)));
  drawDevices();
  drawPages();
  await loadCaps();
}
