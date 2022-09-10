namespace MqttToCsharp2.Generator.Models;

public abstract class DeviceProperty
{
	public string Type { get; set; }
	
	public string Name { get; set; }
	
	public string Description { get; set; }
	
	public int Access { get; set; }
}