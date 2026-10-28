using System.Security.Cryptography;
using AIDocumentArchiveVB;
using Basira.Platform.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Basira.Platform.Data;

public sealed class UserStore
{
    private readonly Db _db;
    public UserStore(Db db) => _db = db;

    private static User Map(SqliteDataReader r) => new()
    {
        Id = r.Str("id"), UserName = r.Str("username"), FullName = r.Str("full_name"), Role = r.Str("role"), Salt = r.Str("salt"),
        Hash = r.Str("hash"), Active = r.Long("active") == 1, Stamp = r.Str("stamp"), CreatedAt = r.Str("created_at"), LastLogin = r.Str("last_login"),
    };

    public static bool Can(User u, string permission) => AuthService.RoleHasPermission(u.Role, permission);
    public static bool CanReadSensitive(User u) => Can(u, "audit");
    public static bool IsAdmin(User u) => u.Role == Roles.Admin;
    /// <summary>نقل المستندات إلى المحذوفات واستعادتها: مدير النظام ومدير الأرشيف. والحذف النهائي لمدير النظام وحده.</summary>
    public static bool CanDelete(User u) => Can(u, "maintenance");

    private static string NewSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    private static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    public bool NeedsSetup()
    {
        using var c = _db.Open();
        return Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM users")) == 0;
    }

