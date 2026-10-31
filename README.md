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
src/Ptz.Gateway       بوابة التشغيل عن بُعد (Kestrel): REST/SSE آمنة + كونسول ويب للجوال + أداة إدارة CLI
tests/Ptz.Tests       187 اختبار وحدة وتكامل (xUnit)
tests/Ptz.Gateway.Tests  327 اختبار تكامل للبوابة والعميل البعيد ونقل Hik-Partner Pro (خادم حقيقي، TLS حقيقي، سحابة وهمية)
deploy/               أمثلة نشر: systemd، nftables، Docker، خدمة Windows، إعداد نموذجي
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
dotnet test  tests/Ptz.Gateway.Tests/Ptz.Gateway.Tests.vbproj

# Windows (التطبيق الفعلي)
dotnet build HbashPTZ.sln -c Release
dotnet run --project src/Ptz.App
```

## أول تشغيل

لا توجد كلمات مرور افتراضية. عند أول تشغيل تُنشئ حساب المدير (سياسة: 12 حرفاً+، كبير/صغير/رقم/رمز)، ثم من
الشاشة الرئيسية تُدخل عنوان كل كاميرا وبياناتها، وتضغط «قراءة وتثبيت شهادة الكاميرا» (تثبيت البصمة)، ثم «فتح لوحة التحكم».
البيانات تُحفظ في `%LocalAppData%\PtzPlatform` (كلمات المرور مشفّرة AES-256-GCM، والمفتاح محميّ بـ DPAPI).

## التشغيل عن بُعد

لا يوجد API عام لـ **Hik-Connect** يتيح التحكم من تطبيق خارجي (والبديل ISUP يتطلب SDK وترخيص شريك)، لذلك بُني حل أقوى:
**بوابة** داخل شبكة الكاميرات تملك بياناتها وتشغّل التحليل الذكي 24/7، وتعرض REST/SSE عبر HTTPS (خلف VPN/نفق) مع TOTP وصلاحيات وتدقيق.
العميلان: تطبيق WinForms بـ«وضع البوابة» (زر «اتصال ببوابة بعيدة…» في شاشة الدخول) وكونسول ويب للجوال على `https://<gateway>:8443/`.

**نقل اختياري عبر Hik-Partner Pro** (لمشتركي منصة الشركاء): تصل البوابة إلى الكاميرتين عبر سحابة Hikvision (OpenAPI، «المرور الشفاف» لأوامر ISAPI)
بدل الشبكة المحلية، مع بقاء الصلاحيات وTOTP والتدقيق وإيقاف الأمان في البوابة. حدوده حسب الدليل: **حركة PTZ ومواضع مسبقة، وإنذارات الكاميرا نفسها** (حركة، تسلل، عبور خط، تلاعب، انقطاع) — لا فيديو ولا تحليل ذكي في البوابة.
مبني ومختبَر على سحابة وهمية مبنية من الدليل، **ولم يُجرَّب على السحابة الحقيقية بعد**. انظر [REMOTE.md §5.4](docs/REMOTE.md).
> **ملاحظة تبعيات**: التطبيق المكتبي ما زال بلا تبعيات خارج .NET (عدا ProtectedData). **البوابة وحدها** تضيف `SkiaSharp` (MIT) لمعالجة JPEG على Linux/Windows —
> قرار قابل للمراجعة (الحدود `IFrameDecoder`/`IJpegTranscoder`)، انظر [REMOTE.md §4](docs/REMOTE.md).

التفاصيل الكاملة: نشر، أوامر CLI، الشبكة، الأمان، مرجع الواجهة وقيود معروفة في **[docs/REMOTE.md](docs/REMOTE.md)**.

```bash
dotnet publish src/Ptz.Gateway -c Release -o /opt/ptz-gateway
PtzGateway user add alice operator && PtzGateway totp enroll alice
PtzGateway camera set se7c432mw --host 192.168.1.64 --user svc
PtzGateway cert create --host gateway.lan && PtzGateway serve
```

## ما تم التحقق منه وما لم يُتحقق منه

انظر القسم 9 من [التقرير التقني](docs/TECHNICAL_REPORT.md): الشيفرة مبنية ومُختبرة آلياً على Linux
(514 اختباراً ناجحاً، منها تكامل مع كاميرا ISAPI وهمية وبوابة حقيقية وTLS حقيقي وسحابة Hik-Partner Pro وهمية)، وكونسول الويب جُرّب بمتصفح Chromium حقيقي
ضد البوابة الفعلية، وواجهة WinForms **مُجمَّعة** لكن **لم تُشغَّل بصرياً**، ولم تُجرَّب على كاميرات فعلية بعد — يلزم اختبار قبول ميداني قبل الإنتاج.
