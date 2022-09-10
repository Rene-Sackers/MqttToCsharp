using MqttToCsharp2.Generator.ExtensionMethods;
using MqttToCsharp2.Generator.Helpers;
using MqttToCsharp2.Generator.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MqttToCsharp2.Generator;

public static class DevicesFactory
{
	public static List<Device> ParseDevices(string json)
	{
		var reader = new JsonTextReader(new StringReader(json));
		return JArray.Load(reader).Select(ParseDevice).Where(d => d != null).ToList();
	}

	private static Device ParseDevice(JToken deviceJson)
	{
		var friendlyName = deviceJson.Value<string>("friendly_name");
		var ieeeAddress = deviceJson.Value<string>("ieee_address");
		var manufacturer = deviceJson.Value<string>("manufacturer");
		var modelId = deviceJson.Value<string>("model_id");
		var definition = deviceJson.Value<JObject>("definition");

		if (definition == null)
			return null;

		var device = new Device
		{
			Name = friendlyName,
			SanitizedName = friendlyName.SanitizeFunctionName(),
			IeeeAddress = ieeeAddress,
			Manufacturer = manufacturer,
			ModelId = modelId,
			Description = definition.Value<string>("description")
		};

		var properties = definition.Value<JArray>("exposes");
		if (properties == null)
			return device;

		foreach (var property in properties)
		{
			var features = property.Value<JArray>("features");
			if (features == null)
			{
				ParseProperty(device, property);
				continue;
			}

			foreach (var feature in features)
			{
				ParseProperty(device, feature);
			}
		}

		return device;
	}

	private static void ParseProperty(Device device, JToken propertyJson)
	{
		var type = propertyJson.Value<string>("type");
		var description = propertyJson.Value<string>("description");
		var name = propertyJson.Value<string>("name");
		var access = propertyJson.Value<int>("access");

		DeviceProperty deviceProperty;
		switch (type)
		{
			case "binary":
				var onValue = propertyJson.Value<string>("value_on");
				var offValue = propertyJson.Value<string>("value_off");
				var toggleValue = propertyJson.Value<string>("value_toggle");

				var enumValue = new EnumValue { Values = new() };

				if (!string.IsNullOrWhiteSpace(onValue))
					enumValue.Values.Add(onValue);

				if (!string.IsNullOrWhiteSpace(offValue))
					enumValue.Values.Add(offValue);

				if (!string.IsNullOrWhiteSpace(toggleValue))
					enumValue.Values.Add(toggleValue);

				deviceProperty = enumValue;
				break;
			case "numeric":
				deviceProperty = new NumericValue
				{
					MinValue = propertyJson.Value<long>("value_min"),
					MaxValue = propertyJson.Value<long>("value_max")
				};
				break;
			case "enum":
				deviceProperty = new EnumValue
				{
					Values = propertyJson.Value<JArray>("values")?.Select(a => a.Value<string>()).ToList()
				};
				break;
			default:
				Console.WriteLine("Unknown expose type: " + type);
				return;
		}

		deviceProperty.Name = name;
		deviceProperty.Description = description;
		deviceProperty.Type = type;

		switch (access)
		{
			case 1: // readonly?
				device.OutputValues.Add(deviceProperty);
				break;
			case 2: // trigger an event/action on the device?
				device.Actions.Add(deviceProperty);
				break;
			case 7: // settable value?
				device.SettableValues.Add(deviceProperty);
				break;
		}
	}
}