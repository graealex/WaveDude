using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace WaveDude.WPF.Controls;

/// <summary>
/// A text box for one number with up/down buttons (the usual spinner, asked for 2026-10-12): Up/Down
/// change the value by <see cref="Increment"/>, PageUp/PageDown by ten increments, the mouse wheel
/// does the same while the box has keyboard focus, and the two repeat buttons on the right do it with
/// the mouse. Every step clamps to <see cref="Minimum"/>..<see cref="Maximum"/>, writes the text back
/// through the <see cref="TextBox.Text"/> binding at once and selects it, so a source moves on the
/// picture with each key press. Typing stays plain text: the binding converts and the model clamps as
/// before, and Enter/Escape come from <see cref="EditBehavior"/> through the base text box style.
/// Numbers are parsed and written in the current culture, like the bindings. UI thread.
/// </summary>
public sealed class NumberBox : TextBox
{
	/// <summary>Identifies the <see cref="Minimum"/> property.</summary>
	public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
		nameof(Minimum), typeof(double), typeof(NumberBox), new PropertyMetadata(double.NegativeInfinity));

	/// <summary>Identifies the <see cref="Maximum"/> property.</summary>
	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
		nameof(Maximum), typeof(double), typeof(NumberBox), new PropertyMetadata(double.PositiveInfinity));

	/// <summary>Identifies the <see cref="Increment"/> property.</summary>
	public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(
		nameof(Increment), typeof(double), typeof(NumberBox), new PropertyMetadata(1.0));

	/// <summary>Steps per PageUp/PageDown and per wheel notch with Shift.</summary>
	private const int PageSteps = 10;

	/// <summary>Lowest value a step can reach (typed text is not limited here; the model clamps it).</summary>
	public double Minimum
	{
		get => (double)GetValue(MinimumProperty);
		set => SetValue(MinimumProperty, value);
	}

	/// <summary>Highest value a step can reach.</summary>
	public double Maximum
	{
		get => (double)GetValue(MaximumProperty);
		set => SetValue(MaximumProperty, value);
	}

	/// <summary>Amount of one Up/Down step; 1 for integers, a fraction for real-valued settings.</summary>
	public double Increment
	{
		get => (double)GetValue(IncrementProperty);
		set => SetValue(IncrementProperty, value);
	}

	/// <summary>The value the text holds, or null while it does not parse.</summary>
	public double? Value => double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ? value : null;

	/// <summary>
	/// Adds <paramref name="steps"/> increments to the value (a non-parsing text counts as 0), clamps,
	/// writes the text and pushes it into the binding. Rounded to the increment's precision so that
	/// 0.1 steps do not accumulate binary noise.
	/// </summary>
	public void Step(int steps)
	{
		if (IsReadOnly || !IsEnabled)
		{
			return;
		}
		double increment = Increment;
		double value = Math.Clamp((Value ?? 0.0) + steps * increment, Minimum, Maximum);
		int decimals = Math.Clamp(DecimalsOf(increment), 0, 10);
		value = Math.Round(value, decimals);
		Text = value.ToString(CultureInfo.CurrentCulture);
		GetBindingExpression(TextProperty)?.UpdateSource();
		SelectAll();
	}

	/// <summary>How many decimals the increment needs ("0.01" → 2); 0 for whole numbers.</summary>
	private static int DecimalsOf(double increment)
	{
		int decimals = 0;
		while (decimals < 10 && Math.Abs(increment * Math.Pow(10, decimals) - Math.Round(increment * Math.Pow(10, decimals))) > 1e-9)
		{
			decimals++;
		}
		return decimals;
	}

	/// <inheritdoc/>
	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (GetTemplateChild("PART_Up") is RepeatButton up)
		{
			up.Click += (_, _) => StepFromButton(1);
		}
		if (GetTemplateChild("PART_Down") is RepeatButton down)
		{
			down.Click += (_, _) => StepFromButton(-1);
		}
	}

	/// <summary>A button step also takes the focus, so the written value is visibly the box's and the wheel works afterwards.</summary>
	private void StepFromButton(int direction)
	{
		if (!IsKeyboardFocusWithin)
		{
			Focus();
		}
		Step(direction);
	}

	/// <inheritdoc/>
	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Handled)
		{
			return;
		}
		int steps = e.Key switch
		{
			Key.Up => 1,
			Key.Down => -1,
			Key.PageUp => PageSteps,
			Key.PageDown => -PageSteps,
			_ => 0,
		};
		if (steps != 0)
		{
			Step(steps);
			e.Handled = true;
		}
	}

	/// <inheritdoc/>
	protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
	{
		base.OnPreviewMouseWheel(e);
		if (e.Handled || !IsKeyboardFocusWithin || e.Delta == 0)
		{
			return;
		}
		int steps = Math.Sign(e.Delta) * ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? PageSteps : 1);
		Step(steps);
		e.Handled = true;
	}
}
