// بصيرة — الهيكل والتوجيه.
import { h, icon, api, mount, state, connectEvents, disconnectEvents, onEvent, nf, toast, can } from './core.js';
import { renderDashboard, renderThreads, renderThread, renderDocs, renderDoc, empty } from './views.js';
import { renderInbox, renderAsk, renderAdmin, renderAuth, openPalette } from './views2.js';
import { renderReview } from './review.js';
import { renderReply } from './reply.js';
import { renderCommitments } from './commitments.js';
import { renderScan } from './scan.js';

const root = document.getElementById('root');
let view, navEls = {}, procBox, awaitingBadge, reviewBadge;

function theme(set) {
  let t = null;
  try { t = set ?? localStorage.getItem('basira-theme'); if (set) localStorage.setItem('basira-theme', set); } catch { /* تخزين غير متاح */ }
  if (t === 'dark' || t === 'light') document.documentElement.dataset.theme = t; else delete document.documentElement.dataset.theme;
}
theme();
const isDark = () => document.documentElement.dataset.theme === 'dark' || (!document.documentElement.dataset.theme && matchMedia('(prefers-color-scheme: dark)').matches);

const SHORT = { '': 'الرئيسية', threads: 'المعاملات', docs: 'المستندات', review: 'التحقق', inbox: 'إدراج', ask: 'اسأل', admin: 'الإدارة' };
const NAV = [['', 'لوحة القيادة', 'home'], ['threads', 'المعاملات', 'thread'], ['docs', 'المستندات', 'docs'], ['commitments', 'الالتزامات', 'flag'], ['review', 'التحقق من القراءة', 'eye'], ['inbox', 'الإدراج', 'upload'], ['ask', 'اسأل بصيرة', 'spark'], ['admin', 'الإدارة', 'sliders']];
const setReviewCount = n => { if (reviewBadge) { reviewBadge.textContent = nf.format(n); reviewBadge.hidden = !n; reviewBadge.title = 'حقول بانتظار التحقق'; } };

function shell() {
  view = h('main', { class: 'content', id: 'view', tabindex: -1 });
  const nav = h('nav', { class: 'nav', 'aria-label': 'التنقل الرئيسي' }, NAV.filter(([k]) => k !== 'inbox' || can('import')).filter(([k]) => k !== 'review' || can('edit')).filter(([k]) => k !== 'admin' || state.me.isAdmin || can('audit') || can('export'))
    .map(([k, label, ic]) => navEls[k] = h('a', { href: '#/' + k }, icon(ic), h('span', { class: 'label-long', text: label }), h('span', { class: 'label-short', text: SHORT[k] }), k === 'threads' ? (awaitingBadge = h('span', { class: 'count', hidden: true })) : k === 'review' ? (reviewBadge = h('span', { class: 'count warn', hidden: true })) : null)));
  procBox = h('div', { class: 'proc', hidden: true });
  const initials = (state.me.fullName || '؟').trim().split(/\s+/).map(w => w[0]).slice(0, 2).join('');
  const themeBtn = h('button', { class: 'btn ghost icon-btn', title: 'تبديل المظهر', 'aria-label': 'تبديل المظهر', onclick: () => { theme(isDark() ? 'light' : 'dark'); mount(themeBtn, icon(isDark() ? 'sun' : 'moon')); } }, icon(isDark() ? 'sun' : 'moon'));
  mount(root, h('div', { class: 'shell' },
    h('aside', { class: 'side' },
      h('a', { class: 'brand', href: '#/', style: 'text-decoration:none;color:inherit' }, h('div', { class: 'logo' }, h('img', { src: '/favicon.svg', alt: '', width: 38, height: 38, style: 'border-radius:11px' })), h('div', {}, h('b', { text: 'بصيرة' }), h('small', { text: state.org || 'منصة الأرشفة الذكية', title: state.version ? 'الإصدار ' + state.version : '' }))),
      nav, h('div', { class: 'spacer' }), procBox,
      h('div', { class: 'who' }, h('div', { class: 'avatar', text: initials }), h('div', { class: 'meta' }, h('b', { text: state.me.fullName }), h('span', { text: state.me.role })),
        h('button', { class: 'btn ghost icon-btn', title: 'خروج', 'aria-label': 'خروج', onclick: logout }, icon('logout')))),
    h('div', { class: 'main' },
      h('header', { class: 'top' },
        h('button', { class: 'search-trigger', onclick: openPalette, 'aria-label': 'بحث سريع' }, icon('search'), h('span', { class: 'st-text', text: 'ابحث في الأرشيف أو انتقل إلى…' }), h('kbd', { class: 'hide-sm', text: 'Ctrl K' })),
        can('import') ? h('a', { class: 'btn primary', href: '#/inbox' }, icon('upload'), h('span', { class: 'hide-sm', text: 'إدراج' })) : null,
        themeBtn),
      view)));
}