    public List<User> List()
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM users ORDER BY full_name", Map);
    }

    public User? Find(string userName)
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM users WHERE username = @userName", Map, new { userName }).FirstOrDefault();
    }

    public User? Verify(string userName, string password)
    {
        var u = Find(userName.Trim());
        if (u is null || !u.Active)
        {
            PasswordHashService.Hash(password, "AAAAAAAAAAAAAAAAAAAAAA=="); // زمن مكافئ لا يكشف وجود الحساب.
            return null;
        }
        if (!PasswordHashService.Verify(password, u.Salt, u.Hash)) return null;
        using var c = _db.Open();
        // ترقية التجزئات القديمة المرحَّلة إلى PBKDF2 عند أول دخول ناجح.
        if (!u.Hash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal))
        {
            u.Salt = NewSalt();
            u.Hash = PasswordHashService.Hash(password, u.Salt);
            c.Exec("UPDATE users SET salt=@Salt, hash=@Hash WHERE id=@Id", new { u.Salt, u.Hash, u.Id });
        }
        c.Exec("UPDATE users SET last_login=@now WHERE id=@Id", new { now = Clock.Now(), u.Id });
        return u;
    }

    public User Create(string userName, string fullName, string role, string password, string actor)
    {
        userName = userName.Trim();
        if (userName.Length is < 2 or > 60 || userName.Any(char.IsWhiteSpace)) throw new ArgumentException("اسم المستخدم من 2 إلى 60 حرفاً دون مسافات.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("اكتب الاسم الكامل.");
        if (!Roles.All.Contains(role)) throw new ArgumentException("دور غير معروف.");
        var reason = PasswordPolicy.Validate(password, userName);
        if (reason != "") throw new ArgumentException(reason);
        var u = new User { Id = Guid.NewGuid().ToString("N"), UserName = userName, FullName = fullName.Trim(), Role = role, Salt = NewSalt(), Stamp = NewStamp(), CreatedAt = Clock.Now() };
        u.Hash = PasswordHashService.Hash(password, u.Salt);
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        if (Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM users WHERE username=@userName", new { userName }, tx)) > 0) throw new ArgumentException("اسم المستخدم مستخدم مسبقاً.");
        c.Exec("INSERT INTO users(id, username, full_name, role, salt, hash, active, stamp, created_at) VALUES(@Id,@UserName,@FullName,@Role,@Salt,@Hash,1,@Stamp,@CreatedAt)", u, tx);
        AuditStore.Append(c, tx, actor, "إنشاء مستخدم", $"{u.UserName} ({u.Role})");
        tx.Commit();
        return u;
    }

    public void Update(string id, string? fullName, string? role, bool? active, string? newPassword, string actor)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        var u = c.Query("SELECT * FROM users WHERE id=@id", Map, new { id }, tx).FirstOrDefault() ?? throw new KeyNotFoundException("المستخدم غير موجود.");
        var changes = new List<string>();
        if (fullName is not null && fullName.Trim() != u.FullName) { u.FullName = fullName.Trim(); changes.Add("الاسم"); }
        if (role is not null && role != u.Role)
        {
            if (!Roles.All.Contains(role)) throw new ArgumentException("دور غير معروف.");
            if (u.Role == Roles.Admin && role != Roles.Admin && AdminCount(c, tx) <= 1) throw new ArgumentException("لا يمكن إزالة آخر مدير للنظام.");
            u.Role = role; changes.Add("الدور: " + role); u.Stamp = NewStamp();
        }
        if (active is not null && active != u.Active)
        {
            if (!active.Value && u.Role == Roles.Admin && AdminCount(c, tx) <= 1) throw new ArgumentException("لا يمكن إيقاف آخر مدير للنظام.");
            u.Active = active.Value; changes.Add(active.Value ? "تفعيل" : "إيقاف"); u.Stamp = NewStamp();
        }
        if (!string.IsNullOrEmpty(newPassword))
        {
            var reason = PasswordPolicy.Validate(newPassword, u.UserName);
            if (reason != "") throw new ArgumentException(reason);
            u.Salt = NewSalt(); u.Hash = PasswordHashService.Hash(newPassword, u.Salt); u.Stamp = NewStamp(); changes.Add("كلمة المرور");
        }
        c.Exec("UPDATE users SET full_name=@FullName, role=@Role, active=@a, salt=@Salt, hash=@Hash, stamp=@Stamp WHERE id=@Id",
            new { u.FullName, u.Role, a = u.Active ? 1 : 0, u.Salt, u.Hash, u.Stamp, u.Id }, tx);
        if (changes.Count > 0) AuditStore.Append(c, tx, actor, "تعديل مستخدم", u.UserName + ": " + string.Join("، ", changes));
        tx.Commit();
    }

    /// <summary>تغيير المستخدم كلمة مروره بنفسه: بكلمته الحالية، وتنتهي جلساته الأخرى بتغيير الختم.</summary>
    public User ChangeOwnPassword(string userName, string current, string next)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        var u = c.Query("SELECT * FROM users WHERE username=@userName", Map, new { userName }, tx).FirstOrDefault() ?? throw new KeyNotFoundException("المستخدم غير موجود.");
        if (!PasswordHashService.Verify(current, u.Salt, u.Hash)) throw new UnauthorizedAccessException("كلمة المرور الحالية غير صحيحة.");
        if (current == next) throw new ArgumentException("اختر كلمة مرور مختلفة عن الحالية.");
        var reason = PasswordPolicy.Validate(next, u.UserName);
        if (reason != "") throw new ArgumentException(reason);
        u.Salt = NewSalt(); u.Hash = PasswordHashService.Hash(next, u.Salt); u.Stamp = NewStamp();
        c.Exec("UPDATE users SET salt=@Salt, hash=@Hash, stamp=@Stamp WHERE id=@Id", new { u.Salt, u.Hash, u.Stamp, u.Id }, tx);
        AuditStore.Append(c, tx, u.UserName, "تغيير كلمة المرور", u.UserName);
        tx.Commit();
        return u;
    }

    private static long AdminCount(SqliteConnection c, SqliteTransaction tx) =>
        Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM users WHERE role=@r AND active=1", new { r = Roles.Admin }, tx));

    /// <summary>ترحيل مستخدم من التطبيق المكتبي مع تجزئته كما هي (تُرقّى عند أول دخول).</summary>
    public bool ImportLegacy(UserAccount a, string actor)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        if (Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM users WHERE username=@u", new { u = a.UserName }, tx)) > 0) return false;
        var role = Roles.All.Contains(a.RoleName) ? a.RoleName : "قارئ";
        c.Exec("INSERT INTO users(id, username, full_name, role, salt, hash, active, stamp, created_at) VALUES(@id,@u,@n,@role,@salt,@hash,@active,@stamp,@created)",
            new { id = string.IsNullOrWhiteSpace(a.Id) ? Guid.NewGuid().ToString("N") : a.Id, u = a.UserName, n = string.IsNullOrWhiteSpace(a.FullName) ? a.UserName : a.FullName, role,
                  salt = a.Salt ?? "", hash = a.PasswordHash ?? "", active = a.IsActive ? 1 : 0, stamp = NewStamp(), created = Clock.Now() }, tx);
        AuditStore.Append(c, tx, actor, "ترحيل مستخدم", a.UserName);
        tx.Commit();
        return true;
    }
}
