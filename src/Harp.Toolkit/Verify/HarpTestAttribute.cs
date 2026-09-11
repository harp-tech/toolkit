namespace Harp.Toolkit.Verify;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class HarpTestAttribute : Attribute
{
    public string? Description { get; set; }

    public bool Prerelease { get; set; }
}