async function logout() {
  try { await api('/logout', { method: 'POST' }); } catch { /* تجاهل */ }
  disconnectEvents(); state.me = null; location.hash = ''; boot();
}
window.addEventListener('basira:logout', () => { if (state.me) { state.me = null; disconnectEvents(); boot(); } });

async function route() {
  if (!state.me || !view) return;
  const [path, query] = location.hash.replace(/^#\/?/, '').split('?');
  const params = new URLSearchParams(query || '');
  const [section, arg] = path.split('/').map(decodeURIComponent);
  const key = section === 'doc' || section === 'reply' ? 'docs' : section === 'thread' ? 'threads' : section === 'scan' ? 'inbox' : section || '';
  for (const [k, el] of Object.entries(navEls)) el.setAttribute('aria-current', k === key ? 'page' : 'false');
  mount(view, h('div', { class: 'grid' }, h('div', { class: 'skeleton' }), h('div', { class: 'skeleton', style: 'min-height:240px' })));
  const titles = { '': 'لوحة القيادة', threads: 'المعاملات', thread: 'معاملة', docs: 'المستندات', doc: 'مستند', reply: 'استوديو الرد', commitments: 'الالتزامات', review: 'التحقق من القراءة', inbox: 'الإدراج', scan: 'المسح الضوئي', ask: 'اسأل بصيرة', admin: 'الإدارة' };
  document.title = (titles[section || ''] || 'بصيرة') + ' | بصيرة';
  try {
    if (!section) await renderDashboard(view);
    else if (section === 'threads') await renderThreads(view, params);
    else if (section === 'thread') await renderThread(view, arg);
    else if (section === 'docs') await renderDocs(view, params);
    else if (section === 'doc') await renderDoc(view, arg, setReviewCount);
    else if (section === 'reply') await renderReply(view, arg, params);
    else if (section === 'commitments') await renderCommitments(view, params);
    else if (section === 'review') await renderReview(view, setReviewCount);
    else if (section === 'inbox') await renderInbox(view);
    else if (section === 'scan') await renderScan(view);
    else if (section === 'ask') await renderAsk(view, params);
    else if (section === 'admin') await renderAdmin(view, arg);
    else mount(view, empty('alert', 'الصفحة غير موجودة.'));
  } catch (e) {
    if (e.status !== 401) mount(view, h('div', { class: 'card pad' }, empty('alert', e.message)));
  }
  view.focus({ preventScroll: true });
  window.scrollTo(0, 0);
}
window.addEventListener('hashchange', route);

let refreshTimer;
function refreshBadges() {
  clearTimeout(refreshTimer);
  refreshTimer = setTimeout(async () => {
    try {
      const d = await api('/dashboard');
      if (awaitingBadge) { awaitingBadge.textContent = nf.format(d.awaitingTotal); awaitingBadge.hidden = !d.awaitingTotal; awaitingBadge.title = 'وارد بانتظار الرد'; }
      if (reviewBadge) setReviewCount((await api('/review/count')).pending);
    } catch { /* غير حرج */ }
  }, 600);
}

function wireEvents() {
  connectEvents();
  onEvent(msg => {
    if (msg.type !== 'job') return;
    const running = [...state.jobs.values()].filter(j => j.state === 'queued' || j.state === 'running');
    procBox.hidden = !running.length;
    if (running.length) {
      const j = running[0];
      mount(procBox, h('div', { class: 'row', style: 'justify-content:space-between' }, h('b', { text: 'جارٍ المعالجة' }), h('span', { class: 'muted', text: nf.format(running.length) })),
        h('div', { class: 'muted small', style: 'white-space:nowrap;overflow:hidden;text-overflow:ellipsis', text: `${j.title} — ${j.stage}` }), h('div', { class: 'bar' }, h('i', { style: `width:${Math.max(4, j.progress)}%` })));
    }
    if (msg.payload.state === 'done') { toast(`اكتملت معالجة «${msg.payload.title}»`, { link: '#/doc/' + encodeURIComponent(msg.payload.documentId) }); refreshBadges(); if (!location.hash || location.hash === '#/') softRefresh(); }
    if (msg.payload.state === 'failed') toast(`تعذرت معالجة «${msg.payload.title}»: ${msg.payload.message}`, { error: true });
  });
}
let softTimer;
function softRefresh() { clearTimeout(softTimer); softTimer = setTimeout(() => { if (!document.querySelector('.overlay')) route(); }, 900); }

document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k' && state.me) { e.preventDefault(); openPalette(); }
  else if (e.key === '/' && state.me && !/INPUT|TEXTAREA|SELECT/.test(document.activeElement?.tagName)) { e.preventDefault(); openPalette(); }
});

async function boot() {
  try {
    const me = await api('/me');
    state.me = me.user; state.org = me.organization; state.version = me.version;
  } catch {
    const setup = await api('/setup').catch(() => ({ needsSetup: false }));
    renderAuth(root, setup.needsSetup ? 'setup' : 'login', boot);
    return;
  }
  state.meta = await api('/meta').catch(() => null);
  shell();
  wireEvents();
  refreshBadges();
  await route();
}
boot();
