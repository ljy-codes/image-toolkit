using ImageToolkit.Domain.Options;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Interfaces;

public interface IOutputPathResolver
{
    string Resolve(string sourcePath, OutputOptions options, string outputExtension);

    string Resolve(
        ImageImportEntry entry,
        OutputOptions options,
        string outputExtension) =>
        Resolve(entry.SourcePath, options, outputExtension);
}
