namespace MqttToCsharp2.Generator.Models;

public class Device
{
	public string Name { get; set; }
	
	public string SanitizedName { get; set; }
		
	public string IeeeAddress { get; set; }
		
	public string Manufacturer { get; set; }
		
	public string ModelId { get; set; }
	
	public string Description { get; set; }

	public List<DeviceProperty> Actions { get; set; } = new();

	public List<DeviceProperty> OutputValues { get; set; } = new();

	public List<DeviceProperty> SettableValues { get; set; } = new();
}