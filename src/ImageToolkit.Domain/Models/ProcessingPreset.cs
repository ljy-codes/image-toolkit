namespace ImageToolkit.Domain.Models;

public sealed record ProcessingPreset(string Name, ProcessingRequest Request)
{
    public override string ToString() => Name;
}
