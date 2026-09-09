using Harp.Toolkit.Verify.Suites;

namespace Harp.Toolkit.Verify;

internal enum ProtocolScope
{
    V1,
    V2,
    Unsupported,
}

internal readonly record struct ProtocolTarget(SemanticVersion? DeclaredVersion, bool PrereleaseRequested)
{
    const int PrereleaseMajorVersion = 2;

    public ProtocolScope Scope
    {
        get
        {
            var major = DeclaredVersion.HasValue ? DeclaredVersion.GetValueOrDefault().Major : 0;
            if (major > PrereleaseMajorVersion)
                return ProtocolScope.Unsupported;

            return major == PrereleaseMajorVersion ? ProtocolScope.V2 : ProtocolScope.V1;
        }
    }

    public bool IncludePrerelease => Scope == ProtocolScope.V2 && PrereleaseRequested;
}
