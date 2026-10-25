using AIDocumentArchiveVB;
using Basira.Platform.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Basira.Platform.Data;

/// <summary>سجل تدقيق مرتبط بسلسلة تجزئة SHA-256 داخل قاعدة البيانات؛ يُكتب في معاملة العملية نفسها.</summary>
public sealed class AuditStore
{
    private readonly Db _db;
    public AuditStore(Db db) => _db = db;

    public static void Append(SqliteConnection c, SqliteTransaction? tx, string actor, string action, string detail)
    {
        actor = Clip(actor, 120); action = Clip(action, 80); detail = Clip(detail, 500);
        var last = c.Query("SELECT seq, hash FROM audit ORDER BY seq DESC LIMIT 1", r => (Seq: r.GetInt64(0), Hash: r.GetString(1)), null, tx).FirstOrDefault();
        var seq = last.Seq + 1;
        var prev = last.Hash ?? AuditChain.GenesisHash;
        if (seq == 1) prev = AuditChain.GenesisHash;
        var ts = Clock.Now();
        var hash = AuditChain.ComputeHash(seq, prev, ts, actor, action, detail);
        c.Exec("INSERT INTO audit(seq, ts, actor, action, detail, prev_hash, hash) VALUES(@seq,@ts,@actor,@action,@detail,@prev,@hash)",
            new { seq, ts, actor, action, detail, prev, hash }, tx);
    }

    private static string Clip(string? v, int max) { v ??= ""; return v.Length <= max ? v : v[..max]; }

    public void Append(string actor, string action, string detail)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        Append(c, tx, actor, action, detail);
        tx.Commit();
    }

    public (bool Valid, long Count, long FirstBroken, string Message, string LastHash) Verify()
    {
        using var c = _db.Open();
        var prev = AuditChain.GenesisHash;
        long expected = 1, count = 0;
        foreach (var r in c.Query("SELECT seq, ts, actor, action, detail, prev_hash, hash FROM audit ORDER BY seq",
                     r => (Seq: r.GetInt64(0), Ts: r.GetString(1), Actor: r.GetString(2), Action: r.GetString(3), Detail: r.GetString(4), Prev: r.GetString(5), Hash: r.GetString(6))))
        {
            if (r.Seq != expected) return (false, count, r.Seq, $"تسلسل مفقود قبل السجل {r.Seq} (حُذف سجل أو أكثر).", prev);
            if (r.Prev != prev) return (false, count, r.Seq, $"الارتباط بالسجل السابق غير مطابق عند السجل {r.Seq}.", prev);
            if (AuditChain.ComputeHash(r.Seq, r.Prev, r.Ts, r.Actor, r.Action, r.Detail) != r.Hash) return (false, count, r.Seq, $"محتوى السجل {r.Seq} لا يطابق تجزئته (عُدّل بعد كتابته).", prev);
            prev = r.Hash; expected++; count++;
        }
        return (true, count, 0, "السلسلة سليمة: لم يُعدَّل أي سجل ولم يُحذف.", prev);
    }

    public List<AuditEntry> Recent(int take, string? actor = null, string? query = null)
    {
        using var c = _db.Open();
        return c.Query("SELECT seq, ts, actor, action, detail FROM audit WHERE (@actor = '' OR actor = @actor) AND (@q = '' OR action LIKE '%'||@q||'%' OR detail LIKE '%'||@q||'%') ORDER BY seq DESC LIMIT @take",
            r => new AuditEntry(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)), new { take, actor = actor ?? "", q = query ?? "" });
    }
}
