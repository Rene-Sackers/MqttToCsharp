using MqttToCsharp2.Generator;
using Newtonsoft.Json;

var devicesJson = await DevicesJsonProvider.GetDevicesJson("192.168.1.123");
if (devicesJson == null)
	return;

var devices = DevicesFactory.ParseDevices(devicesJson);
var generator = new CodeGenerator(devices);
const string @namespace = "MqttClient";
var code = generator.Generate(@namespace);

var outputDir = Path.GetFullPath("./temp");
if (Directory.Exists(outputDir))
	Directory.Delete(outputDir, true);
Directory.CreateDirectory(outputDir);

var testFilePath = Path.Combine(outputDir, "Code.cs");
await File.WriteAllTextAsync(testFilePath, code);

await File.WriteAllTextAsync(Path.Combine(outputDir, "mqtt-devices.json"), JsonConvert.SerializeObject(JsonConvert.DeserializeObject(devicesJson), Formatting.Indented));
await File.WriteAllTextAsync(Path.Combine(outputDir, "parsed-devices.json"), JsonConvert.SerializeObject(devices, Formatting.Indented));

await AssemblyGenerator.GenerateAssemblyAsync(code, Path.Combine(outputDir, "MqttClient.dll"));
//Directory.GetFiles(outputDir, "*.cs").ToList().ForEach(File.Delete);