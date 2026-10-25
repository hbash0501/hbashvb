using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Basira.Platform.Infrastructure;

/// <summary>اتصالات SQLite (وضع WAL) وترحيل المخطط بإصدارات مرقّمة.</summary>
public sealed class Db
{
    private readonly string _cs;

    public Db(PlatformOptions options)
    {
        Directory.CreateDirectory(options.DataFolder);
        _cs = new SqliteConnectionStringBuilder { DataSource = options.DatabasePath, Pooling = true }.ToString();
        Migrate();
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        c.Exec("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=8000;");
        return c;
    }

    private static readonly string[] Migrations =
    {
        """
        CREATE TABLE users(
          id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE COLLATE NOCASE, full_name TEXT NOT NULL, role TEXT NOT NULL,
          salt TEXT NOT NULL, hash TEXT NOT NULL, active INTEGER NOT NULL DEFAULT 1, stamp TEXT NOT NULL,
          created_at TEXT NOT NULL, last_login TEXT);
        CREATE TABLE documents(
          id TEXT PRIMARY KEY, archive_no TEXT NOT NULL UNIQUE, title TEXT NOT NULL DEFAULT '', category TEXT NOT NULL DEFAULT '',
          sub_category TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'جديد', direction TEXT NOT NULL DEFAULT 'وارد',
          confidentiality TEXT NOT NULL DEFAULT 'عادي', priority TEXT NOT NULL DEFAULT 'عادية', source_entity TEXT NOT NULL DEFAULT '',
          destination_entity TEXT NOT NULL DEFAULT '', outgoing_no TEXT NOT NULL DEFAULT '', incoming_no TEXT NOT NULL DEFAULT '',
          external_no TEXT NOT NULL DEFAULT '', document_date TEXT NOT NULL DEFAULT '', due_date TEXT NOT NULL DEFAULT '',
          tags TEXT NOT NULL DEFAULT '', summary TEXT NOT NULL DEFAULT '', text TEXT NOT NULL DEFAULT '',
          file_id TEXT NOT NULL DEFAULT '', file_name TEXT NOT NULL DEFAULT '', file_type TEXT NOT NULL DEFAULT '', file_size INTEGER NOT NULL DEFAULT 0,
          page_count INTEGER NOT NULL DEFAULT 0, ocr_quality INTEGER NOT NULL DEFAULT 0,
          processing TEXT NOT NULL DEFAULT 'none', processing_error TEXT NOT NULL DEFAULT '',
          created_at TEXT NOT NULL, created_by TEXT NOT NULL, updated_at TEXT NOT NULL, updated_by TEXT NOT NULL, version INTEGER NOT NULL DEFAULT 1);
        CREATE INDEX ix_documents_file ON documents(file_id);
        CREATE TABLE revisions(
          id INTEGER PRIMARY KEY AUTOINCREMENT, document_id TEXT NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
          version INTEGER NOT NULL, changed_at TEXT NOT NULL, changed_by TEXT NOT NULL, summary TEXT NOT NULL, snapshot TEXT NOT NULL);
        CREATE INDEX ix_revisions_doc ON revisions(document_id, version);
        CREATE TABLE comments(
          id INTEGER PRIMARY KEY AUTOINCREMENT, document_id TEXT NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
          author TEXT NOT NULL, body TEXT NOT NULL, created_at TEXT NOT NULL);
        CREATE INDEX ix_comments_doc ON comments(document_id);
        CREATE TABLE audit(
          seq INTEGER PRIMARY KEY, ts TEXT NOT NULL, actor TEXT NOT NULL, action TEXT NOT NULL, detail TEXT NOT NULL,
          prev_hash TEXT NOT NULL, hash TEXT NOT NULL);
        CREATE TABLE jobs(
          id TEXT PRIMARY KEY, document_id TEXT NOT NULL, title TEXT NOT NULL, state TEXT NOT NULL, stage TEXT NOT NULL,
          progress INTEGER NOT NULL, message TEXT NOT NULL, created_at TEXT NOT NULL, finished_at TEXT);
        CREATE INDEX ix_jobs_created ON jobs(created_at);
        CREATE TABLE settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE counters(name TEXT PRIMARY KEY, value INTEGER NOT NULL);
        """,
        // التحقق من القراءة الآلية: كل حقل حرج قرأه OCR (الرقم، التاريخ، الموضوع، الإشارة، المهلة) مع مقتطع صورته وحالته.
        """
        ALTER TABLE documents ADD COLUMN scan_quality TEXT NOT NULL DEFAULT '';
        CREATE TABLE field_checks(
          id INTEGER PRIMARY KEY AUTOINCREMENT, document_id TEXT NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
          field TEXT NOT NULL, target TEXT NOT NULL DEFAULT '', value TEXT NOT NULL, candidates TEXT NOT NULL DEFAULT '[]',
          state TEXT NOT NULL, reason TEXT NOT NULL DEFAULT '', page INTEGER NOT NULL DEFAULT 0, box TEXT NOT NULL DEFAULT '',
          text_offset INTEGER NOT NULL DEFAULT -1, text_length INTEGER NOT NULL DEFAULT 0, span_text TEXT NOT NULL DEFAULT '',
          crop_id TEXT NOT NULL DEFAULT '', final_value TEXT NOT NULL DEFAULT '', reviewed_by TEXT NOT NULL DEFAULT '',
          reviewed_at TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL);
        CREATE INDEX ix_checks_doc ON field_checks(document_id);
        CREATE INDEX ix_checks_state ON field_checks(state);
        """,
        // التصنيف الآلي والعنوان من المحتوى: الحقول المملوءة آلياً، وتفاصيل التصنيف، وقرارات المستخدمين عليه (لقياس الدقة الفعلية).
        """
        ALTER TABLE documents ADD COLUMN auto_fields TEXT NOT NULL DEFAULT '';
        ALTER TABLE documents ADD COLUMN classification TEXT NOT NULL DEFAULT '';
        CREATE TABLE classification_feedback(
          id INTEGER PRIMARY KEY AUTOINCREMENT, document_id TEXT NOT NULL, predicted_category TEXT NOT NULL, predicted_sub TEXT NOT NULL,
          final_category TEXT NOT NULL, final_sub TEXT NOT NULL, confidence REAL NOT NULL, confident INTEGER NOT NULL,
          action TEXT NOT NULL, actor TEXT NOT NULL, at TEXT NOT NULL);
        """,
    };

