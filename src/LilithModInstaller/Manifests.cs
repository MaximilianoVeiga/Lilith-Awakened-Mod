namespace LilithModInstaller;

// Release and installed-state manifest models.
internal sealed class ReleaseManifest
{
    public string Version { get; set; } = "0.1.1-rc6";
    public Dictionary<string, PackageSpec> Packages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class PackageSpec
{
    public string File { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Bytes { get; set; }
}

internal sealed class InstalledManifest
{
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; }
    public Dictionary<string, List<string>> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
