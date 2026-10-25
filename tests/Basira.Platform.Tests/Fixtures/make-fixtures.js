// يولّد خطابات اختبار حقيقية (صورة PNG وPDF بطبقة نص) بمتصفح Chromium:
// node make-fixtures.js <مجلد الإخراج>
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const out = process.argv[2] || __dirname;
const page = (body) => `<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
 body{font-family:"Noto Naskh Arabic","Noto Sans Arabic","DejaVu Sans",sans-serif;font-size:22px;line-height:1.9;margin:60px 70px;color:#111;background:#fff}
 .head{display:flex;justify-content:space-between;border-bottom:2px solid #333;padding-bottom:10px;margin-bottom:30px}
 h1{font-size:26px;margin:0} p{margin:14px 0}</style></head><body>${body}</body></html>`;
const incoming = page(`<div class="head"><h1>وزارة المالية</h1><div>الرقم: 4521<br>التاريخ: 1447/03/12هـ</div></div>
<p>الموضوع: طلب بيانات المشاريع المتعثرة</p>
<p>سعادة مدير عام الشؤون الإدارية المحترم</p>
<p>السلام عليكم ورحمة الله وبركاته</p>
<p>نأمل تزويدنا ببيانات المشاريع المتعثرة لديكم خلال (15) يوماً من تاريخه، مع بيان أسباب التعثر والإجراءات المتخذة لمعالجتها.</p>
<p>وتقبلوا تحياتنا</p>`);
const reply = page(`<div class="head"><h1>الإدارة العامة للشؤون الإدارية</h1><div>الرقم: 7781<br>التاريخ: 1447/03/25هـ</div></div>
<p>الموضوع: الرد على طلب بيانات المشاريع المتعثرة</p>
<p>سعادة وكيل وزارة المالية المحترم</p>
<p>السلام عليكم ورحمة الله وبركاته</p>
<p>إشارة إلى خطاب سعادتكم رقم 4521 وتاريخ 1447/03/12هـ بشأن طلب بيانات المشاريع المتعثرة، نرفق لكم البيان المطلوب متضمناً أسباب التعثر والإجراءات المتخذة.</p>
<p>وتقبلوا تحياتنا</p>`);
// خطاب بأرقام هندية (٠-٩): نماذج Tesseract العربية تقرؤها حروفاً لاتينية، ويصلحها قارئ الأرقام في المنصة.
const indic = page(`<div class="head"><h1>ديوان المراقبة العامة</h1><div>الرقم: ٢٢٠٥٢<br>التاريخ: ١٤٤٨/٠٩/١٢هـ</div></div>
<p>الموضوع: ملاحظات المراجعة المالية</p>
<p>المكرم رئيس الهيئة المحترم</p>
<p>السلام عليكم ورحمة الله وبركاته</p>
<p>بالإشارة إلى خطاب سعادتكم رقم ٩٨٢٢٤ وتاريخ ١٤٤٨/٠٥/٠٢هـ بشأن ملاحظات المراجعة المالية، نفيدكم بأنه تمت دراسة الموضوع، ونأمل الإفادة خلال (٣٠) يوماً من تاريخه.</p>
<p>وتقبلوا تحياتنا</p>`);
(async () => {
  const b = await chromium.launch({ executablePath: process.env.CHROMIUM || '/opt/pw-browsers/chromium', args: ['--no-sandbox'] });
  const p = await b.newPage({ viewport: { width: 1100, height: 900 } });
  await p.setContent(incoming); await p.screenshot({ path: out + '/incoming-letter.png', fullPage: true });
  await p.setContent(indic); await p.screenshot({ path: out + '/indic-letter.png', fullPage: true });
  await p.setContent(reply); await p.pdf({ path: out + '/reply-letter.pdf', format: 'A4', printBackground: true });
  await b.close();
})();
