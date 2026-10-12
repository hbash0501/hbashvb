/* وحدة تحكم PTZ — بلا اعتماديات. الرمز المميّز في متغير ذاكرة فقط؛ لا innerHTML ولا style نصّي (CSP). */
(function () {
'use strict';

/* ===== أدوات ===== */
const $ = (s, r) => (r || document).querySelector(s);
const $$ = (s, r) => [...(r || document).querySelectorAll(s)];
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const p2 = (n) => String(n).padStart(2, '0');
const r4 = (v) => Math.round(v * 1e4) / 1e4;
const parseUtc = (s) => (s ? Date.parse(/(Z|[+-]\d\d:?\d\d)$/.test(s) ? s : s + 'Z') : NaN);
const fmtDT = (ms) => { const d = new Date(ms); return d.getFullYear() + '-' + p2(d.getMonth() + 1) + '-' + p2(d.getDate()) + ' ' + d.toTimeString().slice(0, 8); };
const NAME_RE = /^[\p{L}\p{Nd} _.()-]{1,32}$/u;
const unq = (s) => (s || '').replace(/^W\//, '').replace(/^"|"$/g, '');
const cp = (c, p) => '/cameras/' + c.id + p;

// بناء DOM: نصوص عبر TextNode فقط، وسمة style محظورة.
function h(tag, a, ...kids) {
  const e = document.createElement(tag);
  for (const k in a) {
    const v = a[k];
    if (v == null || v === false || k === 'style') continue;
    if (k === 'class') e.className = v;
    else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
    else e.setAttribute(k, v === true ? '' : v);
  }
  for (const c of kids.flat()) if (c != null && c !== false) e.append(c.nodeType ? c : document.createTextNode(String(c)));
  return e;
}
const btn = (t, f, a) => h('button', Object.assign({ type: 'button', onclick: f }, a), t);

/* ===== نصوص ===== */
const ERR = {
  bad_request: 'طلب غير صالح', unauthorized: 'يلزم تسجيل الدخول', session_expired: 'انتهت الجلسة، سجّل الدخول من جديد',
  invalid_credentials: 'اسم المستخدم أو كلمة المرور أو رمز التحقق غير صحيح', forbidden: 'لا تملك الصلاحية لهذا الإجراء',
  step_up_required: 'يلزم تأكيد الهوية برمز التحقق', invalid_totp: 'رمز التحقق غير صحيح', https_required: 'يلزم الاتصال عبر HTTPS',
  not_found: 'العنصر غير موجود', conflict: 'تعارض مع حالة الجهاز', precondition_failed: 'تغيّرت البيانات من مستخدم آخر',
  rate_limited: 'طلبات كثيرة جداً', too_many_streams: 'عدد البثوث المفتوحة كبير جداً', internal: 'خطأ داخلي في الخادم',
  unsupported: 'الكاميرا لا تدعم هذه الميزة', camera_error: 'رفضت الكاميرا الأمر', camera_unavailable: 'الكاميرا غير متاحة حالياً',
  busy: 'الخادم مشغول، أعد المحاولة بعد لحظات', audit_unavailable: 'سجل التدقيق غير متاح؛ التعديلات متوقفة مؤقتاً',
  timeout: 'انتهت مهلة الطلب', network: 'تعذّر الاتصال بالخادم', no_cameras: 'لا توجد كاميرات مهيّأة على الخادم'
};
const SEV = { info: 'معلومة', warning: 'تحذير', critical: 'حرج' };
const KIND = {
  Motion: 'حركة', Intrusion: 'اقتحام', LineCrossing: 'عبور خط', Loitering: 'تسكّع', Running: 'ركض', Anomaly: 'شذوذ', Gathering: 'تجمّع',
  DeviceEvent: 'حدث جهاز', System: 'النظام', ErraticMovement: 'حركة عشوائية', Stationary: 'ثبات', SpeedAnomaly: 'سرعة شاذة'
};
const ROLE = { viewer: 'مشاهد', operator: 'مشغّل', supervisor: 'مشرف', administrator: 'مدير' };
const TIER_AR = { tiny: 'صغيرة جداً', low: 'منخفضة', mid: 'متوسطة', high: 'عالية', hd: 'HD' };
const FLAGS = [[1, 'معلّق'], [2, 'تهيئة'], [4, 'تغيّر إضاءة'], [8, 'تتبّع تلقائي'], [16, 'تعلّم']];
const HEALTH = { ok: 'متصلة', cameraAuth: 'رفضت الكاميرا بيانات اعتماد البوابة', cameraUnreachable: 'لا يمكن الوصول للكاميرا' };
const POLY = 'انقر لإضافة الرؤوس؛ انقر قرب النقطة الأولى أو «إنهاء الرسم» للإغلاق.';
const HINT = { zoom: 'اسحب مستطيلاً فوق الصورة لتكبير تلك المنطقة.', target: 'انقر على الهدف المراد تتبّعه.', zone: POLY, mask: POLY, line: 'انقر نقطتين لتحديد الخط.' };

function errText(e) {
  if (!(e instanceof ApiError)) return 'حدث خطأ غير متوقع';
  let m = ERR[e.code] || (e.status >= 500 ? ERR.internal : 'تعذّر تنفيذ الطلب');
  if (e.code === 'forbidden' && e.required) m += ' (' + e.required + ')';
  if (e.code === 'bad_request' && e.field) m += ' (الحقل: ' + e.field + ')';
  if (e.retry) m += ' — أعد المحاولة بعد ' + e.retry + ' ث';
  if (e.status >= 500 && e.rid) m += ' [' + String(e.rid).slice(0, 8) + ']';
  return m;
}

/* ===== الحالة ===== */
const S = {
  token: null, me: null, perms: new Set(), mfa: true, idleSec: 1800, idleAt: 0, absAt: 0, lastAct: 0, cams: new Map(), cur: null,
  alarms: new Map(), tab: 'ctl', mode: 'view', sync: 0, adm: false, timers: [], prefs: { snd: false, ntf: false, tier: 'auto', spd: 50 }
};
const can = (p) => S.perms.has(p);
const TIERS = ['tiny', 'low', 'mid', 'high', 'hd'];
const TFPS = { tiny: 2, low: 3, mid: 4, high: 5, hd: 5 };   // نسخة من FrameTier.Ladder في الخادم
function loadPrefs() {
  try {
    const p = JSON.parse(localStorage.getItem('ptz.prefs') || '{}');
    S.prefs.snd = p.snd === true; S.prefs.ntf = p.ntf === true;
    if (p.tier === 'auto' || TIERS.includes(p.tier)) S.prefs.tier = p.tier;
    if (Number.isFinite(p.spd)) S.prefs.spd = clamp(Math.round(p.spd / 5) * 5, 5, 100);
  } catch (e) { /* التخزين غير متاح */ }
}
function savePrefs() { try { localStorage.setItem('ptz.prefs', JSON.stringify(S.prefs)); } catch (e) { /* تجاهل */ } }

/* ===== عميل الخادم ===== */
const API = '/api/v1';
class ApiError extends Error {
  constructor(status, code, o) { super(code); this.status = status; this.code = code; this.retry = 0; this.handled = false; Object.assign(this, o); }
}
const errFrom = (res, b) => new ApiError(res.status, (b && b.code) || 'http_' + res.status, {
  field: b && b.field, required: b && b.required, rid: (b && b.requestId) || res.headers.get('X-Request-Id'),
  retry: parseInt(res.headers.get('Retry-After'), 10) || (b && b.retryAfterSeconds) || 0
});

async function once(method, path, o) {
  const ctl = new AbortController(), ext = o.signal, onAbort = () => ctl.abort();
  const t = setTimeout(() => ctl.abort(), o.timeout || 15000);
  if (ext) { if (ext.aborted) ctl.abort(); else ext.addEventListener('abort', onAbort, { once: true }); }
  try {
    const headers = { Accept: 'application/json' };
    if (S.token && !o.anon) headers.Authorization = 'Bearer ' + S.token;
    if (o.body !== undefined) headers['Content-Type'] = 'application/json';
    Object.assign(headers, o.headers);
    const res = await fetch(API + path, {
      method, headers, body: o.body !== undefined ? JSON.stringify(o.body) : undefined, signal: ctl.signal,
      cache: 'no-store', credentials: 'omit', referrerPolicy: 'no-referrer', keepalive: !!o.keepalive
    });
    if (!res.ok) {
      let b = null;
      try { b = (await res.json()).error; } catch (e) { /* ليس JSON */ }
      throw errFrom(res, b);
    }
    if (o.as === 'blob') {
      const t0 = performance.now();
      return { status: res.status, h: res.headers, blob: res.status === 200 ? await res.blob() : null, dl: performance.now() - t0 };
    }
    const json = res.status !== 204 && (res.headers.get('Content-Type') || '').includes('json');
    return { status: res.status, h: res.headers, data: json ? await res.json() : null };
  } catch (e) {
    throw e instanceof ApiError ? e : new ApiError(0, ext && ext.aborted ? 'aborted' : ctl.signal.aborted ? 'timeout' : 'network');
  } finally {
    clearTimeout(t);
    if (ext) ext.removeEventListener('abort', onAbort);
  }
}

// o.full => {data,h}؛ o.as => كائن الاستجابة؛ وإلا data فقط
async function api(method, path, o) {
  o = o || {};
  try {
    const r = await once(method, path, o);
    if (method !== 'GET' && !o.anon) touchIdle();
    return o.full || o.as ? r : r.data;
  } catch (e) {
    if (e.status === 401 && !o.anon) { e.handled = true; onUnauth(e.code); }
    else if (e.code === 'step_up_required' && !o.noStep && (await stepUp())) return api(method, path, Object.assign({}, o, { noStep: true }));
    throw e;
  }
}

// يعرض الخطأ؛ يعيد النتيجة (true عند 204) أو undefined عند الفشل
async function safe(p, ok) {
  try { const r = await p; if (ok) toast(ok === true ? 'تم' : ok, 'ok'); return r == null ? true : r; }
  catch (e) { if (!e.handled && S.token) toast(errText(e), 'err'); return undefined; }
}
const got = (r) => r && r !== true;

/* ===== نوافذ وإشعارات ===== */
function toast(msg, kind) {
  const box = $('#toasts');
  while (box.children.length > 3) box.firstChild.remove();
  const t = h('div', { class: 'toast ' + (kind || '') }, msg);
  box.append(t);
  setTimeout(() => t.remove(), kind === 'err' ? 8000 : 4000);
}

let dq = Promise.resolve();
function ask(o) { const p = dq.then(() => ask1(o)); dq = p.catch(() => {}); return p; }
function ask1(o) {
  return new Promise((resolve) => {
    const d = $('#dlg'), inp = $('#dlg-c'), form = $('#dlg-f');
    $('#dlg-t').textContent = o.title; $('#dlg-x').textContent = o.text || '';
    inp.hidden = !o.totp; inp.required = !!o.totp; inp.value = '';
    $('#dlg-ok').textContent = o.ok || 'موافق';
    const fin = (v) => { form.onsubmit = $('#dlg-no').onclick = d.onclose = null; if (d.open) d.close(); resolve(v); };
    form.onsubmit = (e) => { e.preventDefault(); fin(o.totp ? inp.value.trim() : true); };
    $('#dlg-no').onclick = () => fin(null);
    d.onclose = () => fin(null);
    d.showModal();
    (o.totp ? inp : $('#dlg-ok')).focus();
  });
}

// رمز التحقق الإضافي (403 step_up_required): نسأل المستخدم مرة واحدة لكل الطلبات المتزامنة ثم نعيد الطلب
let stepP = null;
function stepUp() {
  stepP = stepP || (async () => {
    for (;;) {
      const code = await ask({ title: 'تأكيد الهوية', text: 'هذا الإجراء حساس. أدخل رمز التحقق (6 أرقام) من تطبيق المصادقة.', totp: true, ok: 'تأكيد' });
      if (!code) return false;
      try { await api('POST', '/auth/stepup', { body: { totp: code }, noStep: true }); return true; }
      catch (e) {
        if (e.handled) return false;
        toast(errText(e), 'err');
        if (e.code !== 'invalid_totp') return false;
      }
    }
  })().finally(() => { stepP = null; });
  return stepP;
}

/* ===== الجلسة ===== */
function applySession(m, idleSec) {
  S.me = m; S.perms = new Set(m.permissions || []);
  if (idleSec) S.idleSec = idleSec;
  const skew = parseUtc(m.serverTimeUtc) - Date.now() || 0;
  S.idleAt = parseUtc(m.idleExpiresUtc) - skew; S.absAt = parseUtc(m.absoluteExpiresUtc) - skew;
  if (!Number.isFinite(S.absAt)) S.absAt = Date.now() + 12 * 36e5;
  if (!Number.isFinite(S.idleAt)) touchIdle();
}
function touchIdle() { S.idleAt = Math.min(Date.now() + S.idleSec * 1000, S.absAt); }
async function extend() {
  const m = await safe(api('GET', '/auth/me'));
  if (got(m)) { applySession(m); toast('تم تمديد الجلسة', 'ok'); tick(); }
}
function onUnauth(code) { if (S.token) endSession(code === 'session_expired' ? 'انتهت الجلسة. سجّل الدخول من جديد.' : 'يلزم تسجيل الدخول.'); }

function endSession(msg) {
  stopAll(true);
  S.token = null; S.me = null; S.perms = new Set();
  for (const c of S.cams.values()) { sseStop(c); vStop(c); }
  S.timers.forEach(clearInterval); S.timers = [];
  S.cams.clear(); S.alarms.clear(); S.cur = null; S.mode = 'view'; S.adm = false;
  ['#cams', '#allist', '#imgctl', '#auxrow', '#sslist', '#aulist'].forEach((s) => $(s).replaceChildren());
  $('#app').hidden = true; $('#login').hidden = false;
  $('#lp').value = ''; $('#lt').value = ''; $('#lerr').textContent = msg || '';
  if ($('#dlg').open) $('#dlg').close();
  $('#lu').focus();
}
function logout() {
  stopAll(true);
  api('POST', '/auth/logout', { keepalive: true, timeout: 4000, noStep: true }).catch(() => {});
  endSession('تم تسجيل الخروج.');
}

function tick() {
  if (!S.token) return;
  const left = Math.min(S.idleAt, S.absAt) - Date.now(), s = Math.max(0, Math.floor(left / 1000));
  $('#cd').textContent = (s >= 3600 ? Math.floor(s / 3600) + ':' : '') + p2(Math.floor(s / 60) % 60) + ':' + p2(s % 60);
  $('.sess').classList.toggle('low', left < 12e4); $('#ext').hidden = left >= 12e4;
  if (left <= 0 && !S.chk) {   // نتحقق من الخادم (401 ⇒ الشاشة الأولى)
    S.chk = true;
    api('GET', '/auth/me').then((m) => applySession(m)).catch(() => {}).finally(() => { S.chk = false; });
  }
  const c = S.cur; if (!c) return;
  const v = c.v, ns = $('#nosig');
  v.fps = v.cnt; v.cnt = 0;
  $('#st').textContent = (v.tierUsed || '-') + ' · ' + v.fps + ' fps · #' + v.after + (c.ov ? ' · ' + c.ov.tr.length + ' ⌖' : '') +
    FLAGS.filter((f) => c.ov && c.ov.fl & f[0]).map((f) => ' · ' + f[1]).join('');
  ns.hidden = c.online && performance.now() - v.last < 8000;
  ns.textContent = c.online ? 'لا توجد إشارة' : 'الكاميرا غير متصلة';
}
// الخمول يُحسب في الخادم بالطلبات المغيِّرة أو GET /auth/me فقط؛ نجدّد فقط إن كان المستخدم نشطاً
function keepAlive() {
  if (S.token && Date.now() - S.lastAct < 3e5 && S.idleAt - Date.now() < S.idleSec * 500) api('GET', '/auth/me').then((m) => applySession(m)).catch(() => {});
}

/* ===== الكاميرات ===== */
function newCam(d) {
  return {
    id: d.id, d, online: !!d.online, reason: 'ok', engine: d.engine || {}, conn: 'idle', ov: null, evs: [], geo: null, geoBase: '', geoEtag: null,
    geoStale: false, tiers: null, presets: [], loaded: {}, sse: null,
    mv: { seq: 0, p: 0, t: 0, z: 0, held: false, timer: 0, pend: 0, last: 0, err: false },
    v: { run: false, alive: false, ctl: null, wake: null, after: 0, tier: 'low', ok: 0, slow: 0, fail: 0, lastStep: 0, lastDown: 0, last: 0, cnt: 0, fps: 0, tierUsed: '' }
  };
}
const feat = (c, f) => c.d.features.includes(f);

function renderCams() {
  const nav = $('#cams'), list = [...S.cams.values()];
  if (nav.children.length !== list.length) nav.replaceChildren(...list.map((c) => btn([h('strong'), h('span', { class: 'bdg' })], () => selectCam(c.id))));
  list.forEach((c, i) => {
    const b = nav.children[i], bad = c.conn === 'live' && !c.online;
    b.setAttribute('aria-pressed', String(c === S.cur));
    b.firstChild.textContent = c.d.displayName || c.id;
    b.lastChild.className = 'bdg ' + (c.conn !== 'live' ? 'mid' : bad ? 'bad' : 'ok');
    b.lastChild.textContent = c.conn !== 'live' ? '○ غير مباشر' : bad ? '✕ ' + (c.reason === 'cameraAuth' ? 'رفض المصادقة' : 'غير متصلة') : '● متصلة';
  });
  banner();
}
function banner() {
  const c = S.cur; let m = '', k = 'err';
  if (!navigator.onLine) m = 'لا يوجد اتصال بالشبكة.';
  else if (c) {
    if (c.conn === 'retry') { m = 'انقطع البث الحي؛ تتم إعادة الاتصال…'; k = 'warn'; }
    else if (c.conn === 'connecting') { m = 'جارٍ الاتصال بالبث الحي…'; k = 'info'; }
    else if (S.sync > 0) { m = 'جارٍ مزامنة الإنذارات…'; k = 'info'; }
    else if (!c.online) m = 'الكاميرا غير متصلة: ' + (HEALTH[c.reason] || 'غير معروف');
  }
  const b = $('#banner'); b.hidden = !m; b.className = 'banner ' + k; b.textContent = m;
}
function selectCam(id) {
  const c = S.cams.get(id);
  if (!c || c === S.cur) return;
  if (S.cur) { stopAll(false); vStop(S.cur); }
  gdCancel(); setMode('view');
  S.cur = c; c.loaded = {}; c.v.after = 0; c.v.last = 0;
  const vc = $('#vid'); vc.getContext('2d').clearRect(0, 0, vc.width, vc.height);
  tierOpts(); renderAlarms(); renderCams(); renderCtl(); drawOv();
  if (can('smartView') && !c.geo) loadGeometry(c, true);
  if (!document.hidden) vStart(c);
  showTab(S.tab);
}

/* ===== SSE عبر fetch (EventSource لا يرسل Authorization) ===== */
function sseStart(c) {
  const s = c.sse = { stop: false, ctl: null, lastId: null, attempt: 0, hb: 15, n: 0, wt: 0, wake: null, wd: 0, bye: null };
  const setConn = (st) => { c.conn = st; renderCams(); };
  (async function run() {
    while (!s.stop && S.token) {
      s.ctl = new AbortController(); s.bye = null;
      const t0 = Date.now(); let minWait = 0, fatal = false;
      setConn(s.n || s.attempt ? 'retry' : 'connecting');
      try {
        const headers = { Authorization: 'Bearer ' + S.token, Accept: 'text/event-stream' };
        if (s.lastId) headers['Last-Event-ID'] = s.lastId;
        const res = await fetch(API + '/cameras/' + encodeURIComponent(c.id) + '/events', {
          headers, signal: s.ctl.signal, cache: 'no-store', credentials: 'omit', referrerPolicy: 'no-referrer'
        });
        if (!res.ok) {
          let b = null;
          try { b = (await res.json()).error; } catch (e) { /* ليس JSON */ }
          const err = errFrom(res, b);
          if (res.status === 401) { onUnauth(err.code); return; }
          fatal = res.status === 403 || res.status === 404;
          throw err;
        }
        if (!res.body) throw new ApiError(0, 'network');
        const rd = res.body.getReader(), dec = new TextDecoder('utf-8');
        const arm = () => { clearTimeout(s.wd); s.wd = setTimeout(() => s.ctl.abort(), Math.max(20, s.hb * 2.5) * 1000); };
        let buf = '', cur = { e: '', d: [] };
        arm();
        for (;;) {
          const { value, done } = await rd.read();
          if (done) break;
          arm();
          buf = (buf + dec.decode(value, { stream: true })).replace(/\r\n/g, '\n');
          for (let i; (i = buf.indexOf('\n')) >= 0;) {
            const line = buf.slice(0, i); buf = buf.slice(i + 1);
            if (line === '') { if (cur.d.length) onEvent(c, cur.e || 'message', cur.d.join('\n')); cur = { e: '', d: [] }; continue; }
            if (line[0] === ':') continue;
            const k = line.indexOf(':'), f = k < 0 ? line : line.slice(0, k);
            let v = k < 0 ? '' : line.slice(k + 1);
            if (v[0] === ' ') v = v.slice(1);
            if (f === 'event') cur.e = v;
            else if (f === 'data') cur.d.push(v);
            else if (f === 'id') { if (!v.includes('\0')) s.lastId = v; }
            else if (f === 'retry') minWait = Math.min(parseInt(v, 10) || 0, 3e4);
          }
        }
      } catch (e) {
        if (s.stop) return;
        minWait = Math.max(minWait, (e.retry || 0) * 1000);
        if (fatal) { setConn('idle'); toast('تعذّر فتح البث الحي: ' + errText(e), 'err'); return; }
      } finally { clearTimeout(s.wd); }
      if (s.stop || !S.token) return;
      c.ov = null; c.evs = []; ovDirty();
      if (s.bye === 'revoked' || s.bye === 'sessionExpired') {
        if (!(await api('GET', '/auth/me').then((m) => { applySession(m); return true; }).catch(() => false))) return;
      }
      s.attempt = Date.now() - t0 > 1e4 ? 0 : s.attempt + 1;
      if (s.bye === 'shutdown') minWait = Math.max(minWait, 3000);
      setConn('retry');
      const wait = Math.max(minWait, Math.min(3e4, 1000 * 2 ** Math.min(s.attempt, 5)) * (0.8 + Math.random() * 0.4));
      await new Promise((r) => { s.wake = r; s.wt = setTimeout(r, wait); });
    }
  })();
}
function sseStop(c) {
  const s = c.sse; if (!s) return;
  s.stop = true; clearTimeout(s.wt); clearTimeout(s.wd);
  if (s.ctl) s.ctl.abort();
  if (s.wake) s.wake();
}

function onEvent(c, ev, raw) {
  let d;
  try { d = JSON.parse(raw); } catch (e) { return; }
  if (!d || typeof d !== 'object') return;
  const s = c.sse;
  switch (ev) {
    case 'hello':
      s.hb = d.heartbeatSeconds || 15;
      if (!s.lastId && d.alarmSeq) s.lastId = String(d.alarmSeq);
      if (Array.isArray(d.tiers)) { c.tiers = d.tiers.filter((t) => TIERS.includes(t)); tierOpts(); }
      c.online = !!d.online; if (d.online) c.reason = 'ok';
      c.engine = d.engine || c.engine;
      if (s.n++ > 0) loadBacklog();      // إعادة اتصال: أحداث ack/geo قد فاتت
      if (c.geoEtag && d.geometryRev && d.geometryRev !== unq(c.geoEtag)) geoRemote(c, null);
      c.conn = 'live'; renderCams(); renderSmart();
      break;
    case 'ov':
      c.ov = d;
      for (const e of d.ev || []) c.evs.push({ k: e.k, x: e.x, y: e.y, t: performance.now() });
      c.evs.splice(0, Math.max(0, c.evs.length - 16));
      ovDirty();
      break;
    case 'alarm': addAlarm(d, true); break;
    case 'ack': { const a = S.alarms.get(d.id); if (a) { a.acknowledged = true; a.ackedBy = d.by; renderAlarms(); } break; }
    case 'state':
      c.engine = { motionEnabled: d.motionEnabled, behaviorEnabled: d.behaviorEnabled, autoTrackEnabled: d.autoTrackEnabled, forceAnomalyLearning: d.forceAnomalyLearning };
      renderSmart();
      if (d.by && d.by !== S.me.userName) toast('تغيّرت إعدادات الذكاء بواسطة ' + d.by);
      break;
    case 'geo': if (d.rev !== unq(c.geoEtag)) geoRemote(c, d.by); break;
    case 'health': c.online = !!d.online; c.reason = d.reason || 'ok'; renderCams(); break;
    case 'resync': if ((d.what || []).includes('alarms')) loadBacklog(); break;
    case 'bye': s.bye = d.reason || ''; break;
    default: break;
  }
}

/* ===== الفيديو: long-poll على /frame مع طبقة جودة تكيّفية ===== */
const tiersOf = (c) => c.tiers || TIERS;
const tierNow = (c) => (S.prefs.tier !== 'auto' && tiersOf(c).includes(S.prefs.tier) ? S.prefs.tier : tiersOf(c).includes(c.v.tier) ? c.v.tier : tiersOf(c)[0]);
function tierOpts() {
  const sel = $('#tier'), l = S.cur ? tiersOf(S.cur) : TIERS;
  sel.replaceChildren(h('option', { value: 'auto' }, 'تلقائي'), ...l.map((t) => h('option', { value: t }, TIER_AR[t])));
  sel.value = l.includes(S.prefs.tier) ? S.prefs.tier : 'auto';
}
function vStart(c) {
  const v = c.v; v.run = true;
  if (v.alive) { if (v.wake) v.wake(); return; }
  v.alive = true; v.after = 0;
  (async function loop() {
    try {
      while (v.run && S.token && S.cur === c) {
        if (document.hidden) { await new Promise((r) => { v.wake = r; }); v.wake = null; continue; }
        const tier = tierNow(c);
        v.ctl = new AbortController();
        try {
          const r = await api('GET', cp(c, '/frame?tier=' + tier + '&after=' + v.after + '&wait=3000'), { as: 'blob', signal: v.ctl.signal, timeout: 9000 });
          v.fail = 0;
          if (r.status === 200 && r.blob) {
            if (r.blob.size < 100) throw new ApiError(0, 'bad_frame');
            const used = r.h.get('X-Frame-Tier') || tier;
            v.after = parseInt(r.h.get('X-Frame-Seq'), 10) || v.after;
            await drawFrame(c, r.blob);
            v.last = performance.now(); v.cnt++; v.tierUsed = used;
            if (TIERS.indexOf(used) < TIERS.indexOf(tier)) c.tiers = TIERS.slice(0, TIERS.indexOf(used) + 1);   // الخادم خفّض الطبقة (MaxTier)
            adapt(c, used, r.dl);
          } else {   // 204: لا إطار أحدث ضمن المهلة
            const sq = parseInt(r.h.get('X-Frame-Seq'), 10);
            if (sq && sq < v.after) v.after = 0;
            if (r.h.get('X-Camera-State') === 'offline') c.online = false;
            await sleep(150);
          }
        } catch (e) {
          if (!v.run || !S.token) break;
          if (e.code === 'aborted') continue;
          v.ok = 0;
          if (++v.fail >= 2) stepTier(c, -1);
          await sleep(Math.min(8000, Math.max(e.retry * 1000, 500 * 2 ** Math.min(v.fail, 4))));
        }
      }
    } finally { v.alive = false; }
  })();
}
function vStop(c) { c.v.run = false; if (c.v.ctl) c.v.ctl.abort(); if (c.v.wake) c.v.wake(); }
function stepTier(c, dir) {
  const v = c.v, l = tiersOf(c), i = l.indexOf(v.tier) + dir;
  if (i < 0 || i >= l.length) return;
  v.tier = l[i]; v.lastStep = Date.now(); v.ok = v.slow = 0;
  if (dir < 0) v.lastDown = v.lastStep;
}
// زمن تنزيل الجسم نسبةً إلى فترة الإطار: بطيء ⇒ نزول، سريع ثابت ⇒ صعود محسوب
function adapt(c, tier, dl) {
  const v = c.v;
  if (S.prefs.tier !== 'auto') return;
  v.tier = tier;
  const r = dl / (1000 / TFPS[tier]), l = tiersOf(c), nx = l[l.indexOf(tier) + 1], now = Date.now();
  if (r > 0.7) { v.slow++; v.ok = 0; } else if (r < 0.15) { v.ok++; v.slow = 0; } else v.ok = v.slow = 0;
  if (v.slow >= 2) stepTier(c, -1);
  else if (nx && v.ok >= 8 && now - v.lastStep > 6000 && now - v.lastDown > 3e4 && dl * 2.2 / (1000 / TFPS[nx]) < 0.4) stepTier(c, 1);
}
async function drawFrame(c, blob) {
  let bmp;
  try { bmp = await createImageBitmap(blob); } catch (e) { throw new ApiError(0, 'bad_frame'); }
  if (S.cur === c && c.v.run) {
    const vc = $('#vid');
    if (vc.width !== bmp.width || vc.height !== bmp.height) {
      vc.width = bmp.width; vc.height = bmp.height;
      $('#stage').style.aspectRatio = bmp.width + ' / ' + bmp.height;   // CSSOM مسموح بـ style-src 'self'
    }
    vc.getContext('2d').drawImage(bmp, 0, 0);
  }
  if (bmp.close) bmp.close();
}

/* ===== الطبقة الشفافة: تراكب التتبع + الهندسة + أدوات الرسم ===== */
const SEVCOL = { info: '#4aa3ff', warning: '#ffb020', critical: '#ff4d4f' };
const gd = { kind: null, pts: [] };
let ovRaf = 0, drag = null, hover = null, dpr = 1;

function ovDirty() { if (!ovRaf) ovRaf = requestAnimationFrame(drawOv); }
function sizeOv() {
  const cv = $('#ov'), r = cv.getBoundingClientRect();
  dpr = Math.min(window.devicePixelRatio || 1, 2);
  cv.width = Math.max(1, Math.round(r.width * dpr)); cv.height = Math.max(1, Math.round(r.height * dpr));
  ovDirty();
}
function drawOv() {
  ovRaf = 0;
  const cv = $('#ov'), g = cv.getContext('2d'), W = cv.width, H = cv.height, c = S.cur;
  g.clearRect(0, 0, W, H);
  if (!c) return;
  const X = (v) => v * W, Y = (v) => v * H, u = dpr;
  const poly = (pts, close) => { g.beginPath(); pts.forEach((p, i) => g[i ? 'lineTo' : 'moveTo'](X(p.x), Y(p.y))); if (close) g.closePath(); };
  const label = (t, x, y, col) => { g.fillStyle = 'rgba(0,0,0,.6)'; g.fillRect(x, y - 13 * u, g.measureText(t).width + 6 * u, 15 * u); g.fillStyle = col; g.fillText(t, x + 3 * u, y - 2 * u); };
  g.font = 12 * u + 'px system-ui,sans-serif'; g.direction = 'ltr'; g.textAlign = 'left'; g.lineJoin = 'round'; g.lineWidth = 2 * u; g.setLineDash([]);
  if (c.geo && $('#showgeo').checked) {
    for (const m of c.geo.masks) { poly(m, true); g.fillStyle = 'rgba(0,0,0,.5)'; g.fill(); g.strokeStyle = '#aaa'; g.stroke(); }
    for (const z of c.geo.zones) {
      const col = SEVCOL[z.severity] || SEVCOL.info;
      poly(z.points, true); g.fillStyle = col + '30'; g.fill(); g.strokeStyle = col; g.stroke();
      if (z.points[0]) label(z.name, X(z.points[0].x), Y(z.points[0].y), col);
    }
    for (const l of c.geo.lines) {
      const ax = X(l.a.x), ay = Y(l.a.y), bx = X(l.b.x), by = Y(l.b.y);
      g.strokeStyle = '#00e5ff'; poly([l.a, l.b]); g.stroke();
      if (l.direction !== 'both') {   // سهم عمودي يبيّن اتجاه العبور المرصود
        const a = Math.atan2(by - ay, bx - ax) + (l.direction === 'leftToRight' ? 1 : -1) * Math.PI / 2, mx = (ax + bx) / 2, my = (ay + by) / 2;
        g.beginPath(); g.moveTo(mx, my); g.lineTo(mx + Math.cos(a) * 14 * u, my + Math.sin(a) * 14 * u); g.stroke();
      }
      label(l.name, ax, ay, '#00e5ff');
    }
  }
  for (const t of (c.ov && c.ov.tr) || []) {   // إحداثيات التراكب بالألف من الإطار
    if (!t.b || t.b.length !== 4) continue;
    const col = t.k ? '#3ddc84' : '#ffd166', P = t.p || [];
    if (P.length >= 4) { g.beginPath(); for (let i = 0; i + 1 < P.length; i += 2) g[i ? 'lineTo' : 'moveTo'](X(P[i] / 1000), Y(P[i + 1] / 1000)); g.strokeStyle = col + 'aa'; g.stroke(); }
    g.setLineDash(t.k ? [] : [5 * u, 4 * u]); g.strokeStyle = col;
    g.strokeRect(X(t.b[0] / 1000), Y(t.b[1] / 1000), X(t.b[2] / 1000), Y(t.b[3] / 1000)); g.setLineDash([]);
    label('#' + t.i + (t.s ? ' ' + (t.s / 1000).toFixed(2) : ''), X(t.b[0] / 1000), Y(t.b[1] / 1000), col);
  }
  const now = performance.now();
  c.evs = c.evs.filter((e) => now - e.t < 4000);
  for (const e of c.evs) {
    const x = X(e.x / 1000), y = Y(e.y / 1000);
    g.strokeStyle = '#ff4d4f'; g.beginPath(); g.arc(x, y, 14 * u, 0, 7); g.stroke(); label(KIND[e.k] || e.k, x + 16 * u, y, '#ff8a8a');
  }
  g.strokeStyle = g.fillStyle = '#fff';
  if (gd.pts.length) {
    poly(hover && (gd.kind !== 'line' || gd.pts.length < 2) ? gd.pts.concat([hover]) : gd.pts); g.stroke();
    gd.pts.forEach((p) => { g.beginPath(); g.arc(X(p.x), Y(p.y), 4 * u, 0, 7); g.fill(); });
  }
  if (drag && S.mode === 'zoom') {
    g.setLineDash([6 * u, 4 * u]);
    g.strokeRect(X(Math.min(drag.a.x, drag.b.x)), Y(Math.min(drag.a.y, drag.b.y)), X(Math.abs(drag.b.x - drag.a.x)), Y(Math.abs(drag.b.y - drag.a.y)));
  }
  if (c.evs.length) ovDirty();
}
function normPt(e) {
  const r = $('#ov').getBoundingClientRect();
  return { x: r4(clamp((e.clientX - r.left) / r.width, 0, 1)), y: r4(clamp((e.clientY - r.top) / r.height, 0, 1)) };
}
function setMode(m) {
  S.mode = m; $('#stage').dataset.mode = m; drag = hover = null;
  $('#t-zoom').setAttribute('aria-pressed', String(m === 'zoom')); $('#t-tgt').setAttribute('aria-pressed', String(m === 'target'));
  $('#modehint').hidden = !HINT[m]; $('#modehint').textContent = HINT[m] || '';
  if (!HINT[m] || m === 'zoom' || m === 'target') { gd.kind = null; gd.pts = []; $('#geobar').hidden = true; }
  ovDirty();
}
function initStage() {
  const ov = $('#ov');
  new ResizeObserver(sizeOv).observe($('#stage'));
  ov.addEventListener('pointerdown', (e) => {
    if (S.mode === 'view' || !S.cur || e.button > 0) return;
    e.preventDefault(); ov.setPointerCapture(e.pointerId);
    const p = normPt(e); drag = { id: e.pointerId, a: p, b: p }; ovDirty();
  });
  ov.addEventListener('pointermove', (e) => { if (S.mode === 'view') return; const p = normPt(e); if (drag) drag.b = p; else hover = p; ovDirty(); });
  ov.addEventListener('pointerup', (e) => {
    if (!drag || drag.id !== e.pointerId) return;
    const d = drag, c = S.cur, m = S.mode; d.b = normPt(e); drag = null;
    if (m === 'zoom') {
      const x = Math.min(d.a.x, d.b.x), y = Math.min(d.a.y, d.b.y), w = Math.abs(d.b.x - d.a.x), hh = Math.abs(d.b.y - d.a.y);
      setMode('view');
      if (w < 0.02 || hh < 0.02) return toast('المستطيل صغير جداً؛ اسحب منطقة أكبر.', 'err');
      safe(api('POST', cp(c, '/ptz/zoom-area'), { body: { x, y, width: r4(Math.min(w, 1 - x)), height: r4(Math.min(hh, 1 - y)) } }), true);
    } else if (m === 'target') {
      setMode('view'); safe(api('POST', cp(c, '/smart/target'), { body: d.b }), 'تم قفل الهدف');
    } else if (gd.kind) gdPoint(d.b);
  });
  ov.addEventListener('pointercancel', () => { drag = null; ovDirty(); });
  ov.addEventListener('pointerleave', () => { hover = null; ovDirty(); });
  $('#showgeo').addEventListener('change', ovDirty);
  $('#t-zoom').addEventListener('click', () => setMode(S.mode === 'zoom' ? 'view' : 'zoom'));
  $('#t-tgt').addEventListener('click', () => setMode(S.mode === 'target' ? 'view' : 'target'));
  $('#s-tgt').addEventListener('click', () => { setMode('target'); $('#stage').scrollIntoView({ block: 'nearest' }); });
  $('#tier').addEventListener('change', (e) => { S.prefs.tier = e.target.value; savePrefs(); if (S.cur) S.cur.v.after = 0; });
}

/* ===== الحركة PTZ: عقد (lease) بتجديد دوري + seq متزايد + إيقاف مضمون ===== */
const TTL = 1200;                       // ttlMs المطلوب (الخادم يقبل 200–2000)
const inp = { pad: [0, 0], z: 0, key: [0, 0, 0] };
const lensOn = { focus: null, iris: null };
const down = new Set();
let padEnd = () => {};

function mvSend(c, renew) {
  const m = c.mv; m.last = Date.now();
  const body = { pan: m.p, tilt: m.t, zoom: m.z, ttlMs: TTL, seq: ++m.seq };
  if (renew) body.renew = true;
  api('POST', cp(c, '/ptz/move'), { body, timeout: 4000 }).catch((e) => {
    if (e.handled || !m.held || e.code === 'stale_seq' || e.code === 'rate_limited') return;
    if (!m.err) { m.err = true; toast(errText(e), 'err'); }
    if (e.status >= 400 && e.status < 500) mvRelease(c, false, true);
  });
}
function mvSet(c, p, t, z) {
  const m = c.mv;
  if (!p && !t && !z) return mvRelease(c, false, false);
  const changed = p !== m.p || t !== m.t || z !== m.z;
  m.p = p; m.t = t; m.z = z;
  if (!m.held) { m.held = true; m.err = false; m.timer = setInterval(() => { if (m.held) mvSend(c, true); }, TTL / 3); }
  if (!changed) return;
  const wait = 90 - (Date.now() - m.last);
  if (wait <= 0) { clearTimeout(m.pend); m.pend = 0; mvSend(c, false); }
  else if (!m.pend) m.pend = setTimeout(() => { m.pend = 0; if (m.held) mvSend(c, false); }, wait);
}
function mvRelease(c, ka, force) {
  const m = c.mv;
  if (!m.held && !force) return;
  m.held = false; clearInterval(m.timer); clearTimeout(m.pend); m.pend = 0; m.p = m.t = m.z = 0;
  stopSend(c, ka, 0);
}
function stopSend(c, ka, n) {
  api('POST', cp(c, '/ptz/stop'), { body: { seq: ++c.mv.seq }, keepalive: !!ka, timeout: 4000 }).catch((e) => {
    if (e.handled || !S.token) return;
    if ((e.status === 0 || e.status >= 500 || e.status === 429) && n < 3) setTimeout(() => stopSend(c, ka, n + 1), 250 * (n + 1));
    else toast('تعذّر إيقاف الحركة: ' + errText(e), 'err');
  });
}
function applyInput() {
  const c = S.cur; if (!c) return;
  mvSet(c, clamp(inp.pad[0] + inp.key[0], -100, 100), clamp(inp.pad[1] + inp.key[1], -100, 100), clamp(inp.z + inp.key[2], -100, 100));
}
function lensGo(k, d) { const c = S.cur; if (c) { lensOn[k] = c; safe(api('POST', cp(c, '/lens/' + k), { body: { direction: d, speed: S.prefs.spd } })); } }
function lensHalt(k, ka) {
  const c = lensOn[k]; if (!c) return;
  lensOn[k] = null;
  api('POST', cp(c, '/lens/' + k), { body: { direction: 'halt', speed: 50 }, keepalive: !!ka, timeout: 4000 }).catch((e) => { if (!e.handled) toast(errText(e), 'err'); });
}
// إيقاف شامل (تركيز مفقود/إخفاء/خروج/تبديل كاميرا)
function stopAll(ka) {
  down.clear(); inp.pad = [0, 0]; inp.z = 0; inp.key = [0, 0, 0]; padEnd();
  $$('.hold.act').forEach((b) => b.classList.remove('act'));
  for (const c of S.cams.values()) mvRelease(c, ka, false);
  lensHalt('focus', ka); lensHalt('iris', ka);
}
function estop() {
  down.clear(); inp.pad = [0, 0]; inp.z = 0; inp.key = [0, 0, 0]; padEnd();
  for (const c of S.cams.values()) if (c === S.cur || c.mv.held) mvRelease(c, false, true);
  lensHalt('focus', false); lensHalt('iris', false);
  toast('أُرسل أمر الإيقاف', 'ok');
}

const q5 = (v) => (v ? Math.sign(v) * Math.max(5, Math.round(Math.abs(v) / 5) * 5) : 0);
function initPad() {
  const pad = $('#pad'), knob = $('#knob');
  let pid = null, R = 1, cx = 0, cy = 0;
  const upd = (e) => {
    let dx = (e.clientX - cx) / R, dy = (e.clientY - cy) / R, m = Math.hypot(dx, dy);
    if (m > 1) { dx /= m; dy /= m; m = 1; }
    knob.style.transform = 'translate(' + dx * R * 0.66 + 'px,' + dy * R * 0.66 + 'px)';
    if (m < 0.15) { inp.pad = [0, 0]; return applyInput(); }
    const k = (m - 0.15) / 0.85 / m;
    let ux = dx * k, uy = dy * k;
    if (Math.abs(ux) < 0.25 * Math.abs(uy)) ux = 0; else if (Math.abs(uy) < 0.25 * Math.abs(ux)) uy = 0;
    inp.pad = [q5(ux * S.prefs.spd), q5(-uy * S.prefs.spd)];   // أعلى الشاشة ⇒ tilt موجب
    applyInput();
  };
  const end = (e) => {
    if (e && e.pointerId !== pid) return;
    pid = null; pad.classList.remove('on'); knob.style.transform = ''; inp.pad = [0, 0];
    if (e) applyInput();
  };
  padEnd = () => { if (pid !== null) { try { pad.releasePointerCapture(pid); } catch (x) { /* لا شيء */ } } end(null); };
  pad.addEventListener('pointerdown', (e) => {
    if (pid !== null || !S.cur || e.button > 0) return;
    pid = e.pointerId; pad.setPointerCapture(pid);
    const r = pad.getBoundingClientRect(); R = r.width / 2; cx = r.left + R; cy = r.top + R;
    pad.classList.add('on'); pad.focus({ preventScroll: true }); e.preventDefault(); upd(e);
  });
  pad.addEventListener('pointermove', (e) => { if (e.pointerId === pid) upd(e); });
  ['pointerup', 'pointercancel', 'lostpointercapture'].forEach((t) => pad.addEventListener(t, end));

  $$('.hold').forEach((b) => {   // أزرار الضغط المستمر: تقريب / تركيز / حدقة
    let id = null;
    const on = () => { b.classList.add('act'); if (b.dataset.k === 'z') { inp.z = +b.dataset.d * S.prefs.spd; applyInput(); } else lensGo(b.dataset.k, b.dataset.d); };
    const off = () => { b.classList.remove('act'); if (b.dataset.k === 'z') { inp.z = 0; applyInput(); } else lensHalt(b.dataset.k, false); };
    b.addEventListener('pointerdown', (e) => { if (id !== null || e.button > 0) return; id = e.pointerId; b.setPointerCapture(id); e.preventDefault(); on(); });
    ['pointerup', 'pointercancel', 'lostpointercapture'].forEach((t) => b.addEventListener(t, (e) => { if (e.pointerId === id) { id = null; off(); } }));
    b.addEventListener('keydown', (e) => { if ((e.key === ' ' || e.key === 'Enter') && !e.repeat && id === null) { e.preventDefault(); on(); } });
    b.addEventListener('keyup', (e) => { if ((e.key === ' ' || e.key === 'Enter') && id === null && b.classList.contains('act')) off(); });
  });
  $$('.pad,.hold').forEach((b) => b.addEventListener('contextmenu', (e) => e.preventDefault()));

  const spd = $('#spd');
  spd.value = S.prefs.spd; $('#spdv').textContent = S.prefs.spd;
  spd.addEventListener('input', () => { S.prefs.spd = +spd.value; $('#spdv').textContent = spd.value; });
  spd.addEventListener('change', savePrefs);
  $('#estop').addEventListener('click', estop);
}

const KEYS = { ArrowUp: [0, 1, 0], ArrowDown: [0, -1, 0], ArrowLeft: [-1, 0, 0], ArrowRight: [1, 0, 0], '+': [0, 0, 1], '=': [0, 0, 1], '-': [0, 0, -1], _: [0, 0, -1] };
function keyVec() {
  let p = 0, t = 0, z = 0;
  for (const k of down) { p += KEYS[k][0]; t += KEYS[k][1]; z += KEYS[k][2]; }
  const s = S.prefs.spd;
  inp.key = [clamp(p, -1, 1) * s, clamp(t, -1, 1) * s, clamp(z, -1, 1) * s];
  applyInput();
}
const typing = (t) => /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName) || t.isContentEditable || !!t.closest('[role=tablist],dialog');
function initKeys() {
  document.addEventListener('keydown', (e) => {
    S.lastAct = Date.now();
    if (!S.token || e.ctrlKey || e.metaKey || e.altKey || $('#dlg').open) return;
    if (e.key === 'Escape') { if (S.mode !== 'view') setMode('view'); estop(); return; }
    if (typing(e.target)) return;
    if (e.key === ' ' && !/^(BUTTON|A|SUMMARY)$/.test(e.target.tagName)) { e.preventDefault(); estop(); return; }
    if (!KEYS[e.key] || !can('ptzControl')) return;
    e.preventDefault();
    if (!e.repeat && !down.has(e.key)) { down.add(e.key); keyVec(); }
  });
  document.addEventListener('keyup', (e) => { if (down.delete(e.key)) keyVec(); });
  window.addEventListener('blur', () => stopAll(true));
  window.addEventListener('pagehide', () => stopAll(true));
  document.addEventListener('visibilitychange', () => {
    const c = S.cur;
    if (document.hidden) { stopAll(true); if (c && c.v.ctl) c.v.ctl.abort(); }
    else if (c && S.token) { c.v.after = 0; vStart(c); }
  });
  document.addEventListener('pointerdown', () => { S.lastAct = Date.now(); initAudio(); }, { passive: true });
  window.addEventListener('online', () => { banner(); for (const c of S.cams.values()) if (c.sse && c.sse.wake) { clearTimeout(c.sse.wt); c.sse.wake(); } });
  window.addEventListener('offline', banner);
}

/* ----- المواضع والدوريات والرئيسية ----- */
function renderCtl() {
  const c = S.cur;
  const ptz = feat(c, 'ptz') && can('ptzControl'), lens = feat(c, 'focus') && can('imageControl'), pro = c.d.profile || {};
  $('#ctl-na').hidden = ptz || lens; $('#padgrp').hidden = !ptz; $('#lensgrp').hidden = !lens;
  $('#b-home').hidden = !(feat(c, 'homePosition') && can('ptzControl')); $('#b-sethome').hidden = !(feat(c, 'homePosition') && can('presetManage'));
  $('#t-zoom').hidden = !ptz; $('#t-tgt').hidden = !can('autoTrack'); $('#estop').disabled = !can('ptzControl');
  $('#d-pre').hidden = !(feat(c, 'presets') && can('presetUse')); $('#prefm').hidden = !can('presetManage');
  $('#d-pat').hidden = !((feat(c, 'patrol') || feat(c, 'pattern')) && can('presetUse'));
  $('#r-patrol').hidden = !feat(c, 'patrol'); $('#r-pattern').hidden = !feat(c, 'pattern');
  $('#pre-n').max = pro.maxPresets || 255; $('#pa-n').max = pro.maxPatrols || 8; $('#pt-n').max = pro.maxPatterns || 8;
  if (!$('#d-pre').hidden) loadPresets(c);
  renderPresets();
}
async function loadPresets(c) {
  const r = await safe(api('GET', cp(c, '/presets')));
  if (got(r)) { c.presets = r.items.slice().sort((a, b) => a.id - b.id); renderPresets(); }
}
function renderPresets() {
  const c = S.cur, ul = $('#prelist');
  ul.replaceChildren(...(c.presets.length ? c.presets : [null]).map((p) => !p ? h('li', { class: 'muted' }, 'لا توجد مواضع محفوظة.') : h('li', null,
    h('b', { class: 'ltr' }, '#' + p.id), h('span', { class: 'grow' }, p.name || '—'),
    btn('انتقال', () => safe(api('POST', cp(c, '/presets/' + p.id + '/goto')), true)),
    can('presetManage') && btn('حذف', async () => {
      if (await ask({ title: 'حذف موضع', text: 'حذف الموضع «' + p.name + '» (#' + p.id + ')؟', ok: 'حذف' }) && await safe(api('DELETE', cp(c, '/presets/' + p.id)))) loadPresets(c);
    }))));
}
function initCtl() {
  $('#b-home').addEventListener('click', () => safe(api('POST', cp(S.cur, '/ptz/home')), true));
  $('#b-sethome').addEventListener('click', async () => {
    if (await ask({ title: 'تعيين الموضع الرئيسي', text: 'سيصبح الموضع الحالي للكاميرا هو الموضع الرئيسي. متابعة؟', ok: 'تعيين' })) safe(api('PUT', cp(S.cur, '/ptz/home')), true);
  });
  $('#prefm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const c = S.cur, n = parseInt($('#pre-n').value, 10), name = $('#pre-nm').value.trim();
    if (!NAME_RE.test(name)) return toast('الاسم من 1 إلى 32 حرفاً (حروف وأرقام ومسافة و _ - . ( ) فقط).', 'err');
    if (c.presets.some((p) => p.id === n) && !(await ask({ title: 'استبدال موضع', text: 'الموضع #' + n + ' موجود؛ استبداله؟', ok: 'استبدال' }))) return;
    if (await safe(api('PUT', cp(c, '/presets/' + n), { body: { name } }), true)) { $('#pre-nm').value = ''; loadPresets(c); }
  });
  for (const [row, base] of [['#r-patrol', '/patrols/'], ['#r-pattern', '/patterns/']]) {
    $$('button', $(row)).forEach((b) => b.addEventListener('click', () => {
      const n = parseInt($('input', $(row)).value, 10);
      if (n >= 1) safe(api('POST', cp(S.cur, base + n + '/' + b.dataset.a)), true);
    }));
  }
}

/* ===== لوحة الصورة ===== */
// الخادم لا يرسل النطاقات في ImageDto؛ هذا الفهرس نسخة من ImageParamCatalog (الرقمي 0–100).
const IMG = {
  brightness: ['السطوع'], contrast: ['التباين'], saturation: ['التشبّع'], sharpness: ['الحدّة'], wdrLevel: ['مستوى WDR'],
  irBrightness: ['سطوع الأشعة تحت الحمراء'], noiseReduceLevel: ['خفض الضجيج'],
  exposureType: ['نمط التعريض', ['auto', 'IrisFirst', 'ShutterFirst', 'gainFirst', 'manual']],
  irCutMode: ['فلتر IR-Cut', ['auto', 'day', 'night']], wdrMode: ['وضع WDR', ['open', 'close', 'auto']],
  blcMode: ['تعويض الإضاءة الخلفية', ['off', 'up', 'down', 'left', 'right', 'center', 'multiArea', 'auto', 'custom']],
  whiteBalanceStyle: ['توازن الأبيض', ['auto1', 'auto2', 'manual', 'indoor', 'outdoor', 'daylightLamp', 'sodiumLight']],
  supplementLightMode: ['الإضاءة المساندة', ['close', 'irLight', 'colorVuWhiteLight', 'eventIntelligence', 'schedule']],
  focusStyle: ['نمط التركيز', ['AUTO', 'MANUAL', 'SEMIAUTOMATIC']], defogMode: ['إزالة الضباب', ['open', 'close', 'auto']]
};
const AUX = [['light', 'الإضاءة المساندة (IR)', 'supplementLight'], ['wiper', 'المساحة', 'wiper']];
async function loadImage(c) {
  const d = await safe(api('GET', cp(c, '/image')));
  if (!got(d) || c !== S.cur) return;
  const box = $('#imgctl'), ok = can('imageControl'), vals = d.values || {};
  $('#img-ro').hidden = ok;
  const put = async (k, v) => { if (!(await safe(api('PUT', cp(c, '/image'), { body: { values: { [k]: String(v) } } })))) loadImage(c); };
  box.replaceChildren(...(d.supported || []).map((k) => {
    const sp = IMG[k] || [k], id = 'im-' + k, val = vals[k];
    let ctl, out = null;
    if (sp[1]) {
      ctl = h('select', { id, disabled: !ok }, sp[1].map((o) => h('option', { value: o }, o)));
      const hit = [...ctl.options].find((o) => o.value.toLowerCase() === String(val).toLowerCase());
      if (val != null && !hit) ctl.append(h('option', { value: val }, val));
      if (val != null) ctl.value = hit ? hit.value : val;
    } else {
      ctl = h('input', IMG[k] ? { id, type: 'range', min: 0, max: 100, step: 1, disabled: !ok } : { id, maxlength: 32, disabled: !ok });
      ctl.value = val != null ? val : IMG[k] ? 50 : '';
      if (IMG[k]) { out = h('output', { class: 'ltr', for: id }, ctl.value); ctl.addEventListener('input', () => { out.textContent = ctl.value; }); }
    }
    ctl.addEventListener('change', () => put(k, ctl.value));
    return h('div', { class: 'rng' }, h('label', { for: id }, sp[0]), ctl, out);
  }));
  $('#auxrow').replaceChildren(...AUX.filter((a) => feat(c, a[2])).map(([kind, label]) => h('div', { class: 'row', role: 'group', 'aria-label': label }, h('span', null, label),
    ...[[true, 'تشغيل'], [false, 'إيقاف']].map(([en, t]) => btn(t, () => safe(api('PUT', cp(c, '/aux/' + kind), { body: { enabled: en } }), t + ': ' + label), { disabled: !ok })))));
}

/* ===== لوحة الذكاء ===== */
const BEH = [   // [الاسم، التسمية، أدنى، أعلى، 1=صحيح]؛ النطاقات من WireMap.ToBehavior
  ['loiterSeconds', 'مدة التسكّع (ث)', 0.1, 3600], ['loiterRadius', 'نصف قطر التسكّع', 0.005, 0.5], ['runSpeed', 'سرعة الركض', 0.01, 5],
  ['runMinSeconds', 'أدنى مدة ركض (ث)', 0.1, 60], ['stationarySeconds', 'مدة الثبات (ث)', 0.1, 3600], ['stationarySpeed', 'سرعة الثبات', 0.0001, 1],
  ['erraticReversals', 'عدد الانعكاسات', 1, 50, 1], ['erraticWindowSeconds', 'نافذة الحركة العشوائية (ث)', 0.5, 300],
  ['erraticMinSegment', 'أدنى مقطع عشوائي', 0.001, 0.5], ['gatherCount', 'عدد المتجمّعين', 2, 50, 1], ['gatherRadius', 'نصف قطر التجمّع', 0.01, 0.5],
  ['eventCooldownSeconds', 'فاصل تكرار الحدث (ث)', 0, 3600], ['minTrackAgeSeconds', 'أدنى عمر مسار (ث)', 0, 60]
];
function renderSmart() {
  const c = S.cur; if (!c) return;
  const e = c.engine || {}, has = typeof e.motionEnabled === 'boolean', cfg = can('smartConfigure');
  $('#smt-na').hidden = has;
  $('#s-mot').checked = !!e.motionEnabled; $('#s-beh').checked = !!e.behaviorEnabled; $('#s-trk').checked = !!e.autoTrackEnabled;
  $('#s-learn').value = e.forceAnomalyLearning === true ? 'on' : e.forceAnomalyLearning === false ? 'off' : 'auto';
  $('#s-mot').disabled = $('#s-beh').disabled = $('#s-learn').disabled = !(has && cfg);
  $('#s-trk').disabled = !(has && can('autoTrack'));
  $('#s-tgt').hidden = $('#s-rel').hidden = !can('autoTrack'); $('#s-save').hidden = !cfg;
}
async function putState(patch) {
  const c = S.cur, r = await safe(api('PUT', cp(c, '/smart/state'), { body: patch }));
  if (got(r)) c.engine = r;
  renderSmart();   // عند الفشل يعود العنصر إلى الحالة المعروفة
}
async function loadBehavior(c) {
  const d = await safe(api('GET', cp(c, '/smart/behavior')));
  if (!got(d) || c !== S.cur) return;
  const cfg = can('smartConfigure');
  $('#behf').replaceChildren(...BEH.map(([k, label, lo, hi, int]) => {
    const i = h('input', { id: 'b-' + k, type: 'number', min: lo, max: hi, step: int ? 1 : 'any', required: true, disabled: !cfg, dir: 'ltr' });
    i.value = d[k];
    return h('label', { class: 'fld', for: 'b-' + k }, label, i);
  }), cfg && h('button', { type: 'submit', class: 'primary' }, 'حفظ العتبات'));
}

/* ----- محرّر الهندسة: ETag / If-Match كما في SmartRoutes ----- */
const geoDirty = (c) => !!c.geo && JSON.stringify(c.geo) !== c.geoBase;
function setGeo(c, data, etag) {
  c.geo = { zones: data.zones || [], lines: data.lines || [], masks: data.masks || [] };
  c.geoBase = JSON.stringify(c.geo); c.geoEtag = etag; c.geoStale = false;
  if (c === S.cur) { renderGeo(); ovDirty(); }
}
async function loadGeometry(c, quiet) {
  const r = await safe(api('GET', cp(c, '/smart/geometry'), { full: true }));
  if (!got(r)) return;
  setGeo(c, r.data, r.h.get('ETag'));
  if (!quiet && c === S.cur) gdCancel();
}
function geoRemote(c, by) {   // حدث geo من مستخدم آخر (أو hello بمراجعة مختلفة)
  if (!can('smartView')) return;
  if (geoDirty(c)) { c.geoStale = true; renderGeo(); }
  else loadGeometry(c, true).then(() => { if (by && by !== S.me.userName) toast('تم تحديث المناطق والخطوط بواسطة ' + by); });
}
function renderGeo() {
  const c = S.cur, ul = $('#geolist');
  if (!c || !c.geo) return ul.replaceChildren();
  const cfg = can('smartConfigure'), g = c.geo, dirty = geoDirty(c);
  const row = (txt, arr, i) => h('li', null, h('span', { class: 'grow' }, txt), btn('✕', () => { arr.splice(i, 1); renderGeo(); ovDirty(); }, { disabled: !cfg, 'aria-label': 'حذف' }));
  const rows = [...g.zones.map((z, i) => row('منطقة: ' + z.name + ' (' + (SEV[z.severity] || z.severity) + '، ' + z.points.length + ' نقطة)', g.zones, i)),
    ...g.lines.map((l, i) => row('خط: ' + l.name, g.lines, i)), ...g.masks.map((m, i) => row('قناع إخفاء #' + (i + 1) + ' (' + m.length + ' نقطة)', g.masks, i))];
  ul.replaceChildren(...(rows.length ? rows : [h('li', { class: 'muted' }, 'لا توجد مناطق أو خطوط.')]));
  $('#geostale').hidden = !c.geoStale;
  $('#g-save').disabled = !cfg || !dirty; $('#g-rev').disabled = !dirty && !c.geoStale;
  $$('[data-g]').forEach((b) => { b.disabled = !cfg; });
}
function gdStart(kind) {
  const c = S.cur; if (!c || !c.geo) return;
  const lim = { zone: [c.geo.zones, 32], line: [c.geo.lines, 32], mask: [c.geo.masks, 16] }[kind];
  if (lim[0].length >= lim[1]) return toast('بلغت الحد الأقصى لهذا النوع (' + lim[1] + ').', 'err');
  if (kind !== 'mask' && !NAME_RE.test($('#g-name').value.trim())) { $('#g-name').focus(); return toast('أدخل اسماً صالحاً (1–32 حرفاً: حروف وأرقام ومسافة و _ - . ( )).', 'err'); }
  setMode(kind); gd.kind = kind; gd.pts = []; $('#geobar').hidden = false; $('#stage').scrollIntoView({ block: 'nearest' });
}
function gdCancel() { if (gd.kind) setMode('view'); gd.kind = null; gd.pts = []; $('#geobar').hidden = true; }
function gdPoint(p) {
  if (gd.kind === 'line') { gd.pts.push(p); if (gd.pts.length === 2) gdCommit(); }
  else if (gd.pts.length >= 3 && Math.hypot(p.x - gd.pts[0].x, p.y - gd.pts[0].y) < 0.04) gdCommit();
  else if (gd.pts.length >= 64) toast('الحد الأقصى 64 نقطة.', 'err');
  else gd.pts.push(p);
  ovDirty();
}
function gdCommit() {
  const c = S.cur, name = $('#g-name').value.trim(), k = gd.kind;
  if (k !== 'mask' && !NAME_RE.test(name)) return toast('الاسم غير صالح.', 'err');
  if (k !== 'line' && gd.pts.length < 3) return toast('المضلّع يحتاج 3 نقاط على الأقل.', 'err');
  const pts = gd.pts.map((p) => ({ x: p.x, y: p.y }));
  if (k === 'line') c.geo.lines.push({ name, direction: $('#g-dir').value, a: pts[0], b: pts[1] });
  else if (k === 'zone') c.geo.zones.push({ name, severity: $('#g-sev').value, points: pts });
  else c.geo.masks.push(pts);
  gdCancel(); renderGeo();
  const mm = k !== 'mask' && /^(.*?)(\d+)$/.exec(name);
  if (mm) $('#g-name').value = mm[1] + (+mm[2] + 1);
}
async function saveGeometry() {
  const c = S.cur;
  try {
    const r = await api('PUT', cp(c, '/smart/geometry'), { body: c.geo, headers: { 'If-Match': c.geoEtag }, full: true });
    setGeo(c, r.data, r.h.get('ETag')); toast('تم حفظ المناطق والخطوط', 'ok');
  } catch (e) {
    if (e.handled) return;
    const lost = e.code === 'precondition_failed';
    toast(errText(e) + (lost ? ' — حُمّلت النسخة الأحدث، أعد تطبيق تعديلاتك.' : ''), 'err');
    if (lost) loadGeometry(c, true);   // أي خطأ آخر يُبقي المسودة
  }
}
function initSmart() {
  const sv = (id, key) => $(id).addEventListener('change', (e) => putState({ [key]: e.target.checked }));
  sv('#s-mot', 'motionEnabled'); sv('#s-beh', 'behaviorEnabled'); sv('#s-trk', 'autoTrackEnabled');
  $('#s-learn').addEventListener('change', (e) => putState(e.target.value === 'auto' ? { autoAnomalyLearning: true } : { forceAnomalyLearning: e.target.value === 'on' }));
  $('#s-rel').addEventListener('click', () => safe(api('DELETE', cp(S.cur, '/smart/target')), 'تم إلغاء قفل الهدف'));
  $('#s-save').addEventListener('click', async () => {
    if (await ask({ title: 'حفظ نموذج الشذوذ', text: 'حفظ نموذج الشذوذ الحالي على الخادم؟', ok: 'حفظ' })) safe(api('POST', cp(S.cur, '/smart/anomaly/save')), true);
  });
  $('#behf').addEventListener('submit', async (e) => {
    e.preventDefault();
    const body = {};
    for (const [k, , , , int] of BEH) body[k] = int ? parseInt($('#b-' + k).value, 10) : parseFloat($('#b-' + k).value);
    if (got(await safe(api('PUT', cp(S.cur, '/smart/behavior'), { body }), 'تم حفظ عتبات السلوك'))) loadBehavior(S.cur);
  });
  $$('[data-g]').forEach((b) => b.addEventListener('click', () => gdStart(b.dataset.g)));
  $('#g-fin').addEventListener('click', () => { if (gd.kind === 'line') toast('الخط يحتاج نقطتين.', 'err'); else gdCommit(); });
  $('#g-undo').addEventListener('click', () => { gd.pts.pop(); ovDirty(); });
  $('#g-can').addEventListener('click', gdCancel);
  $('#g-save').addEventListener('click', saveGeometry);
  $('#g-rev').addEventListener('click', () => { gdCancel(); loadGeometry(S.cur); });
  $('#g-name').value = 'منطقة 1';
}

/* ===== الإنذارات ===== */
let alT = 0, actx = null, lastBeep = 0;
const camName = (id) => { const c = S.cams.get(id); return c ? c.d.displayName || id : id; };
function addAlarm(a, live) {
  if (!a || !a.id) return;
  const old = S.alarms.get(a.id);
  if (old) Object.assign(old, a); else S.alarms.set(a.id, a);
  if (S.alarms.size > 400) [...S.alarms.values()].sort((x, y) => x.seq - y.seq).slice(0, S.alarms.size - 300).forEach((k) => S.alarms.delete(k.id));
  if (!old && live && !a.acknowledged) notifyAlarm(a);
  if (!alT) alT = setTimeout(() => { alT = 0; renderAlarms(); }, 120);
}
function renderAlarms() {
  let items = [...S.alarms.values()].sort((a, b) => b.seq - a.seq);
  const unacked = items.filter((a) => !a.acknowledged).length, n = $('#alm-n');
  n.hidden = !unacked; n.textContent = unacked > 99 ? '99+' : unacked;
  if ($('#a-un').checked) items = items.filter((a) => !a.acknowledged);
  if ($('#a-cur').checked && S.cur) items = items.filter((a) => a.cameraId === S.cur.id);
  $('#allist').replaceChildren(...items.slice(0, 150).map((a) => h('li', { class: 'al sev-' + a.severity + (a.acknowledged ? ' acked' : '') },
    h('span', { class: 'sev' }, SEV[a.severity] || a.severity), h('b', null, KIND[a.kind] || a.kind),
    btn(camName(a.cameraId), () => selectCam(a.cameraId), { class: 'lnk' }), h('time', { class: 'ltr muted' }, fmtDT(parseUtc(a.timeUtc))),
    h('span', { class: 'grow' }, a.message),
    a.acknowledged ? h('span', { class: 'muted' }, 'أُقِرّ', a.ackedBy && [' بواسطة ', h('bdi', null, a.ackedBy)])
      : can('ackAlarms') && btn('إقرار', () => ackAlarm(a)))));
  $('#alempty').hidden = items.length > 0;
}
async function ackAlarm(a) {
  if (await safe(api('POST', '/alarms/' + encodeURIComponent(a.id) + '/ack'), true)) { a.acknowledged = true; a.ackedBy = S.me.userName; renderAlarms(); }
}
async function loadBacklog() {
  if (!can('smartView')) return;
  S.sync++; banner();
  const r = await safe(api('GET', '/alarms?limit=200'));
  S.sync--; banner();
  if (got(r)) { r.items.forEach((a) => addAlarm(a, false)); renderAlarms(); }
}
function initAudio() {   // يُستدعى من إيماءة مستخدم فقط
  if (actx || !S.prefs.snd) return;
  try { actx = new (window.AudioContext || window.webkitAudioContext)(); if (actx.state === 'suspended') actx.resume().catch(() => {}); } catch (e) { actx = null; }
}
function beep(crit) {
  if (!S.prefs.snd || !actx || Date.now() - lastBeep < 1500) return;
  lastBeep = Date.now();
  (crit ? [0, 0.22] : [0]).forEach((dt) => {
    const o = actx.createOscillator(), g = actx.createGain(), t = actx.currentTime + dt;
    o.type = 'square'; o.frequency.value = crit ? 960 : 700;
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.15, t + 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
    o.connect(g); g.connect(actx.destination); o.start(t); o.stop(t + 0.2);
  });
}
function notifyAlarm(a) {
  if (a.severity === 'info') return;
  const crit = a.severity === 'critical', txt = 'إنذار ' + (SEV[a.severity] || '') + ': ' + (KIND[a.kind] || a.kind) + ' — ' + camName(a.cameraId);
  toast(txt, crit ? 'err' : '');
  if (crit) $('#sr').textContent = txt + ' ' + a.message;
  beep(crit);
  if (S.prefs.ntf && document.hidden && 'Notification' in window && Notification.permission === 'granted') {
    try { new Notification(txt, { body: a.message, tag: a.id, lang: 'ar', dir: 'rtl', silent: true }); } catch (e) { /* غير مدعوم */ }
  }
}
function initAlarms() {
  const granted = 'Notification' in window && Notification.permission === 'granted';
  $('#a-snd').checked = S.prefs.snd; $('#a-ntf').checked = S.prefs.ntf && granted;
  $('#a-snd').addEventListener('change', (e) => { S.prefs.snd = e.target.checked; savePrefs(); if (e.target.checked) { initAudio(); beep(false); } });
  $('#a-ntf').addEventListener('change', async (e) => {
    if (e.target.checked) {
      const p = !('Notification' in window) ? 'denied' : Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission();
      if (p !== 'granted') { e.target.checked = false; toast('لم يُمنح إذن الإشعارات.', 'err'); }
    }
    S.prefs.ntf = e.target.checked; savePrefs();
  });
  $('#a-un').addEventListener('change', renderAlarms); $('#a-cur').addEventListener('change', renderAlarms);
}

/* ===== الإدارة: الجلسات والتدقيق ===== */
async function loadSessions() {
  const r = await safe(api('GET', '/auth/sessions'));
  if (!got(r)) return;
  $('#sslist').replaceChildren(...r.items.map((s) => h('li', null,
    h('b', null, s.client || 'عميل غير معروف'), s.current && h('span', { class: 'chip' }, 'الجلسة الحالية'), h('span', { class: 'ltr muted' }, s.ipMasked),
    h('span', { class: 'grow muted' }, 'بدأت ', h('bdi', null, fmtDT(parseUtc(s.createdUtc))), ' · آخر نشاط ', h('bdi', null, fmtDT(parseUtc(s.lastActivityUtc)))),
    btn('إنهاء', async () => {
      if (!(await ask({ title: 'إنهاء جلسة', text: s.current ? 'ستنتهي جلستك الحالية وتخرج من النظام.' : 'إنهاء هذه الجلسة؟', ok: 'إنهاء' }))) return;
      if (await safe(api('DELETE', '/auth/sessions/' + encodeURIComponent(s.sessionRef)), true)) { if (s.current) endSession('تم إنهاء الجلسة.'); else loadSessions(); }
    }))));
}
let auBefore = null;
async function loadAudit(more) {
  const r = await safe(api('GET', '/audit?limit=50' + (more && auBefore ? '&beforeSeq=' + auBefore : '')));
  if (!got(r)) return;
  const rows = r.items.map((e) => h('li', null, h('b', null, e.action), h('span', { class: e.outcome === 'OK' ? 'ok' : 'bad' }, e.outcome),
    h('time', { class: 'ltr muted' }, fmtDT(parseUtc(e.timeUtc))),
    h('span', { class: 'grow' }, h('bdi', null, e.user), ' ← ', h('bdi', null, e.target), e.details && [h('br'), h('span', { class: 'muted' }, e.details)])));
  const ul = $('#aulist');
  if (more) ul.append(...rows); else ul.replaceChildren(...rows);
  auBefore = r.nextBeforeSeq || null; $('#au-m').hidden = !auBefore;
  if (!ul.children.length) ul.append(h('li', { class: 'muted' }, 'لا توجد سجلات.'));
}
async function verifyAudit() {
  const el = $('#au-vr'); el.className = 'hint'; el.textContent = 'جارٍ التحقق…';
  const r = await safe(api('GET', '/audit/verify', { timeout: 65000 }));
  if (!got(r)) { el.textContent = 'تعذّر التحقق.'; return; }
  el.className = 'hint ' + (r.isValid ? 'ok' : 'bad');
  el.replaceChildren(r.isValid ? 'السلسلة سليمة — فُحص ' + r.entriesChecked + ' سجلاً. ' : 'اكتُشف تلاعب عند السجل رقم ' + r.firstBadSeq + ' (فُحص ' + r.entriesChecked + '). ',
    h('span', { class: 'ltr', title: r.lastHash }, 'hash: ' + String(r.lastHash || '').slice(0, 16)));
}

/* ===== التبويبات ===== */
const TABS = ['ctl', 'img', 'smt', 'alm', 'adm'];
const tabOk = (t) => (t === 'img' ? can('viewLive') : t === 'smt' || t === 'alm' ? can('smartView') : true);
function showTab(t) {
  if (!tabOk(t)) t = 'ctl';
  S.tab = t;
  for (const x of TABS) { const on = x === t, b = $('#tb-' + x); b.setAttribute('aria-selected', String(on)); b.tabIndex = on ? 0 : -1; $('#p-' + x).hidden = !on; }
  const c = S.cur, L = c.loaded;
  if (t === 'img' && !L.img) { L.img = 1; loadImage(c); }
  if (t === 'smt' && !L.smt) { L.smt = 1; renderSmart(); loadBehavior(c); renderGeo(); }
  if (t === 'alm') renderAlarms();
  if (t === 'adm' && !S.adm) { S.adm = true; loadSessions(); $('#audsec').hidden = !can('viewAudit'); if (can('viewAudit')) loadAudit(false); }
  if (t !== 'smt') gdCancel();
}
function initTabs() {
  TABS.forEach((t) => $('#tb-' + t).addEventListener('click', () => showTab(t)));
  $('.tabs').addEventListener('keydown', (e) => {
    const vis = TABS.filter((t) => !$('#tb-' + t).hidden);
    let i = vis.indexOf(S.tab);
    if (e.key === 'ArrowLeft') i++; else if (e.key === 'ArrowRight') i--; else if (e.key === 'Home') i = 0; else if (e.key === 'End') i = vis.length - 1; else return;
    e.preventDefault(); const t = vis[(i + vis.length) % vis.length]; showTab(t); $('#tb-' + t).focus();
  });
  $('#ss-r').addEventListener('click', loadSessions);
  $('#au-r').addEventListener('click', () => loadAudit(false));
  $('#au-m').addEventListener('click', () => loadAudit(true));
  $('#au-v').addEventListener('click', verifyAudit);
}

/* ===== الدخول ===== */
let lockT = 0;
function loginError(e) {
  const el = $('#lerr'), b = $('#lbtn');
  let m = errText(e);
  if (e.code === 'bad_request') m = e.field === 'totp' ? 'رمز التحقق يجب أن يكون 6 أرقام.' : 'تحقق من اسم المستخدم وكلمة المرور.';
  if (e.code === 'invalid_credentials') m += '. المحاولات الفاشلة المتكررة تقفل الدخول مؤقتاً.';
  if (e.status === 423) m = 'الحساب مقفل مؤقتاً.';
  if ((e.status === 429 || e.status === 423) && e.retry > 0) {   // Retry-After
    let left = e.retry; b.disabled = true;
    const upd = () => { el.textContent = (e.status === 423 ? 'الحساب مقفل مؤقتاً.' : 'محاولات كثيرة.') + ' أعد المحاولة بعد ' + left + ' ث'; };
    upd(); clearInterval(lockT);
    lockT = setInterval(() => { if (--left <= 0) { clearInterval(lockT); b.disabled = false; el.textContent = ''; } else upd(); }, 1000);
    return;
  }
  el.textContent = m;
}
async function doLogin(ev) {
  ev.preventDefault();
  const u = $('#lu').value.trim(), p = $('#lp').value, t = $('#lt').value.trim(), el = $('#lerr'), b = $('#lbtn');
  el.textContent = '';
  if (!u || !p) { el.textContent = 'أدخل اسم المستخدم وكلمة المرور.'; return; }
  if ((t || S.mfa) && !/^\d{6}$/.test(t)) { el.textContent = 'أدخل رمز التحقق (6 أرقام).'; $('#lt').focus(); return; }
  b.disabled = true;
  try {
    const r = await api('POST', '/auth/login', { anon: true, body: { userName: u, password: p, totp: t || undefined, client: 'web-console' } });
    $('#lp').value = $('#lt').value = '';
    S.token = r.accessToken; applySession(r.session, r.idleTimeoutSeconds);
    await enter();
    b.disabled = false;
  } catch (e) {
    $('#lp').value = $('#lt').value = '';
    if (S.token) { if (!e.handled) endSession(errText(e)); } else loginError(e);
    if (!(e.retry > 0 && (e.status === 429 || e.status === 423))) b.disabled = false;
  }
}
async function enter() {
  const list = await api('GET', '/cameras');
  if (!list.items.length) throw new ApiError(0, 'no_cameras');
  $('#login').hidden = true; $('#app').hidden = false;
  S.lastAct = Date.now(); S.tab = 'ctl'; S.cur = null;
  $('#who').textContent = S.me.userName; $('#role').textContent = ROLE[S.me.role] || S.me.role;
  TABS.forEach((t) => { $('#tb-' + t).hidden = !tabOk(t); });
  S.cams.clear();
  for (const d of list.items) S.cams.set(d.id, newCam(d));
  tierOpts(); sizeOv(); renderAlarms(); tick();
  S.timers.push(setInterval(tick, 1000), setInterval(keepAlive, 60000));
  for (const c of S.cams.values()) sseStart(c);
  selectCam(list.items[0].id);
  loadBacklog();
}

function init() {
  loadPrefs();
  $('#lf').addEventListener('submit', doLogin);
  $('#lo').addEventListener('click', logout);
  $('#ext').addEventListener('click', extend);
  initStage(); initPad(); initKeys(); initCtl(); initSmart(); initAlarms(); initTabs();
  api('GET', '/server', { anon: true, timeout: 6000 }).then((r) => {   // mfa=false ⇒ الرمز اختياري
    S.mfa = r.mfa !== false; $('#lt-l').textContent = S.mfa ? 'رمز التحقق (6 أرقام)' : 'رمز التحقق (اختياري)';
  }).catch((e) => { $('#lerr').textContent = errText(e); });
  $('#lu').focus();
}
init();
})();
