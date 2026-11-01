using System.Diagnostics.CodeAnalysis;

namespace Harp.Toolkit;

static class ExceptionExtensions
{
    const string DescriptionKey = "Harp.Toolkit.Description";

    public static void SetDescription(this Exception exception, string description)
    {
        exception.Data[DescriptionKey] = description;
    }

    public static bool TryGetDescription(this Exception exception, [NotNullWhen(true)] out string? description)
    {
        description = exception.Data[DescriptionKey] as string;
        return description is not null;
    }
}
