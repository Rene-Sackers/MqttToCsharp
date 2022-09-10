using MqttToCsharp2.Generator.Helpers;

namespace MqttToCsharp2.Generator.ExtensionMethods;

public static class StringExtensions
{
	public static string SanitizeFunctionName(this string @string)
		=> SanitizationHelpers.SanitizeFunctionName(@string);

	public static string Indent(this string @string, int depth)
	{
		var indentation = new string('\t', depth);
		return @string.Replace("\n", "\n" + indentation);
	}
}