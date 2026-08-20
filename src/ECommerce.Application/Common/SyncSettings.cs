namespace ECommerce.Application.Common;

/// <summary>
/// Settings for the 1C inventory sync endpoint. The password is a shared secret
/// handed to the 1C operator. It is deliberately NOT committed — in production
/// it's supplied via the <c>Sync__Password</c> environment variable (systemd).
/// When empty, the sync endpoint fails closed (rejects every request).
/// </summary>
public class SyncSettings
{
    public const string SectionName = "Sync";

    public string? Password { get; set; }
}
