using System.ComponentModel;
using System.Reflection;
using System.Windows.Markup;
using System.Xaml;

namespace WaveDude.WPF.Markup;

/// <summary>
/// Base of the markup extensions that read a model property's <see cref="System.ComponentModel"/>
/// metadata, so labels and tooltips in the views come from the one place that defines them.
/// The member is written as <c>prefix:Type.Property</c>, resolved with the XAML namespaces in scope.
/// </summary>
public abstract class ModelMemberExtension : MarkupExtension
{
	/// <param name="member">Type and property, for example <c>model:Scene.Width</c>.</param>
	protected ModelMemberExtension(string member)
	{
		Member = member;
	}

	/// <summary>Type and property, for example <c>model:Scene.Width</c>.</summary>
	[ConstructorArgument("member")]
	public string Member { get; set; }

	/// <inheritdoc/>
	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		int dot = Member.LastIndexOf('.');
		if (dot <= 0 || dot == Member.Length - 1)
		{
			throw new System.Windows.Markup.XamlParseException($"'{Member}' is not of the form prefix:Type.Property.");
		}
		var resolver = (IXamlTypeResolver?)serviceProvider.GetService(typeof(IXamlTypeResolver))
			?? throw new InvalidOperationException("No XAML type resolver available.");
		Type type = resolver.Resolve(Member[..dot]);
		string name = Member[(dot + 1)..];
		PropertyInfo property = type.GetProperty(name)
			?? throw new System.Windows.Markup.XamlParseException($"'{type.Name}' has no property '{name}'.");
		return Provide(property);
	}

	/// <summary>Produces the value for the resolved property.</summary>
	protected abstract object Provide(PropertyInfo property);
}

/// <summary>The property's <see cref="DisplayNameAttribute"/>, or its name.</summary>
public sealed class LabelExtension : ModelMemberExtension
{
	/// <inheritdoc/>
	public LabelExtension(string member) : base(member)
	{
	}

	/// <inheritdoc/>
	protected override object Provide(PropertyInfo property) =>
		property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
}

/// <summary>The property's <see cref="DescriptionAttribute"/> text, or null without one (no tooltip).</summary>
public sealed class HintExtension : ModelMemberExtension
{
	/// <inheritdoc/>
	public HintExtension(string member) : base(member)
	{
	}

	/// <inheritdoc/>
	protected override object Provide(PropertyInfo property) =>
		property.GetCustomAttribute<DescriptionAttribute>()?.Description!;
}
