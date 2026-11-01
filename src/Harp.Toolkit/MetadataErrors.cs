using YamlDotNet.Core;

namespace Harp.Toolkit;

static class MetadataErrors
{
    public static string Describe(FileInfo metadataPath, YamlException exception)
    {
        return exception.Start.Equals(Mark.Empty)
            ? $"{metadataPath.FullName}: {exception.Message}"
            : $"{metadataPath.FullName}({exception.Start.Line},{exception.Start.Column}): {exception.Message}";
    }
}
