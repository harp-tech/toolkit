namespace Harp.Toolkit.Verify;

public class Report
{
    public string DeviceName { get; set; } = "Unknown Device";
    public string PortName { get; set; } = string.Empty;
    public string WhoAmI { get; set; } = string.Empty;
    public string HardwareVersion { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public DateTime RunDate { get; set; } = DateTime.Now;
    public bool IncludePrerelease { get; set; }
    public string ProtocolNotice { get; set; } = string.Empty;
    public string AbortReason { get; set; } = string.Empty;
    public string DeclaredProtocolVersion { get; set; } = string.Empty;
    public string CheckedProtocolVersion { get; set; } = string.Empty;
    public string ProtocolCommit { get; set; } = string.Empty;
    public string ProtocolCommitUrl { get; set; } = string.Empty;
    public string RegisterSetVersion { get; set; } = string.Empty;
    public List<SuiteResult> Suites { get; set; } = new();
}
