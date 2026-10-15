using System.Collections.Concurrent;

namespace Basira.Web.Services;

/// <summary>يحدّ من محاولات تخمين كلمات المرور: بعد عدد من الإخفاقات يُقفل المفتاح (مستخدم + عنوان) مدة النافذة.</summary>
public sealed class LoginThrottle
{
    private sealed class State { public int Failures; public DateTimeOffset WindowStart; public DateTimeOffset LockedUntil; }

    private readonly ConcurrentDictionary<string, State> _states = new();
    private readonly int _max;
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;

    public LoginThrottle(PortalOptions options, TimeProvider time)
    {
        _max = options.MaxFailedLogins;
        _window = options.LockoutWindow;
        _time = time;
    }

    private static string Key(string user, string ip) => user.Trim().ToLowerInvariant() + "|" + ip;

    /// <summary>الزمن المتبقي للقفل، أو null إن لم يكن مقفلاً.</summary>
    public TimeSpan? LockedFor(string user, string ip)
    {
        if (!_states.TryGetValue(Key(user, ip), out var s)) return null;
        lock (s)
        {
            var now = _time.GetUtcNow();
            return s.LockedUntil > now ? s.LockedUntil - now : null;
        }
    }

    public void RecordFailure(string user, string ip)
    {
        var s = _states.GetOrAdd(Key(user, ip), _ => new State());
        lock (s)
        {
            var now = _time.GetUtcNow();
            if (s.Failures == 0 || now - s.WindowStart > _window) { s.Failures = 0; s.WindowStart = now; }
            s.Failures++;
            if (s.Failures >= _max) { s.LockedUntil = now + _window; s.Failures = 0; }
        }
    }

    public void RecordSuccess(string user, string ip) => _states.TryRemove(Key(user, ip), out _);
}
