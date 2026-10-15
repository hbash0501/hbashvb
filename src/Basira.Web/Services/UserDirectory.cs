using AIDocumentArchiveVB;

namespace Basira.Web.Services;

/// <summary>يقرأ users.xml الذي يديره التطبيق المكتبي ويعيد تحميله عند تغيّره.</summary>
public sealed class UserDirectory
{
    private readonly PortalOptions _options;
    private readonly object _lock = new();
    private AuthService? _auth;
    private DateTime _stamp = DateTime.MinValue;

    public UserDirectory(PortalOptions options) => _options = options;

    private string UsersPath => Path.Combine(_options.DataFolder, "users.xml");

    private AuthService Auth()
    {
        lock (_lock)
        {
            var stamp = File.Exists(UsersPath) ? File.GetLastWriteTimeUtc(UsersPath) : DateTime.MinValue;
            if (_auth is null || stamp != _stamp)
            {
                _auth = new AuthService(new AppSettings
                {
                    BaseDataFolder = _options.DataFolder,
                    ArchiveRootFolder = _options.ArchiveRootFolder,
                    CurrentEmployeeName = "بوابة الويب",
                });
                _stamp = File.Exists(UsersPath) ? File.GetLastWriteTimeUtc(UsersPath) : DateTime.MinValue;
            }
            return _auth;
        }
    }

    public bool NeedsSetup => Auth().NeedsInitialSetup;

    public UserAccount? Verify(string userName, string password) => Auth().VerifyCredentials(userName, password);

    public UserAccount? Find(string userName)
    {
        var u = Auth().GetUsers().FirstOrDefault(x => string.Equals(x.UserName, userName, StringComparison.OrdinalIgnoreCase));
        return u is { IsActive: true } ? u : null;
    }

    public void CreateInitialAdmin(string password) => Auth().CreateInitialAdmin(password);

    public static bool Can(UserAccount user, string permission) => AuthService.RoleHasPermission(user.RoleName, permission);

    /// <summary>الأدوار التي تقرأ نصوص المستندات الحساسة.</summary>
    public static bool CanReadSensitive(UserAccount user) => Can(user, "audit");
}
