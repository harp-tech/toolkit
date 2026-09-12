using System.CommandLine;

namespace Harp.Toolkit;

public abstract class PortNameOption : Option<string>
{
    protected PortNameOption(string name)
        : base(name)
    {
    }
}
