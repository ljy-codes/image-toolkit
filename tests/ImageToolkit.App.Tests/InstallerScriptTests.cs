namespace ImageToolkit.App.Tests;

public sealed class InstallerScriptTests
{
    [Fact]
    public void Uninstaller_requires_explicit_confirmation_before_deleting_user_data()
    {
        var path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "installer",
                "ImageToolkit.iss"));
        var script = File.ReadAllText(path);

        Assert.Contains(
            "procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);",
            script);
        Assert.Contains(
            "(CurUninstallStep = usUninstall) and not UninstallSilent",
            script);
        Assert.Contains("MB_YESNO or MB_DEFBUTTON2", script);
        Assert.Contains(
            "ExpandConstant('{localappdata}\\ImageToolkit')",
            script);
        Assert.Contains(
            "(CurUninstallStep = usPostUninstall) and DeleteUserData",
            script);
        Assert.Contains(
            "DelTree(UserDataDirectory, True, True, True)",
            script);
        Assert.DoesNotContain("[UninstallDelete]", script);
    }
}
