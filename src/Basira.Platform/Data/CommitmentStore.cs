using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;

namespace Basira.Platform.Data;

/// <summary>قرارات المستخدمين على الالتزامات: تُحفظ بمفتاح الالتزام (المستند ونص الجملة) وتُسجَّل في التدقيق.</summary>
public sealed class CommitmentStore
{
    public static readonly string[] States = { "fulfilled", "dismissed", "open" };
    private readonly Db _db;
    public event Action? Changed;

    public CommitmentStore(Db db) => _db = db;

    public Dictionary<string, CommitmentDecision> All()
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM commitment_decisions", r => new CommitmentDecision(r.Str("key"), r.Str("document_id"), r.Str("state"), r.Str("note"), r.Str("actor"), r.Str("at")))
            .ToDictionary(d => d.Key);
    }

    public void Set(string key, string documentId, string state, string note, string actor, string label)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        c.Exec("""
            INSERT INTO commitment_decisions(key, document_id, state, note, actor, at) VALUES(@key, @documentId, @state, @note, @actor, @at)
            ON CONFLICT(key) DO UPDATE SET state=@state, note=@note, actor=@actor, at=@at
            """, new { key, documentId, state, note, actor, at = Clock.Now() }, tx);
        AuditStore.Append(c, tx, actor, "قرار على التزام", label);
        tx.Commit();
        Changed?.Invoke();
    }
}
