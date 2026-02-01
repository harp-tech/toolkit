namespace Harp.Toolkit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class HarpTestAttribute : Attribute
{
    public string? Description { get; set; }
}
