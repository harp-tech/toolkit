namespace Harp.Toolkit;

public class DevicePortNameOption : PortNameOption
{
    public DevicePortNameOption()
        : base("--port")
    {
        Description = "Name of the serial port used to communicate with the device.";
        Required = true;
    }
}
