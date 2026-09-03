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
    public const string PortraitModelId = "u2net-human-seg";
    public const string GeneralModelId = "u2net";

    public static IReadOnlyList<AiModelManifest> Defaults { get; } =
    [
        new(
            PortraitModelId,
            "人像抠图模型",
            new Uri(
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net_human_seg.onnx"),
            "u2net_human_seg.onnx",
            175_997_641,
            "01EB6A29A5C4D8EDB30B56ADAD9BB3A2A0535338E480724A213E0ACFD2D1C73C",
            "U-2-Net Apache-2.0；ONNX 由 rembg 项目托管，安装包不包含模型文件",
            320,
            320),
        new(
            GeneralModelId,
            "商品 / 通用物体模型",
            new Uri(
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx"),
            "u2net.onnx",
            175_997_641,
            "8D10D2F3BB75AE3B6D527C77944FC5E7DCD94B29809D47A739A7A728A912B491",
            "U-2-Net Apache-2.0；ONNX 由 rembg 项目托管，安装包不包含模型文件",
            320,
            320)
    ];
}
