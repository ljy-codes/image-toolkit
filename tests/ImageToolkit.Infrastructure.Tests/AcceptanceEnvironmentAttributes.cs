namespace ImageToolkit.Infrastructure.Tests;

public sealed class AiAcceptanceFactAttribute : FactAttribute
{
    public AiAcceptanceFactAttribute()
    {
        if (!HasAll(
                "IMAGETOOLKIT_AI_MODEL_DIR",
                "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR"))
        {
            Skip = "未配置真实 AI 验收目录。";
        }
    }

    private static bool HasAll(params string[] names) =>
        names.All(name => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(name)));
}

public sealed class AiAcceptanceTheoryAttribute : TheoryAttribute
{
    public AiAcceptanceTheoryAttribute()
    {
        if (!HasAll(
                "IMAGETOOLKIT_AI_MODEL_DIR",
                "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR"))
        {
            Skip = "未配置真实 AI 验收目录。";
        }
    }

    private static bool HasAll(params string[] names) =>
        names.All(name => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(name)));
}

public sealed class CorpusAcceptanceFactAttribute : FactAttribute
{
    public CorpusAcceptanceFactAttribute()
    {
        if (!HasAll(
                "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR",
                "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR"))
        {
            Skip = "未配置多场景图片集验收目录。";
        }
    }

    private static bool HasAll(params string[] names) =>
        names.All(name => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(name)));
}

public sealed class CorpusAcceptanceTheoryAttribute : TheoryAttribute
{
    public CorpusAcceptanceTheoryAttribute()
    {
        if (!HasAll(
                "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR",
                "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR"))
        {
            Skip = "未配置多场景图片集验收目录。";
        }
    }

    private static bool HasAll(params string[] names) =>
        names.All(name => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(name)));
}

public sealed class AiCorpusAcceptanceFactAttribute : FactAttribute
{
    public AiCorpusAcceptanceFactAttribute()
    {
        if (!HasAll(
                "IMAGETOOLKIT_AI_MODEL_DIR",
                "IMAGETOOLKIT_ACCEPTANCE_CORPUS_DIR",
                "IMAGETOOLKIT_ACCEPTANCE_EVIDENCE_DIR"))
        {
            Skip = "未配置真实 AI 模型和多场景图片集验收目录。";
        }
    }

    private static bool HasAll(params string[] names) =>
        names.All(name => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(name)));
}
