using Bonsai.Harp;
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
    const int ReadTimeoutMilliseconds = 2000;

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

    public static async Task<ProtocolTarget> ResolveAsync(
        VerifyConnection connection,
        bool prereleaseRequested,
        CancellationToken cancellationToken = default)
    {
        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readTimeout.CancelAfter(ReadTimeoutMilliseconds);
        try
        {
            var reply = await connection.CommandAsync(
                HarpCommand.ReadByte(Suites.Version.Address),
                readTimeout.Token);
            var payload = reply.GetPayloadArray<byte>();
            if (payload.Length != Suites.Version.RegisterLength)
                return new ProtocolTarget(null, prereleaseRequested);

            var protocolVersion = Suites.Version.GetPayload(reply).ProtocolVersion;
            return protocolVersion.Major == 0
                ? new ProtocolTarget(null, prereleaseRequested)
                : new ProtocolTarget(protocolVersion, prereleaseRequested);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProtocolTarget(null, prereleaseRequested);
        }
    }
}
