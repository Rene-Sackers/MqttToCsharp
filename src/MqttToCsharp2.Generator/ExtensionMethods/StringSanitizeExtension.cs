using MqttToCsharp2.Generator.Helpers;

namespace MqttToCsharp2.Generator.ExtensionMethods;

public static class StringSanitizeExtension
{
	public static string SanitizeFunctionName(this string @string)
		=> SanitizationHelpers.SanitizeFunctionName(@string);
}