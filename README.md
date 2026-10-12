# منصة التحكم بكاميرات PTZ — Hikvision

منصة تحكم أمنية بلغة **Visual Basic (.NET 8)** لكاميرتين مستقلتين:

| الطراز | الملف التعريفي | نافذة التحكم |
|---|---|---|
| DS-2SE7C432MW-AEB | `CameraProfiles.Ds2Se7c432mwAeb` | `FrmDs2Se7c432mw` |
| DS-2DE7232IW-AE | `CameraProfiles.Ds2De7232iwAe` | `FrmDs2De7232iw` |

الاتصال يتم عبر بروتوكول **ISAPI الرسمي** من Hikvision (HTTPS + Digest) — لا مكتبات طرف ثالث غير معتمدة.

## البنية

```
src/Ptz.Core          النواة: النماذج، ISAPI، المتحكمات، الذكاء الاصطناعي، الأمان، الإنذارات (net8.0، متعددة المنصات)
src/Ptz.Presentation  المقدِّمات (MVP) وعقود الواجهات — بلا أي اعتماد على WinForms ← قابلة للاختبار
src/Ptz.App           واجهة WinForms: Designer منفصل عن الكود، لوحات UserControl وحيدة الوظيفة (net8.0-windows)
tests/Ptz.Tests       120 اختبار وحدة وتكامل (xUnit)
build/                فحص تجميع الواجهة على Linux/CI
docs/TECHNICAL_REPORT.md   التقرير التقني الشامل
```

فصل التصميم عن الوظائف: كل نافذة/لوحة = `X.Designer.vb` (تصميم فقط) + `X.vb` (ربط أحداث بلا منطق)،
والمنطق كله في `Ptz.Presentation` و`Ptz.Core`.

## البناء والاختبار

```bash
# أي منصة (النواة + المقدِّمات + الاختبارات + فحص تجميع الواجهة)
dotnet build HbashPTZ.CrossPlatform.slnf
dotnet test  tests/Ptz.Tests/Ptz.Tests.vbproj

# Windows (التطبيق الفعلي)
dotnet build HbashPTZ.sln -c Release
dotnet run --project src/Ptz.App
```

## أول تشغيل

لا توجد كلمات مرور افتراضية. عند أول تشغيل تُنشئ حساب المدير (سياسة: 12 حرفاً+، كبير/صغير/رقم/رمز)، ثم من
الشاشة الرئيسية تُدخل عنوان كل كاميرا وبياناتها، وتضغط «قراءة وتثبيت شهادة الكاميرا» (تثبيت البصمة)، ثم «فتح لوحة التحكم».
البيانات تُحفظ في `%LocalAppData%\PtzPlatform` (كلمات المرور مشفّرة AES-256-GCM، والمفتاح محميّ بـ DPAPI).

## ما تم التحقق منه وما لم يُتحقق منه

انظر القسم 9 من [التقرير التقني](docs/TECHNICAL_REPORT.md): الشيفرة مبنية ومُختبرة آلياً على Linux
(120 اختباراً ناجحاً، منها تكامل مع كاميرا ISAPI وهمية)، وواجهة WinForms **مُجمَّعة** لكن **لم تُشغَّل بصرياً**،
ولم تُجرَّب على كاميرات فعلية بعد — يلزم اختبار قبول ميداني قبل الإنتاج.
