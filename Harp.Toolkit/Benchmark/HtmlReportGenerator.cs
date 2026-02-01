using System.Reflection;
using RazorLight;

namespace Harp.Toolkit;

public static class HtmlReportGenerator
{
    public static async Task<string> GenerateAsync(Report report)
    {
        var engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
            .UseMemoryCachingProvider()
            .Build();

        // The template is copied to the output directory under Reporting/ReportTemplate.cshtml
        // RazorLight expects the path relative to the project root (which we set to the assembly location)
        string templatePath = Path.Combine("Benchmark", "ReportTemplate.cshtml");

        return await engine.CompileRenderAsync(templatePath, report);
    }
}
