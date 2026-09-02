namespace ImageToolkit.Domain.Models;

public sealed record ImageFileInfo(
    string SourcePath,
    string FileName,
    string Extension,
    long SizeBytes,
    PixelSize PixelSize,
    bool HasAlpha);
