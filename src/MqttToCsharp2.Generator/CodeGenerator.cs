using System.Text;
using MqttToCsharp2.Generator.ExtensionMethods;
using MqttToCsharp2.Generator.Models;

namespace MqttToCsharp2.Generator;

public class CodeGenerator
{
	private readonly List<Device> _devices;
	
	private Dictionary<EnumProperty, string> _enumMap;

	public CodeGenerator(List<Device> devices)
	{
		_devices = devices;
	}

	public string Generate(string @namespace)
	{
		_enumMap = new();
		
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
{DeclareTypes()}
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

	private string DeclareTypes()
	{
		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(@"
public interface IDevice
{
	public string IeeeAddress { get; }
	public string FriendlyName { get; }
	internal void TriggerStateChanged(string payloadJson);
}

public enum OnOffToggle
{
	[EnumMember(Value = ""ON"")]
	On,
	[EnumMember(Value = ""OFF"")]
	Off,
	[EnumMember(Value = ""TOGGLE"")]
	Toggle,
}
");
		
		// Does not account for composite types.
		var allDevicesproperties = _devices.OrderBy(d => d.IeeeAddress)
			.SelectMany(d => d.Actions)
			.Concat(_devices.SelectMany(d => d.OutputValues))
			.Concat(_devices.SelectMany(d => d.SettableValues))
			.Distinct()
			.ToList();

		allDevicesproperties.AddRange(allDevicesproperties
			.OfType<CompositeProperty>()
			.SelectMany(p => p.Properties)
			.ToList());

		var uniqueEnums = allDevicesproperties
			.OfType<EnumProperty>()
			.Select(p => new
			{
				Property = p,
				ConcatValues = string.Join(",", p.Values.OrderBy(v => v))
			})
			.GroupBy(eg => eg.ConcatValues)
			.Select(g => new
			{
				Name = g.First().Property.Name.SanitizeFunctionName(),
				g.First().Property.Values,
				Properties = g.Select(x => x.Property).ToList()
			})
			.OrderBy(e => e.Name);
		
		var declaredEnumNames = new List<string>();
		foreach (var uniqueEnum in uniqueEnums)
		{
			string enumName = null;
			for (var i = 0; i <= 10; i++)
			{
				enumName = i == 0 ? uniqueEnum.Name : uniqueEnum.Name + i;
				
				if (!declaredEnumNames.Contains(enumName))
					break;
			}

			if (enumName == null)
				throw new InvalidOperationException("Could not generate a unique name for enum after 10 attempts: " + uniqueEnum.Name);

			declaredEnumNames.Add(enumName);
			stringBuilder.AppendLine(GenerateEnum(enumName, uniqueEnum.Values));
			uniqueEnum.Properties.ForEach(p => _enumMap.Add(p, enumName));
		}

		return stringBuilder.ToString();
	}

	private string GenerateDeviceClass(Device device)
	{
		return $@"
public class {device.SanitizedName} : IDevice {{
	public string IeeeAddress => ""{device.IeeeAddress}"";
	public string FriendlyName => ""{device.Name}"";

	public DeviceReadState LastState {{ get; private set; }}

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

	private TaskCompletionSource _getStateTcs;

	public async Task<DeviceReadState> GetAsync()
	{{
		var json = JsonConvert.SerializeObject(new DeviceReadState());

		var message = new MqttApplicationMessageBuilder()
			.WithTopic($""zigbee2mqtt/{{IeeeAddress}}/get"")
			.WithPayload(json)
			.Build();

		_getStateTcs = new();
		await _client.PublishAsync(message);

		var waitResult = await Task.WhenAny(_getStateTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
		return waitResult == _getStateTcs.Task ? LastState : null;
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
		
		LastState = state;
		
		_getStateTcs?.TrySetResult();
		StateChanged?.Invoke(state);
	}}
	{GenerateDeviceSetState(device).Indent(1)}
	{GenerateDeviceReadState(device).Indent(1)}
}}";
	}

	private string GenerateDeviceSetState(Device device)
	{
		return $@"
public class DeviceSetState
{{
	{device.SettableValues.Concat(device.Actions).Select(DevicePropertyToClassProperty).ConcatStrings().Indent(1)}
}}";
	}

	private string DevicePropertyToClassProperty(DeviceProperty deviceProperty)
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

		switch (deviceProperty)
		{
			case NumericProperty _:
				stringBuilder.AppendLine($"public int? {sanitizedName} {{ get; set; }}");
				return stringBuilder.ToString();
			case BooleanProperty _:
				stringBuilder.AppendLine($"public bool? {sanitizedName} {{ get; set; }}");
				return stringBuilder.ToString();
			case OnOffToggleProperty _:
				stringBuilder.AppendLine($"public OnOffToggle? {sanitizedName} {{ get; set; }}");
				return stringBuilder.ToString();
			case CompositeProperty compositeProperty:
				var className = sanitizedName + "Composite";
				stringBuilder.AppendLine($"public {className} {sanitizedName} {{ get; set; }}\n");
				stringBuilder.Append(GenerateCompositePropertyClass(className, compositeProperty.Properties));
				return stringBuilder.ToString();
			case EnumProperty enumProperty:
				stringBuilder.AppendLine($"public {_enumMap[enumProperty]}? {sanitizedName} {{ get; set; }}\n");
				return stringBuilder.ToString();
			default:
				Console.WriteLine("Unknown device property type: " + deviceProperty.GetType().Name);
				return null;
		}
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

	private string GenerateCompositePropertyClass(string className, List<DeviceProperty> properties)
	{
		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"public class {className}");
		stringBuilder.AppendLine("{");
		foreach (var property in properties)
		{
			stringBuilder.AppendLine("\t" + DevicePropertyToClassProperty(property).Indent(1));
		}
		stringBuilder.AppendLine("}");

		return stringBuilder.ToString();
	}

	private string GenerateDeviceReadState(Device device)
	{
		return $@"
public class DeviceReadState
{{
	{device.SettableValues.Concat(device.OutputValues).Select(DevicePropertyToClassProperty).ConcatStrings().Indent(1)}
}}";
	}
}