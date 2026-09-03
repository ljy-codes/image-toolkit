namespace ImageToolkit.App.Models;

public sealed record ChoiceOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
