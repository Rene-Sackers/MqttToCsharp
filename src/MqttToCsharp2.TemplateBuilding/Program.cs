using System.Runtime.Serialization;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

await Devices.InitializeAsync();

var state = await Devices.PcRoomLight.GetAsync();

// Devices.PcRoomLight.StateChanged += async s =>
// {
// 	switch (s.State)
// 	{
// 		case PcRoomLight.DeviceReadState.StateEnum.On:
// 			await Devices.PowerSwitch1.SetAsync(new() { State = PowerSwitch1.DeviceSetState.StateEnum.On });
// 			break;
// 		case PcRoomLight.DeviceReadState.StateEnum.Off:
// 			await Devices.PowerSwitch1.SetAsync(new() { State = PowerSwitch1.DeviceSetState.StateEnum.Off });
// 			break;
// 	}
// 	
// 	if (s.State != PcRoomLight.DeviceReadState.StateEnum.On)
// 		return;
// 	
// 	await Task.Delay(5000);
// 	await Devices.PcRoomLight.SetAsync(new()
// 	{
// 		State = PcRoomLight.DeviceSetState.StateEnum.Off
// 	});
// };

// await Devices.PcRoomLight.SetAsync(new()
// {
// 	Effect = PcRoomLight.DeviceSetState.EffectEnum.Breathe
// });
// await Task.Delay(10000);
// await Devices.PcRoomLight.SetAsync(new()
// {
// 	Effect = PcRoomLight.DeviceSetState.EffectEnum.FinishEffect
// });

Console.ReadKey();

public static class Devices
{
	private static readonly List<IDevice> AllDevicesPrivate = new();

	public static IReadOnlyCollection<IDevice> AllDevices => AllDevicesPrivate;

	public static PcRoomLight PcRoomLight { get; private set; }

	public static PowerSwitch1 PowerSwitch1 { get; private set; }
	
	public static async Task InitializeAsync()
	{
		const string mqttIp = "192.168.1.123";
		
		var factory = new MqttFactory();
		var options = new MqttClientOptionsBuilder()
			.WithTcpServer(mqttIp)
			.Build();

		var client = factory.CreateMqttClient();
		await client.ConnectAsync(options);
		CreateDevices(client);

		client.ApplicationMessageReceivedAsync += HandleMessage;
		await client.SubscribeAsync("zigbee2mqtt/#");
	}

	private static void CreateDevices(IMqttClient client)
	{
		AllDevicesPrivate.Add(PcRoomLight = new(client));
		AllDevicesPrivate.Add(PowerSwitch1 = new(client));
	}

	private static Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
	{
		var deviceName = e.ApplicationMessage.Topic["zigbee2mqtt/".Length..];
		var device = AllDevicesPrivate.FirstOrDefault(d => d.FriendlyName == deviceName);
		if (device == null)
			return Task.CompletedTask;

		var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
		device.TriggerStateChanged(payloadJson);
		
		return Task.CompletedTask;
	}
}

public interface IDevice
{
	public string IeeeAddress { get; }
	public string FriendlyName { get; }
	internal void TriggerStateChanged(string payloadJson);
}

public class PcRoomLight : IDevice
{
	public string IeeeAddress => "0x60a423fffef1a847";
	public string FriendlyName => "pc-room-light";
	
	public DeviceReadState LastState { get; private set; }

	public delegate void StateChangedEventHandler(DeviceReadState state);

	public event StateChangedEventHandler StateChanged;

	private readonly IMqttClient _client;

	public PcRoomLight(IMqttClient client)
	{
		_client = client;
	}

	public async Task SetAsync(DeviceSetState state)
	{
		var settings = new JsonSerializerSettings();
		settings.Converters.Add(new StringEnumConverter());
		settings.NullValueHandling = NullValueHandling.Ignore;
		var json = JsonConvert.SerializeObject(state, settings);

		var message = new MqttApplicationMessageBuilder()
			.WithTopic($"zigbee2mqtt/{IeeeAddress}/set")
			.WithPayload(json)
			.Build();

		await _client.PublishAsync(message);
	}

	private TaskCompletionSource GetStateTcs;

