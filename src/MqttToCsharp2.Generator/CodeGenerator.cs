using System.Text;
using System.Text.RegularExpressions;
using MqttToCsharp2.Generator.ExtensionMethods;
using MqttToCsharp2.Generator.Models;

namespace MqttToCsharp2.Generator;

public class CodeGenerator
{
	private readonly List<Device> _devices;

	public CodeGenerator(List<Device> devices)
	{
		_devices = devices;
	}

	public string Generate(string @namespace)
	{
		return @$"
using System.Runtime.Serialization;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace {@namespace};

public static class Devices
{{
	private static readonly List<IDevice> AllDevicesPrivate = new();

	public static IReadOnlyCollection<IDevice> AllDevices => AllDevicesPrivate;

	public static async Task InitializeAsync()
	{{
		const string mqttIp = ""192.168.1.123"";
		
		var factory = new MqttFactory();
		var options = new MqttClientOptionsBuilder()
			.WithTcpServer(mqttIp)
			.Build();

		var client = factory.CreateMqttClient();
		await client.ConnectAsync(options);
		CreateDevices(client);

		client.ApplicationMessageReceivedAsync += HandleMessage;
		await client.SubscribeAsync(""zigbee2mqtt/#"");
	}}

	private static Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
	{{
		var deviceName = e.ApplicationMessage.Topic[""zigbee2mqtt/"".Length..];
		var device = AllDevicesPrivate.FirstOrDefault(d => d.FriendlyName == deviceName);
		if (device == null)
			return Task.CompletedTask;

		var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
		device.TriggerStateChanged(payloadJson);
		
		return Task.CompletedTask;
	}}

	{GenerateCreateDevices(_devices).Indent(1)}
	{GenerateDeviceProperties(_devices).Indent(1)}
}}
{DeclareIDevice()}
{_devices.Select(GenerateDeviceClass).ConcatStrings()}";
	}

	private static string GenerateCreateDevices(IEnumerable<Device> devices)
	{
		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("private static void CreateDevices(IMqttClient client)\n{");
		devices.ToList().ForEach(d => stringBuilder.AppendLine($"\tAllDevicesPrivate.Add({d.SanitizedName} = new(client));"));
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static string GenerateDeviceProperties(IEnumerable<Device> devices)
	{
		var sb = new StringBuilder();
		foreach (var device in devices)
			sb.AppendLine($"public static {device.SanitizedName} {device.SanitizedName} {{ get; private set; }}");

		return sb.ToString();
	}

	private static string DeclareIDevice()
	{
		return @"
public interface IDevice
{
	public string IeeeAddress { get; }
	public string FriendlyName { get; }
	internal void TriggerStateChanged(string payloadJson);
}";
	}

	private static string GenerateDeviceClass(Device device)
	{
		return $@"
public class {device.SanitizedName} : IDevice {{
	public string IeeeAddress => ""{device.IeeeAddress}"";
	public string FriendlyName => ""{device.Name}"";

	public delegate void StateChangedEventHandler(DeviceReadState state);

	public event StateChangedEventHandler StateChanged;

	private readonly IMqttClient _client;

	public {device.SanitizedName}(IMqttClient client)
	{{
		_client = client;
	}}

	public async Task SetAsync(DeviceSetState state)
	{{
		var settings = new JsonSerializerSettings();
		settings.Converters.Add(new StringEnumConverter());
		settings.NullValueHandling = NullValueHandling.Ignore;
		var json = JsonConvert.SerializeObject(state, settings);

		var message = new MqttApplicationMessageBuilder()
			.WithTopic($""zigbee2mqtt/{{IeeeAddress}}/set"")
			.WithPayload(json)
			.Build();

		await _client.PublishAsync(message);
	}}
	
	void IDevice.TriggerStateChanged(string payloadJson)
	{{
		DeviceReadState state;
		try
		{{
			state = JsonConvert.DeserializeObject<DeviceReadState>(payloadJson);
		}}
		catch (Exception e)
		{{
			Console.WriteLine(e);
			return;
		}}

		if (state == null)
			return;
		
		StateChanged?.Invoke(state);
	}}
	{GenerateDeviceSetState(device).Indent(1)}
	{GenerateDeviceReadState(device).Indent(1)}
}}";
	}

	private static string GenerateDeviceSetState(Device device)
	{
		return $@"
public class DeviceSetState
{{
	{device.SettableValues.Concat(device.Actions).Select(DevicePropertyToClassProperty).ConcatStrings().Indent(1)}
}}";
	}

	private static string DevicePropertyToClassProperty(DeviceProperty deviceProperty)
	{
		var sanitizedName = deviceProperty.Name.SanitizeFunctionName();
		
		var stringBuilder = new StringBuilder();

		if (!string.IsNullOrWhiteSpace(deviceProperty.Description))
		{
			stringBuilder.AppendLine($@"/// <summary>
/// {deviceProperty.Description.Replace("\n", "\n /// ")}
/// </summary>");
		}
		stringBuilder.AppendLine($"[JsonProperty(\"{deviceProperty.Name}\")]");

		if (deviceProperty is NumericValue numericValue)
		{
			stringBuilder.Append($"public int? {sanitizedName} {{ get; set; }}");
			return stringBuilder.ToString();
		}

		if (deviceProperty is not EnumValue enumValue)
		{
			Console.WriteLine("Unknown device property type: " + deviceProperty.GetType().Name);
			return null;
		}
		
		// Property is EnumValue
		var enumName = sanitizedName + "Enum";
		stringBuilder.AppendLine($"public {enumName}? {sanitizedName} {{ get; set; }}\n");
		stringBuilder.Append(GenerateEnum(enumName, enumValue.Values));

		return stringBuilder.ToString();
	}

	private static string GenerateEnum(string enumName, IEnumerable<string> values)
	{
		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"public enum {enumName}");
		stringBuilder.AppendLine("{");
		foreach (var value in values)
		{
			var valueEnumName = value.SanitizeFunctionName();
			valueEnumName = valueEnumName.IsAllCaps() ? valueEnumName.CapitalizeFirstLetterOnly() : valueEnumName;
			
			stringBuilder.AppendLine($"\t[EnumMember(Value = \"{value}\")]");
			stringBuilder.AppendLine($"\t{valueEnumName},");
		}
		stringBuilder.AppendLine("}");

		return stringBuilder.ToString();
	}

	private static string GenerateDeviceReadState(Device device)
	{
		return $@"
public class DeviceReadState
{{
	{device.SettableValues.Concat(device.OutputValues).Select(DevicePropertyToClassProperty).ConcatStrings().Indent(1)}
}}";
	}
}