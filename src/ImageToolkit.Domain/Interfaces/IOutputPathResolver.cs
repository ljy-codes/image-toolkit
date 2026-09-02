using ImageToolkit.Domain.Options;

namespace ImageToolkit.Domain.Interfaces;

public interface IOutputPathResolver
{
    string Resolve(string sourcePath, OutputOptions options, string outputExtension);
}
