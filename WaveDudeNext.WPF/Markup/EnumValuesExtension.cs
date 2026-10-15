using System.Windows.Markup;
using System.Xaml;

namespace WaveDudeNext.WPF.Markup;

/// <summary>
/// All values of an enum, for an <c>ItemsSource</c>. The type is written with its XAML prefix,
/// for example <c>{meta:EnumValues model:SceneMode}</c>.
/// </summary>
public sealed class EnumValuesExtension : MarkupExtension
{
	/// <param name="type">The enum type with its XAML prefix.</param>
	public EnumValuesExtension(string type)
	{
		Type = type;
	}

	/// <summary>The enum type with its XAML prefix.</summary>
	[ConstructorArgument("type")]
	public string Type { get; set; }

	/// <inheritdoc/>
	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		var resolver = (IXamlTypeResolver?)serviceProvider.GetService(typeof(IXamlTypeResolver))
			?? throw new InvalidOperationException("No XAML type resolver available.");
		Type type = resolver.Resolve(Type);
		if (!type.IsEnum)
		{
			throw new System.Windows.Markup.XamlParseException($"'{Type}' is not an enum.");
		}
		return Enum.GetValues(type);
	}
}
