// يولّد خطابات اختبار عربية بقيم صحيحة معروفة (ground truth) ويرسمها بـ Chromium بدقة مسح 300 نقطة/بوصة.
// node make-corpus.mjs <مجلد الإخراج> [العدد]
import { writeFile, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const out = process.argv[2] || 'corpus'; const count = +(process.argv[3] || 24);
await mkdir(out, { recursive: true });
let seed = +(process.env.CORPUS_SEED || 20261025); const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
const pick = a => a[Math.floor(rnd() * a.length)];
const east = s => s.replace(/[0-9]/g, d => '٠١٢٣٤٥٦٧٨٩'[d]);
const orgs = ['وزارة المالية', 'وزارة الصحة', 'أمانة منطقة الرياض', 'هيئة الحكومة الرقمية', 'وزارة الموارد البشرية والتنمية الاجتماعية', 'ديوان المراقبة العامة', 'الإدارة العامة للشؤون الإدارية', 'جامعة الملك سعود', 'المؤسسة العامة للتدريب التقني والمهني'];
const subjects = ['طلب بيانات المشاريع المتعثرة', 'تجديد تصريح المبنى الإداري', 'ترشيح موظفين لبرنامج القيادة', 'ملاحظات المراجعة المالية', 'تحديث بيانات المنشآت الصحية', 'اعتماد الميزانية التشغيلية', 'صرف مستحقات المقاولين', 'خطة التحول الرقمي للمرحلة الثانية', 'إسناد أعمال صيانة الأنظمة', 'تشكيل لجنة مراجعة الإجراءات', 'طلب تمديد مدة العقد', 'استيضاح حول المسميات الوظيفية'];
const months = ['محرم', 'صفر', 'ربيع الأول', 'ربيع الآخر', 'جمادى الأولى', 'جمادى الآخرة', 'رجب', 'شعبان', 'رمضان', 'شوال', 'ذو القعدة', 'ذو الحجة'];
const pad = n => String(n).padStart(2, '0');
function hDate() { return { y: pick([1446, 1447, 1448]), m: 1 + Math.floor(rnd() * 12), d: 1 + Math.floor(rnd() * 28) }; }
function fmtDate(h, style) {
  if (style === 'text') return `${h.d} ${months[h.m - 1]} ${h.y}هـ`;
  const s = `${h.y}/${pad(h.m)}/${pad(h.d)}هـ`;
  return style === 'east' ? east(s) : s;
}
const canon = h => `H${h.y}-${pad(h.m)}-${pad(h.d)}`;
function number() {
  const k = rnd();
  if (k < .55) return String(1000 + Math.floor(rnd() * 98999));
  if (k < .8) return `${100 + Math.floor(rnd() * 900)}/${pick(['ص', 'م', 'ت', 'س'])}`;
  return `${Math.floor(rnd() * 90) + 10}/${Math.floor(rnd() * 9) + 1}/${pick([1446, 1447, 1448])}`;
}
const keyOf = n => n.replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d)).replace(/\s+/g, '').replace(/-/g, '/');
const fonts = ['"Noto Naskh Arabic","DejaVu Sans",serif', '"Noto Sans Arabic","DejaVu Sans",sans-serif', 'Tahoma,"DejaVu Sans",sans-serif'];
const truth = [];
const b = await chromium.launch({ executablePath: process.env.CHROMIUM || '/opt/pw-browsers/chromium', args: ['--no-sandbox'] });
const p = await b.newPage({ viewport: { width: 1240, height: 1754 }, deviceScaleFactor: 2 });
for (let i = 1; i <= count; i++) {
  const org = pick(orgs), subject = pick(subjects), no = number(), date = hDate();
  const dateStyle = pick(['num', 'num', 'east', 'text']);
  const eastNo = dateStyle === 'east' && /^\d+$/.test(no);
  const refs = [];
  const nRefs = rnd() < .75 ? 1 + (rnd() < .3 ? 1 : 0) : 0;
  for (let r = 0; r < nRefs; r++) refs.push({ number: number(), date: hDate(), owner: pick(['خطابكم', 'خطاب سعادتكم', 'خطابنا']) });
  const days = rnd() < .6 ? pick([5, 7, 10, 14, 15, 30]) : 0;
  const lead = refs.length ? refs.map((r, j) => `${j === 0 ? pick(['إشارة إلى ', 'بالإشارة إلى ', 'رداً على ', 'إلحاقاً ل']) : 'وعطفاً على '}${r.owner} رقم ${r.number} وتاريخ ${fmtDate(r.date, dateStyle === 'text' ? 'num' : dateStyle)}`).join('، ') + ` بشأن ${subject}، ` : '';
  const ask = days ? `نأمل التكرم بالإفادة خلال (${days}) ${days <= 10 ? 'أيام' : 'يوماً'} من تاريخه.` : 'نأمل الاطلاع والإحاطة.';
  const body = `${lead}نفيدكم بأنه تمت دراسة الموضوع من قبل الإدارة المختصة، وقد تبين الحاجة إلى استكمال بعض المتطلبات النظامية والمستندات الداعمة. ${ask}`;
  const headerNo = eastNo ? east(no) : no;
  const html = `<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
  body{font-family:${pick(fonts)};font-size:${pick([18, 19, 20, 21, 22])}px;line-height:1.95;margin:70px 80px;color:#111;background:#fff}
  .h{display:flex;justify-content:space-between;align-items:flex-start;border-bottom:2px solid #222;padding-bottom:12px;margin-bottom:34px}
  .h b{font-size:1.3em}.meta{line-height:1.8}p{margin:14px 0}</style></head><body>
  <div class="h"><b>${org}</b><div class="meta">الرقم: ${headerNo}<br>التاريخ: ${fmtDate(date, dateStyle)}<br>المرفقات: ${pick(['لا يوجد', '1', 'ملف'])}</div></div>
  <p>الموضوع: ${subject}</p><p>${pick(['سعادة', 'معالي', 'المكرم'])} ${pick(['مدير عام الشؤون الإدارية', 'وكيل الوزارة للخدمات المشتركة', 'رئيس الهيئة'])} المحترم</p>
  <p>السلام عليكم ورحمة الله وبركاته</p><p>${body}</p><p>وتقبلوا تحياتنا،</p></body></html>`;
  await p.setContent(html);
  const name = `L${String(i).padStart(3, '0')}`;
  await p.screenshot({ path: `${out}/${name}.png`, fullPage: false });
  truth.push({ name, number: keyOf(no), date: canon(date), subject, refs: refs.map(r => ({ number: keyOf(r.number), date: canon(r.date) })), deadlineDays: days });
}
await b.close();
await writeFile(`${out}/truth.json`, JSON.stringify(truth, null, 1));
console.log('letters:', truth.length);
