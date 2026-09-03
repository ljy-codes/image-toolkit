namespace ImageToolkit.Infrastructure.AI;

public sealed record AiModelManifest(
    string ModelId,
    string DisplayName,
    Uri DownloadUri,
    string FileName,
    long SizeBytes,
    string Sha256,
    string License,
    int InputWidth,
    int InputHeight)
{
    public const string PortraitModelId = "birefnet-portrait";
    public const string GeneralModelId = "birefnet-general";

    public static IReadOnlyList<AiModelManifest> Defaults { get; } =
    [
        new(
            PortraitModelId,
            "BiRefNet 高精度人像模型",
            new Uri(
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/BiRefNet-portrait-epoch_150.onnx"),
            "birefnet-portrait.onnx",
            972_666_916,
            "1BA1C8FF5A7BBFADC8D8D13FB11D7BE793F91F23D9D466549E37A854F6668F99",
            "BiRefNet MIT；ONNX 由 rembg 项目托管，安装包不包含模型文件",
            1024,
            1024),
        new(
            GeneralModelId,
            "BiRefNet 高精度通用模型",
            new Uri(
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/BiRefNet-general-epoch_244.onnx"),
            "birefnet-general.onnx",
            972_666_916,
            "58F621F00F5D756097615970A88A791584600DCF7C45B18A0A6267535A1EBD3C",
            "BiRefNet MIT；ONNX 由 rembg 项目托管，安装包不包含模型文件",
            1024,
            1024)
    ];
}
