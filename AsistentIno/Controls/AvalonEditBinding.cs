using System.Windows;
using ICSharpCode.AvalonEdit;

namespace AsistentIno.Controls;

public static class AvalonEditBinding
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(AvalonEditBinding),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextChanged));

    private static readonly DependencyProperty IsSubscribedProperty = DependencyProperty.RegisterAttached(
        "IsSubscribed",
        typeof(bool),
        typeof(AvalonEditBinding),
        new PropertyMetadata(false));

    public static string GetText(DependencyObject element) =>
        (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value) =>
        element.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextEditor editor)
            return;

        if (!(bool)editor.GetValue(IsSubscribedProperty))
        {
            editor.TextChanged += Editor_TextChanged;
            editor.SetValue(IsSubscribedProperty, true);
        }

        var text = e.NewValue as string ?? string.Empty;
        if (editor.Text != text)
            editor.Text = text;
    }

    private static void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (sender is TextEditor editor)
            editor.SetCurrentValue(TextProperty, editor.Text);
    }
}
