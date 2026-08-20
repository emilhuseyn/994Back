namespace ECommerce.Application.DTOs;

/// <summary>
/// Site-wide theme configuration. Persisted as a JSON blob in SiteSettings
/// under the key <c>_theme.config</c>. A sensible default is returned when no
/// row exists.
/// </summary>
public class ThemeDto
{
    public ThemeColorsDto Colors { get; set; } = new();
    public ThemeTypographyDto Typography { get; set; } = new();
    public ThemeLayoutDto Layout { get; set; } = new();
    public ThemeStorefrontDto Storefront { get; set; } = new();
    public ThemeAdminDto Admin { get; set; } = new();

    public static ThemeDto Default() => new();
}

public class ThemeColorsDto
{
    public string Primary { get; set; } = "#000000";
    public string PrimaryHover { get; set; } = "#262626";
    public string Accent { get; set; } = "#2563eb";
    public string Background { get; set; } = "#ffffff";
    public string Foreground { get; set; } = "#0a0a0a";
    public string Muted { get; set; } = "#6b7280";
    public string Border { get; set; } = "#e5e7eb";
    public string Soft { get; set; } = "#f7f7f7";
    public string Success { get; set; } = "#16a34a";
    public string Danger { get; set; } = "#dc2626";
    public string Warning { get; set; } = "#f59e0b";
}

public class ThemeTypographyDto
{
    public string FontFamily { get; set; } = "Inter";
    public int FontSizeBase { get; set; } = 14;       // px, applied to body
    public int FontSizeHeading { get; set; } = 28;    // px, hero / large headings
    public int FontWeightBold { get; set; } = 600;
}

public class ThemeLayoutDto
{
    public int ContainerWidth { get; set; } = 1280;   // px
    public int Radius { get; set; } = 4;              // px, default border radius
    public int SpacingBase { get; set; } = 16;        // px, base spacing unit
}

public class ThemeStorefrontDto
{
    public bool ShowAnnouncementBar { get; set; } = true;
    public string AnnouncementBg { get; set; } = "#000000";
    public string AnnouncementText { get; set; } = "#ffffff";
    public string HeaderBg { get; set; } = "#ffffff";
    public string FooterBg { get; set; } = "#ffffff";
}

public class ThemeAdminDto
{
    public string SidebarBg { get; set; } = "#ffffff";
    public string SidebarText { get; set; } = "#374151";
    public string SidebarActiveBg { get; set; } = "#000000";
    public string SidebarActiveText { get; set; } = "#ffffff";
    public string TopbarBg { get; set; } = "#ffffff";
}
