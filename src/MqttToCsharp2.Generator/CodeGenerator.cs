using System.Text;
using System.Text.RegularExpressions;
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
		var codeStringBuilder = new StringBuilder();

		codeStringBuilder.AppendLine(@$"using System.Runtime.Serialization;
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

	private static void CreateDevices(IMqttClient client)
	{{
		AllDevicesPrivate.Add(PcRoomLight = new(client));
		AllDevicesPrivate.Add(PowerSwitch1 = new(client));
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
}}");
		
		DeclareIDevice(codeStringBuilder);

		foreach (var device in _devices)
		{
			codeStringBuilder.AppendLine($"public static {device.SanitizedName} {device.SanitizedName} {{ get; private set; }}");
		}


		_devices.ForEach(d => GenerateDeviceClass(codeStringBuilder, d));

		return codeStringBuilder.ToString();
	}

	private static void DeclareIDevice(StringBuilder stringBuilder)
	{
		stringBuilder.AppendLine(@"public interface IDevice
{
	public string IeeeAddress { get; }
	public string FriendlyName { get; }
	internal void TriggerStateChanged(string payloadJson);
}
");
	}

	private static void GenerateDeviceClass(StringBuilder stringBuilder, Device device)
	{
		stringBuilder.AppendLine($@"
public class {device.SanitizedName} : IDevice {{
	public string IeeeAddress => ""{device.IeeeAddress}"";
	public string FriendlyName => ""{device.Name}"";

	public delegate void StateChangedEventHandler(DeviceSetState state);

	public event StateChangedEventHandler StateChanged;

	private readonly IMqttClient _client;

	public PcRoomLight(IMqttClient client)
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
		DeviceSetState state;
		try
		{{
			state = JsonConvert.DeserializeObject<DeviceSetState>(payloadJson);
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
}}");
	}
}