namespace Harp.Toolkit.Verify;

public class Report
{
    public string DeviceName { get; set; } = "Unknown Device";
    public DateTime RunDate { get; set; } = DateTime.Now;
    public bool IncludePrerelease { get; set; }
    public int PrereleaseTestCount { get; set; }
    public List<SuiteResult> Suites { get; set; } = new();
}
