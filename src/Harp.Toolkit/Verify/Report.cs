namespace Harp.Toolkit.Verify;

public class Report
{
    public string DeviceName { get; set; } = "Unknown Device";
    public DateTime RunDate { get; set; } = DateTime.Now;
    public bool IncludePrerelease { get; set; }
    public string ProtocolNotice { get; set; } = string.Empty;
    public string DeclaredProtocolVersion { get; set; } = string.Empty;
    public string CheckedProtocolVersion { get; set; } = string.Empty;
    public string RegisterSetVersion { get; set; } = string.Empty;
    public List<SuiteResult> Suites { get; set; } = new();
}
