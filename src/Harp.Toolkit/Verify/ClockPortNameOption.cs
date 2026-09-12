namespace Harp.Toolkit.Verify;

public class ClockPortNameOption : PortNameOption
{
    public ClockPortNameOption()
        : base("--clock-port")
    {
        Description = "Serial port of the reference clock device. Enables clock alignment tests.";
    }
}
