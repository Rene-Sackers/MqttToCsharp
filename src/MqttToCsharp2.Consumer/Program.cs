using MqttToCsharp;

await Devices.InitializeAsync();

var motionSensor = Devices.HallwayMotion;
var light = Devices.PcRoomLight;

// light.StateChanged += async s =>
// {
// 	switch (s.State)
// 	{
// 		case OnOffToggle.On:
// 			await Devices.PowerSwitch1.SetAsync(new() { State = OnOffToggle.On });
// 			break;
// 		case OnOffToggle.Off:
// 			await Devices.PowerSwitch1.SetAsync(new() { State = OnOffToggle.Off });
// 			break;
// 	}
// };

Devices.PcRoomRemote.StateChanged += async s =>
{
	if (s.Action.HasValue)
		Console.WriteLine("Action: " + s.Action);
};

await Devices.PowerSwitch1.SetAsync(new() { State = (await Devices.PcRoomLight.GetAsync()).State });
Devices.PowerSwitch1.StateChanged += async s =>
{
	if (s.State.HasValue)
		await Devices.PcRoomLight.SetAsync(new() { State = s.State });
};

Console.WriteLine("Light is currently: " + (await light.GetAsync()).State);

var turnOffAfterNoMotionFor = TimeSpan.FromMinutes(2);
var hallwayLightTurnOffTimer = new Timer(async _ =>
{
	if (light.LastState.State != OnOffToggle.On)
		return;
	
	await light.SetAsync(new() { State = OnOffToggle.Off });
	Console.WriteLine("Turning light off");
});
var lastOccupancyState = false;

motionSensor.StateChanged += async s =>
{
	if (s.Occupancy != lastOccupancyState)
	{
		lastOccupancyState = s.Occupancy.Value;
		Console.WriteLine($"Occupancy changed to: {s.Occupancy}");
	}

	if (s.Occupancy == true)
	{
		hallwayLightTurnOffTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		Console.WriteLine("Set timer to infinite");
	}
	else
	{
		hallwayLightTurnOffTimer.Change(turnOffAfterNoMotionFor, Timeout.InfiniteTimeSpan);
		Console.WriteLine($"Set timer to {turnOffAfterNoMotionFor.TotalMinutes} minutes");
	}

	await light.SetAsync(new() { State = OnOffToggle.Off });
	Console.WriteLine("Turning light on");
};

await Task.Delay(-1);