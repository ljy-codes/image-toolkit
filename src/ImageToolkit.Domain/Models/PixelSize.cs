namespace ImageToolkit.Domain.Models;

public readonly record struct PixelSize(int Width, int Height)
{
    public int ShortEdge => Math.Min(Width, Height);
}
