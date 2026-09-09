namespace Harp.Toolkit.Verify;

/// <summary>
/// Identifies the specification text encoded by the conformance checks.
/// </summary>
internal static class ProtocolReference
{
    /// <summary>
    /// The harp-tech/protocol commit carrying that specification text.
    /// </summary>
    public const string Commit = "11b584bdd2eb45a8b55ebc540512864ff0667326";

    /// <summary>
    /// Gets the abbreviated commit, for display where a link carries the full reference.
    /// </summary>
    public static string ShortCommit => Commit[..7];

    /// <summary>
    /// Gets the address of the specification documents as they stood at that commit.
    /// </summary>
    public static string TreeUrl => $"https://github.com/harp-tech/protocol/tree/{Commit}";
}
