using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MqttToCsharp2.Generator;

public static class AssemblyGenerator
{
	public static async Task GenerateAssemblyAsync(string code, string dllPath)
	{
		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
			MetadataReference.CreateFromFile("C:\\Program Files\\dotnet\\packs\\Microsoft.NETCore.App.Ref\\6.0.1\\ref\\net6.0\\System.Runtime.dll"), // Works
			// MetadataReference.CreateFromFile("C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\6.0.5\\System.Runtime.dll"), // <- Does not work
			// MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // <- Does not work
		};

		// Breaks working references from above
		// var referencedAssemblies = Assembly.GetEntryAssembly().GetReferencedAssemblies();
		// foreach(var referencedAssembly in referencedAssemblies)
		// {
		// 	var loadedAssembly = Assembly.Load(referencedAssembly);   
		//
		// 	references.Add(MetadataReference.CreateFromFile(loadedAssembly.Location)); 
		// }

		var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
		var parsedCode = SyntaxFactory.ParseSyntaxTree(code, parseOptions);

		var compilationOptions = new CSharpCompilationOptions(
			OutputKind.DynamicallyLinkedLibrary,
			optimizationLevel: OptimizationLevel.Release,
			platform: Platform.AnyCpu,
			assemblyIdentityComparer: AssemblyIdentityComparer.Default);

		var compiled = CSharpCompilation.Create(
			Path.GetFileNameWithoutExtension(dllPath),
			new[] { parsedCode },
			references: references,
			compilationOptions
		);

		var outputFilePath = Path.Combine(dllPath);
		await using var outputFile = File.Create(outputFilePath);
		var emitResult = compiled.Emit(outputFile);
		if (emitResult.Success)
		{
			await outputFile.FlushAsync();
			Console.WriteLine("Success: " + outputFilePath);
			return;
		}

		foreach (var diagnostic in emitResult.Diagnostics)
			await Console.Error.WriteLineAsync(diagnostic.GetMessage());
	}
}