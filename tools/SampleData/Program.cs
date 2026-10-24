using AIDocumentArchiveVB;

// الاستخدام: dotnet run --project tools/SampleData -- <مجلد البيانات> [عدد المستندات]
var data = Path.GetFullPath(args.Length > 0 ? args[0] : "sample-data");
var count = args.Length > 1 ? int.Parse(args[1]) : 60;
var settings = new AppSettings { BaseDataFolder = data, ArchiveRootFolder = Path.Combine(data, "ArchiveFiles"), CurrentEmployeeName = "مولّد البيانات" };
var repo = new ArchiveRepository(settings);
repo.EnsureSeedData();
var auth = new AuthService(settings);
if (auth.NeedsInitialSetup) auth.CreateInitialAdmin("Sample-Owner-2026!");
foreach (var (user, name, role) in new[] { ("reviewer", "سارة المراجعة", "مراجع"), ("reader", "خالد القارئ", "قارئ") })
{
    auth.SaveUser(new UserAccount { UserName = user, FullName = name, RoleName = role }, "Sample-User-2026!");
}

var rnd = new Random(7);
var today = DateTime.Today;
var entities = new[] { "الإدارة التقنية", "إدارة العقود", "الإدارة المالية", "الموارد البشرية", "مكتب المدير", "إدارة الأرشيف", "الشؤون القانونية" };
var topics = new (string Cat, string Sub, string Title, string Body)[]
{
    ("عقود", "عقد صيانة", "عقد صيانة الأنظمة والتجهيزات", "يرجى مراجعة عقد الصيانة واستكمال مطابقة نطاق الأعمال مع محضر التسليم قبل الموعد المحدد."),
    ("عقود", "عقد توريد", "عقد توريد أجهزة حاسب", "ينص العقد على توريد أجهزة حاسب وفق المواصفات المرفقة خلال ثلاثين يوماً من تاريخ الإبلاغ."),
    ("مالي", "فواتير", "فاتورة أعمال الصيانة الشهرية", "يرجى مطابقة الفاتورة مع بنود العقد واعتماد الصرف بعد المراجعة المالية."),
    ("مالي", "مستخلصات", "مستخلص أعمال المرحلة الثانية", "تم رفع المستخلص للاعتماد مرفقاً بمحضر الاستلام وشهادة الإنجاز."),
    ("موارد بشرية", "إجازات", "تعميم تنظيم الإجازات السنوية", "أُقر جدول الإجازات السنوية بعد مراجعة الإدارات المختصة، ويُعمل به اعتباراً من بداية العام."),
    ("موارد بشرية", "انتدابات", "طلب انتداب موظف لحضور دورة تدريبية", "يرجى الموافقة على انتداب الموظف لحضور الدورة التدريبية في مدينة أخرى لمدة خمسة أيام."),
    ("إداري", "قرارات", "قرار تشكيل لجنة مراجعة الإجراءات", "صدر قرار بتشكيل لجنة لمراجعة الإجراءات الداخلية ورفع توصياتها خلال شهر."),
    ("مراسلات", "وارد", "خطاب وارد بشأن استكمال بيانات معاملة", "نأمل استكمال بيانات المعاملة المرفقة وإفادتنا بما يتم في أقرب وقت."),
    ("تقارير", "تقرير شهري", "تقرير أداء الأرشيف الشهري", "يتناول التقرير أعمال التسجيل والمراجعة واكتمال بيانات المستندات خلال الشهر."),
    ("سري", "معاملة حساسة", "محضر تحقيق إداري", "محتوى سري يخص إجراءات تحقيق إداري ولا يجوز تداوله خارج الجهات المختصة."),
};
var statuses = new[] { "قيد المراجعة", "جاهز للمراجعة", "بانتظار المعالجة", "مؤرشف", "مؤرشف", "مؤرشف" };

for (var i = 1; i <= count; i++)
{
    var t = topics[rnd.Next(topics.Length)];
    var sensitive = t.Cat == "سري";
    var due = rnd.Next(4) == 0 ? today.AddDays(rnd.Next(-20, 15)).ToString("yyyy/MM/dd") : "";
    var hasText = rnd.Next(12) != 0;
    var entity = entities[rnd.Next(entities.Length)];
    var refNo = $"REF-{2026}-{rnd.Next(100, 400)}";
    var created = today.AddDays(-rnd.Next(1, 90));
    var cited = created.AddDays(-rnd.Next(3, 40));
    var cal = new System.Globalization.UmAlQuraCalendar();
    var hijri = $"{cal.GetYear(cited)}/{cal.GetMonth(cited):00}/{cal.GetDayOfMonth(cited):00}هـ";
    var opening = (i % 3) switch
    {
        0 => $"إشارة إلى خطاب سعادتكم رقم {rnd.Next(1000, 9999)} وتاريخ {hijri} بشأن {t.Title}، ",
        1 when i > 5 => $"إلحاقاً لخطابنا رقم ARC-2026-{i - 3:000} وتاريخ {hijri}، ",
        _ => "",
    };
    var doc = new DocumentRecord
    {
        Title = t.Title + (i % 4 == 0 ? $" ({i})" : ""),
        GeneralCategory = i % 17 == 0 ? "" : t.Cat,
        SubCategory = t.Sub,
        Status = statuses[rnd.Next(statuses.Length)],
        ArchiveNumber = $"ARC-2026-{i:000}",
        ReferenceNumber = refNo,
        SourceEntity = entity,
        DestinationEntity = entities[rnd.Next(entities.Length)],
        DueDateText = due,
        Confidentiality = sensitive ? "سري" : "عادي",
        Tags = string.Join("، ", new[] { t.Sub, entity }),
        Summary = t.Body.Split('،', '.')[0],
        TextContent = hasText ? $"الموضوع: {t.Title}\n\n{opening}{t.Body}\n\nالمرجع: {refNo}. الجهة: {entity}. المستند رقم {i}." : "",
        FileHashSha256 = i % 11 == 0 ? new string('a', 64) : Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        FileName = $"doc-{i:000}.pdf", FileType = ".pdf", PageCount = rnd.Next(1, 12),
        CreatedAt = created,
    };
    repo.SaveDocument(doc);
}
Console.WriteLine($"تم إنشاء {count} مستنداً و3 مستخدمين في: {data}");
Console.WriteLine("admin / Sample-Owner-2026!   reviewer / Sample-User-2026!   reader / Sample-User-2026!");
