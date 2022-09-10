namespace MqttToCsharp2.Generator.Models;

public class CompositeProperty : DeviceProperty
{
	public List<DeviceProperty> Properties { get; set; }
}