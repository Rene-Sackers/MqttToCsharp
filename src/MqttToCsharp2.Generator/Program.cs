using MqttToCsharp2.Generator;
using Newtonsoft.Json;

var outputDir = Path.GetFullPath("../../../../MqttToCsharp2.Generated/");
var clearExtensions = new[] { ".cs", ".json" };
var filesToClear = Directory.GetFiles(outputDir).Where(f => clearExtensions.Contains(Path.GetExtension(f))).ToList();
Console.WriteLine("Clearing files:\n\t" + string.Join("\n\t", filesToClear));
filesToClear.ForEach(File.Delete);

Console.WriteLine("Getting devices");
var devicesJson = await DevicesJsonProvider.GetDevicesJson("192.168.1.123");
if (devicesJson == null)
	return;
await File.WriteAllTextAsync(Path.Combine(outputDir, "mqtt-devices.json"), JsonConvert.SerializeObject(JsonConvert.DeserializeObject(devicesJson), Formatting.Indented));

Console.WriteLine("Parsing devices");
var devices = DevicesFactory.ParseDevices(devicesJson);
await File.WriteAllTextAsync(Path.Combine(outputDir, "parsed-devices.json"), JsonConvert.SerializeObject(devices, Formatting.Indented));


Console.WriteLine("Generating code");
var generator = new CodeGenerator(devices);
const string @namespace = "MqttClient";
var code = generator.Generate(@namespace);

await File.WriteAllTextAsync(Path.Combine(outputDir, "Code.cs"), code);

Console.WriteLine("Done");


// await AssemblyGenerator.GenerateAssemblyAsync(code, Path.Combine(outputDir, "MqttClient.dll"));
//Directory.GetFiles(outputDir, "*.cs").ToList().ForEach(File.Delete);