	public async Task<DeviceReadState> GetAsync()
	{
		var json = JsonConvert.SerializeObject(new DeviceReadState());

		var message = new MqttApplicationMessageBuilder()
			.WithTopic($"zigbee2mqtt/{IeeeAddress}/get")
			.WithPayload(json)
			.Build();

		GetStateTcs = new();
		await _client.PublishAsync(message);

		var waitResult = await Task.WhenAny(GetStateTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
		return waitResult == GetStateTcs.Task ? LastState : null;
	}
	
	void IDevice.TriggerStateChanged(string payloadJson)
	{
		DeviceReadState state;
		try
		{
			state = JsonConvert.DeserializeObject<DeviceReadState>(payloadJson);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			return;
		}

		if (state == null)
			return;
		
		LastState = state;
		
		GetStateTcs?.TrySetResult();
		StateChanged?.Invoke(state);
	}

	public class DeviceSetState
	{
		[JsonProperty("brightness")]
		public int? Brightness { get; set; }
		
		[JsonProperty("power_on_behavior")]
		public PowerOnBehaviorEnum? PowerOnBehavior { get; set; }
		
		[JsonProperty("state")]
		public StateEnum? State { get; set; }
		
		[JsonProperty("effect")]
		public EffectEnum? Effect { get; set; }

		public enum PowerOnBehaviorEnum
		{
			[EnumMember(Value = "on")]
			On,
			[EnumMember(Value = "off")]
			Off,
			[EnumMember(Value = "previous")]
			Previous
		}

		public enum StateEnum
		{
			[EnumMember(Value = "ON")]
			On,
			[EnumMember(Value = "OFF")]
			Off,
			[EnumMember(Value = "TOGGLE")]
			Toggle
		}

		public enum EffectEnum
		{
			[EnumMember(Value = "blink")]
			Blink,
			[EnumMember(Value = "breathe")]
			Breathe,
			[EnumMember(Value = "okay")]
			Okay,
			[EnumMember(Value = "channel_change")]
			ChannelChange,
			[EnumMember(Value = "finish_effect")]
			FinishEffect,
			[EnumMember(Value = "stop_effect")]
			StopEffect,
		}
	}

	public class DeviceReadState
	{
		[JsonProperty("brightness")]
		public int? Brightness { get; set; }
		
		[JsonProperty("linkquality")]
		public bool? Linkquality { get; set; }
		
		[JsonProperty("power_on_behavior")]
		public PowerOnBehaviorEnum? PowerOnBehavior { get; set; }
		
		[JsonProperty("state")]
		public StateEnum? State { get; set; }

		public enum PowerOnBehaviorEnum
		{
			[EnumMember(Value = "on")]
			On,
			[EnumMember(Value = "off")]
			Off,
			[EnumMember(Value = "previous")]
			Previous
		}

		public enum StateEnum
		{
			[EnumMember(Value = "ON")]
			On,
			[EnumMember(Value = "OFF")]
			Off,
			[EnumMember(Value = "TOGGLE")]
			Toggle
		}
	}
}

public class PowerSwitch1 : IDevice
{
	public string IeeeAddress => "0xbc33acfffe4e7084";
	public string FriendlyName => "power-switch-1";

	public delegate void StateChangedEventHandler(DeviceReadState state);

	public event StateChangedEventHandler StateChanged;

	private readonly IMqttClient _client;

	public PowerSwitch1(IMqttClient client)
	{
		_client = client;
	}

	public async Task SetAsync(DeviceSetState state)
	{
		var settings = new JsonSerializerSettings();
		settings.Converters.Add(new StringEnumConverter());
		settings.NullValueHandling = NullValueHandling.Ignore;
		var json = JsonConvert.SerializeObject(state, settings);

		var message = new MqttApplicationMessageBuilder()
			.WithTopic($"zigbee2mqtt/{IeeeAddress}/set")
			.WithPayload(json)
			.Build();

		await _client.PublishAsync(message);
	}
	
	void IDevice.TriggerStateChanged(string payloadJson)
	{
		DeviceReadState state;
		try
		{
			state = JsonConvert.DeserializeObject<DeviceReadState>(payloadJson);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			return;
		}

		if (state == null)
			return;
		
		StateChanged?.Invoke(state);
	}

	public class DeviceSetState
	{
		[JsonProperty("state")]
		public StateEnum? State { get; set; }

		public enum StateEnum
		{
			[EnumMember(Value = "ON")]
			On,
			[EnumMember(Value = "OFF")]
			Off,
			[EnumMember(Value = "TOGGLE")]
			Toggle
		}
	}

	public class DeviceReadState
	{
		[JsonProperty("linkquality")]
		public bool? Linkquality { get; set; }
		
		[JsonProperty("state")]
		public StateEnum? State { get; set; }

		public enum StateEnum
		{
			[EnumMember(Value = "ON")]
			On,
			[EnumMember(Value = "OFF")]
			Off,
			[EnumMember(Value = "TOGGLE")]
			Toggle
		}
	}
}