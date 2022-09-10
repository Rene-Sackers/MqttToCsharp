using System.Text;

namespace MqttToCsharp2.Generator;

public class TabbedStringBuilder
{
	private readonly StringBuilder _stringBuilder;
	private readonly string _tabPrefix;

	public TabbedStringBuilder(StringBuilder stringBuilder, int tabDepth)
	{
		_stringBuilder = stringBuilder;
		_tabPrefix = new('\t', tabDepth);
	}

	public TabbedStringBuilder(int tabDepth)
	{
		_stringBuilder = new();
		_tabPrefix = new('\t', tabDepth);
	}

	public void AppendLine(string line) => _stringBuilder.AppendLine(_tabPrefix + line.Replace("\n", "\n" + _tabPrefix));

	public override string ToString() => _stringBuilder.ToString();
}