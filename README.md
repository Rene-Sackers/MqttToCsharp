# MqttToCsharp

This project auto-generates C# code that allows you to easily create fully typed C# scripts co controll zigbee2mqtt devices over MQTT.

At the moment it's more of an experiment. Not intended for actual use.

`MqttToCsharp2.Generator` connects to the MQTT server and listens to the `zigbee2mqtt/bridge/devices` topic. This topic returns a JSON file with all devices known to z2m.

It parses these device and determines their names, input and ouput proprties and actions.

It then generates a C# boilerplate that defines each device and their properties, and puts the code into `Code.cs` in `MqttToCsharp2.Generated`.

`MqttToCsharp2.Consumer` references `MqttToCsharp2.Generated` and has some example scripts (these are based on devices in my own home, you'll obviously have to modify the scripts to try it out yourself).

## Try it yourself

In order to try this yourself, you'll need to modify `MqttToCsharp2.Generator/CodeGenerator.cs` and ensure it properly sets up a connection to the MQTT server in the generated `InitializeAsync()` method.

In `MqttToCsharp2.Generator/Program.cs` ensure the `DevicesJsonProvider.GetDevicesJson` method call has proper options to connect to your MQTT server as well.

## Example scripts:

```csharp
// Turn PowerSwitch1 on/off when pressing On/Off buttons on PcRoomRemote
Devices.PcRoomRemote.StateChanged += async s =>
{
	if (s.Action == Action1.On)
		await Devices.PowerSwitch1.SetAsync(new() { State = OnOffToggle.On });
	else if (s.Action == Action1.Off)
		await Devices.PowerSwitch1.SetAsync(new() { State = OnOffToggle.Off });
};
```

```csharp
// Set PowerSwitch1 state to PcRoomLight's state (sync on/off)
await Devices.PowerSwitch1.SetAsync(new() { State = (await Devices.PcRoomLight.GetAsync()).State });

// When PowerSwitch1 turns on or off, set PcRoomLight's state to the same value
Devices.PowerSwitch1.StateChanged += async s =>
{
	if (s.State.HasValue)
		await Devices.PcRoomLight.SetAsync(new() { State = s.State });
};
```