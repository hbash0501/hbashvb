// يملأ منصة تعمل بأرشيف تجريبي واقعي عبر واجهتها البرمجية (لبيئات التطوير والعرض فقط).
// node seed.mjs http://127.0.0.1:5280 [مجلد ملفات الاختبار]
import { readFile } from 'node:fs/promises';
const base = process.argv[2] || 'http://127.0.0.1:5280';
const fixtures = process.argv[3];
let cookie = '';
async function call(path, { method = 'GET', body, form } = {}) {
  const headers = { 'X-Requested-With': 'basira', cookie };
  if (body) headers['Content-Type'] = 'application/json';
  const r = await fetch(base + '/api' + path, { method, headers, body: form || (body ? JSON.stringify(body) : undefined) });
  const set = r.headers.get('set-cookie'); if (set) cookie = set.split(';')[0];
  const data = (r.headers.get('content-type') || '').includes('json') ? await r.json() : null;
  if (!r.ok) throw new Error(`${path}: ${r.status} ${JSON.stringify(data)}`);
  return data;
}
const ago = n => { const d = new Date(); d.setDate(d.getDate() - n); return d; };
const hijri = d => { const p = Object.fromEntries(new Intl.DateTimeFormat('en-u-ca-islamic-umalqura-nu-latn', { year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(d).map(x => [x.type, x.value])); return `${p.year.replace(/\D/g, '')}/${p.month}/${p.day}هـ`; };
const letter = ({ org, no, date, subject, to, body }) => `${org}\nالرقم: ${no}\nالتاريخ: ${hijri(date)}\nالموضوع: ${subject}\n\n${to} المحترم\nالسلام عليكم ورحمة الله وبركاته\n\n${body}\n\nوتقبلوا تحياتنا`;
const ours = 'الإدارة العامة للشؤون الإدارية';
async function upload(name, text, direction, category, confidentiality = 'عادي') {
  const form = new FormData();
  form.append('files', new Blob([text], { type: 'text/plain' }), name);
  form.append('direction', direction); form.append('category', category); form.append('confidentiality', confidentiality);
  return (await call('/inbox', { method: 'POST', form }))[0];
}

const setup = await call('/setup');
if (setup.needsSetup) await call('/setup', { method: 'POST', body: { fullName: 'سعيد العتيبي', userName: 'admin', password: 'Basira-Owner-2026!', organization: ours } });
else await call('/login', { method: 'POST', body: { userName: 'admin', password: 'Basira-Owner-2026!' } });
for (const [u, n, r] of [['sara', 'سارة القحطاني', 'مدير الأرشيف'], ['khalid', 'خالد الدوسري', 'موظف أرشفة'], ['reem', 'ريم الشهري', 'مراجع'], ['viewer', 'عبدالله المطيري', 'قارئ']])
  await call('/admin/users', { method: 'POST', body: { userName: u, fullName: n, role: r, password: 'Basira-Team-2026!' } }).catch(() => {});

const L = [
  // معاملة: بيانات الموظفين (طلب ← رد ← تعقيب ← رد)
  ['hr-1.txt', 'وارد', 'موارد بشرية', { org: 'وزارة الموارد البشرية والتنمية الاجتماعية', no: '9911', date: ago(46), subject: 'طلب بيانات الموظفين المتعاقدين', to: 'سعادة مدير عام الشؤون الإدارية', body: 'نأمل التكرم بتزويدنا ببيانات الموظفين المتعاقدين لديكم وفق النموذج المرفق خلال (10) أيام عمل من تاريخه، لاستكمال إجراءات التوطين.' }],
  ['hr-2.txt', 'صادر', 'موارد بشرية', { org: ours, no: '120', date: ago(38), subject: 'الرد على طلب بيانات الموظفين المتعاقدين', to: 'سعادة وكيل الوزارة للتوطين', body: `إشارة إلى خطاب سعادتكم رقم 9911 وتاريخ ${hijri(ago(46))} بشأن طلب بيانات الموظفين المتعاقدين، نرفق لكم البيانات المطلوبة وفق النموذج المعتمد.` }],
  ['hr-3.txt', 'وارد', 'موارد بشرية', { org: 'وزارة الموارد البشرية والتنمية الاجتماعية', no: '10402', date: ago(21), subject: 'استيضاح حول بيانات الموظفين', to: 'سعادة مدير عام الشؤون الإدارية', body: `إلحاقاً لخطابنا رقم 9911 وإشارة إلى خطابكم رقم 120 وتاريخ ${hijri(ago(38))}، نأمل استيضاح المسميات الوظيفية لعدد من المتعاقدين خلال أسبوعين.` }],
  // معاملة: تصريح الأمانة بلا رد
  ['permit.txt', 'وارد', 'مراسلات', { org: 'أمانة المنطقة', no: '2025-778', date: ago(19), subject: 'تجديد تصريح المبنى الإداري', to: 'سعادة مدير الإدارة العامة للشؤون الإدارية', body: 'نفيدكم بقرب انتهاء تصريح المبنى الإداري، ونأمل استكمال متطلبات التجديد والرد علينا في موعد أقصاه ' + hijri(ago(-6)) + '.' }],
  // معاملة: الصيانة (عقد ← خطاب المقاول ← فاتورة)
  ['maint-1.txt', 'صادر', 'عقود', { org: ours, no: '88', date: ago(60), subject: 'إسناد أعمال صيانة الأنظمة', to: 'المكرم مدير شركة البنية المتقدمة', body: 'نفيدكم بإسناد أعمال صيانة الأنظمة والتجهيزات لشركتكم وفق العقد رقم 4201، على أن تبدأ الأعمال خلال (7) أيام من تاريخه.' }],
  ['maint-2.txt', 'وارد', 'عقود', { org: 'شركة البنية المتقدمة', no: 'ADV-311', date: ago(55), subject: 'تأكيد بدء أعمال الصيانة', to: 'سعادة مدير عام الشؤون الإدارية', body: `إشارة إلى خطابكم رقم 88 وتاريخ ${hijri(ago(60))}، نؤكد بدء فريقنا أعمال الصيانة وفق الجدول الزمني المعتمد.` }],
  ['maint-3.txt', 'وارد', 'مالي', { org: 'شركة البنية المتقدمة', no: 'INV-2026-091', date: ago(9), subject: 'فاتورة أعمال الصيانة للربع الثالث', to: 'سعادة مدير الإدارة المالية', body: 'إلحاقاً لخطابنا رقم ADV-311، نرفق فاتورة أعمال الصيانة للربع الثالث بمبلغ 45,000 ريال، ونأمل صرفها خلال (15) يوماً من تاريخه.' }],
  // خطاب غائب: خطابان يشيران إلى خطابنا 5050 غير المؤرشف
  ['ghost-1.txt', 'وارد', 'مراسلات', { org: 'هيئة الحكومة الرقمية', no: '7720', date: ago(14), subject: 'ملاحظات على خطة التحول الرقمي', to: 'سعادة مدير عام الشؤون الإدارية', body: `إشارة إلى خطابكم رقم 5050 وتاريخ ${hijri(ago(30))} بشأن خطة التحول الرقمي، نرفق ملاحظات الهيئة على المرحلة الأولى.` }],
  ['ghost-2.txt', 'وارد', 'مراسلات', { org: 'هيئة الحكومة الرقمية', no: '7795', date: ago(4), subject: 'متابعة خطة التحول الرقمي', to: 'سعادة مدير عام الشؤون الإدارية', body: `تعقيباً على خطابكم رقم ٥٠٥٠ وتاريخ ${hijri(ago(30))}، نأمل موافاتنا بالجدول الزمني المحدّث خلال خمسة أيام عمل.` }],
  // مستندات مفردة
  ['circular.txt', 'داخلي', 'إداري', { org: ours, no: 'ت/14', date: ago(12), subject: 'تعميم بشأن الدوام في شهر رمضان', to: 'سعادة مديري الإدارات', body: 'بناءً على قرار مجلس الوزراء رقم 315 وتاريخ 1440/05/27هـ، يكون الدوام الرسمي خلال شهر رمضان من الساعة العاشرة صباحاً حتى الثالثة عصراً.' }],
  ['training.txt', 'وارد', 'موارد بشرية', { org: 'معهد الإدارة العامة', no: '3301', date: ago(6), subject: 'ترشيح موظفين لبرنامج القيادة', to: 'سعادة مدير عام الشؤون الإدارية', body: 'نأمل ترشيح ثلاثة من موظفيكم لبرنامج القيادة الإدارية خلال (5) أيام عمل من تاريخه.' }],
  ['secret.txt', 'وارد', 'سري', { org: 'ديوان المراقبة العامة', no: 'س/221', date: ago(25), subject: 'ملاحظات المراجعة المالية', to: 'سعادة مدير عام الشؤون الإدارية', body: 'نرفق لكم ملاحظات المراجعة المالية للعام الماضي، ونأمل الرد عليها خلال ثلاثين يوماً.' }, 'سري'],
];
for (const [name, dir, cat, l, conf] of L) await upload(name, letter(l), dir, cat, conf);
if (fixtures) {
  for (const [f, dir] of [['incoming-letter.png', 'وارد'], ['reply-letter.pdf', 'صادر']]) {
    const form = new FormData();
    form.append('files', new Blob([await readFile(fixtures + '/' + f)]), f); form.append('direction', dir); form.append('category', 'مالي');
    await call('/inbox', { method: 'POST', form });
  }
}
console.log('تم: ' + L.length + ' خطاباً' + (fixtures ? ' + ملفان حقيقيان' : ''));
