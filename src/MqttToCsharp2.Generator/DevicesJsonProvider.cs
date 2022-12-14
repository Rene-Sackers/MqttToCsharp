using System.Text;
using MQTTnet;
using MQTTnet.Client;

namespace MqttToCsharp2.Generator;

public static class DevicesJsonProvider
{
	public static Task<string> GetDevicesJson(string mqttServeIp)
		=> GetDevicesJson(new MqttClientOptionsBuilder().WithTcpServer(mqttServeIp).Build());
	
	public static async Task<string> GetDevicesJson(MqttClientOptions options)
	{
		var factory = new MqttFactory();

		using var client = factory.CreateMqttClient();
		var tcs = new TaskCompletionSource<string>();

		client.ApplicationMessageReceivedAsync += e =>
		{
			var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

			tcs.SetResult(payloadJson);
			return Task.CompletedTask;
		};

		var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
			.WithTopicFilter(f => f.WithTopic("zigbee2mqtt/bridge/devices"))
			.Build();

		await client.ConnectAsync(options);
		await client.SubscribeAsync(subscribeOptions);

		var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
		if (completedTask != tcs.Task)
		{
			Console.WriteLine("Failed to get devices from MQTT");
			await client.DisconnectAsync();
			return null;
		}

		await client.DisconnectAsync();
		return tcs.Task.Result;
	}
}