    private void Migrate()
    {
        using var c = new SqliteConnection(_cs);
        c.Open();
        c.Exec("PRAGMA journal_mode=WAL;");
        var version = Convert.ToInt32(c.Scalar("PRAGMA user_version;"));
        for (var i = version; i < Migrations.Length; i++)
        {
            using var tx = c.BeginTransaction();
            c.Exec(Migrations[i], null, tx);
            c.Exec($"PRAGMA user_version={i + 1};", null, tx);
            tx.Commit();
        }
    }
}

/// <summary>أدوات استعلام مختصرة؛ المعاملات تُمرَّر كخصائص كائن مجهول (@name).</summary>
public static class SqliteExtensions
{
    private static void Bind(SqliteCommand cmd, object? args)
    {
        if (args is null) return;
        foreach (var p in args.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            cmd.Parameters.AddWithValue("@" + p.Name, p.GetValue(args) ?? DBNull.Value);
    }

    private static SqliteCommand Cmd(SqliteConnection c, string sql, object? args, SqliteTransaction? tx)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        Bind(cmd, args);
        return cmd;
    }

    public static int Exec(this SqliteConnection c, string sql, object? args = null, SqliteTransaction? tx = null)
    {
        using var cmd = Cmd(c, sql, args, tx);
        return cmd.ExecuteNonQuery();
    }

    public static object? Scalar(this SqliteConnection c, string sql, object? args = null, SqliteTransaction? tx = null)
    {
        using var cmd = Cmd(c, sql, args, tx);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    public static List<T> Query<T>(this SqliteConnection c, string sql, Func<SqliteDataReader, T> map, object? args = null, SqliteTransaction? tx = null)
    {
        using var cmd = Cmd(c, sql, args, tx);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    public static string Str(this SqliteDataReader r, string name) { var i = r.GetOrdinal(name); return r.IsDBNull(i) ? "" : r.GetString(i); }
    public static long Long(this SqliteDataReader r, string name) { var i = r.GetOrdinal(name); return r.IsDBNull(i) ? 0 : r.GetInt64(i); }
}

public static class Clock
{
    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
}
