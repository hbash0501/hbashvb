# بصيرة — منصة الأرشفة الذكية

تطبيق سطح مكتب لأرشفة المستندات وفهرستها والبحث فيها، مبني بلغة VB.NET (Windows Forms) على .NET Framework 4.8، ويدعم استخراج النص (OCR)، والمسح الضوئي، والكاميرا، وطابور معالجة في الخلفية، وبحثاً عربياً بالأدلة، وحزم تصدير.

## المتطلبات

- Windows 10/11 أو Windows Server مناسب، و.NET Framework 4.8.
- للبناء: Visual Studio 2022 (أدوات سطح مكتب VB.NET وحزمة استهداف .NET Framework 4.8).
- لـ OCR: Tesseract مع اللغتين `ara` و`eng`. لملفات PDF المصورة: Poppler أو Ghostscript.

## البناء والتشغيل

1. شغّل `BUILD_AND_VALIDATE_AR.ps1` (يتحقق من الملفات والمراجع ثم يبني نسخة Release).
2. شغّل `RUN_BASIRA.cmd`.
3. في التثبيت الجديد يُنشأ حساب المدير الافتراضي `admin`، ويُلزَم المستخدم بتعيين كلمة مرور جديدة (10 أحرف فأكثر) عند أول دخول. راجع `docs/OPERATIONS_AR.md`.

## الوثائق

| الملف | المحتوى |
|---|---|
| `docs/ANALYSIS_REPORT_AR.md` | تحليل النسخة السابقة ونقاط الضعف والمعالجة |
| `docs/EXECUTION_REPORT_AR.md` | تقرير التنفيذ وحدود التحقق |
| `docs/OPERATIONS_AR.md` | دليل التشغيل والصيانة والنسخ الاحتياطي |
| `docs/ARCHITECTURE_AR.md` | البنية التقنية للمكونات |
| `docs/SCANNER_AND_PROGRESS_AR.md` | المسح الضوئي وإشعارات المعالجة |
| `THIRD_PARTY_NOTICES.md`، `Licenses/` | تراخيص المكتبات الخارجية |

## الاختبارات

مشروع `Tests/Basira.Core.Tests.vbproj` (‏.NET 8) يشغّل فحوص المحرك والبحث والمواعيد والإحالات والطابور وحزم التصدير: `dotnet run --project Tests`.
