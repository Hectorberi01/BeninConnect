using Avalonia.Controls;

namespace BeninConnect.App.Shared.Controls;

/// <summary>Bouton d'action principal (style « Button.primary » de Styles.axaml).</summary>
public class PrimaryButton : Button
{
    public PrimaryButton() => Classes.Add("primary");

    protected override Type StyleKeyOverride => typeof(Button);
}

/// <summary>Bouton secondaire (style « Button.secondary »).</summary>
public class SecondaryButton : Button
{
    public SecondaryButton() => Classes.Add("secondary");

    protected override Type StyleKeyOverride => typeof(Button);
}
