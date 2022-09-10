namespace MqttToCsharp2.Generator.Models;

public class NumericProperty : DeviceProperty
{
	public long? MaxValue { get; set; }
	
	public long? MinValue { get; set; }
}