using System.CommandLine.Parsing;

namespace Harp.Toolkit;

internal static class SymbolValidation
{
    public static FileInfo AcceptExistingOnly(this SymbolResult result, FileInfo fileInfo)
    {
        if (!Path.Exists(fileInfo.FullName))
            result.AddError($"File does not exist: '{fileInfo}'.");
        return fileInfo;
    }
}
