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

	public static string ConcatStrings(this IEnumerable<string> strings) => string.Join('\n', strings);

	public static bool IsAllCaps(this string @string) => @string == @string.ToUpperInvariant();

	public static string CapitalizeFirstLetterOnly(this string @string) => @string[..1] + @string[1..].ToLowerInvariant();
}