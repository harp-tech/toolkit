namespace Harp.Toolkit;

public class Report
{
    public string DeviceName { get; set; } = "Unknown Device";
    public DateTime RunDate { get; set; } = DateTime.Now;
    public List<SuiteResult> Suites { get; set; } = new();
}
