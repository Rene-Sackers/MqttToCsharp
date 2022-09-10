using System.Text.RegularExpressions;

namespace MqttToCsharp2.Generator.Helpers;

public static class SanitizationHelpers
{
	private static readonly Regex SanitizeFunctionNameRegex = new(@"(?:[^a-zA-Z0-9]+|^\s*)(\w)", RegexOptions.Compiled);

	public static string SanitizeFunctionName(string name)
	{
		name = SanitizeFunctionNameRegex.Replace(name, match => match.Groups[1].Value.ToUpperInvariant());
		return char.IsNumber(name[0]) ? "X" + name : name;
	}
}