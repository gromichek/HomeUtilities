namespace HomeUtilities.ViewModels.Tests;

public sealed class FileErrorLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hu-errlog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private FileErrorLog Create() => new(Path.Combine(_dir, "logs", "errors.log"));

    [Fact]
    public void Append_CreatesTheFolderAndKeepsSourceAndException()
    {
        var log = Create();

        log.Append("Editor.Save", new InvalidOperationException("boom"));

        var text = log.ReadAll();
        Assert.Contains("Editor.Save", text);
        Assert.Contains("InvalidOperationException: boom", text);
    }

    [Fact]
    public void ReadAll_OfAMissingLog_IsEmpty_AndClearRemovesTheFile()
    {
        var log = Create();
        Assert.Equal(string.Empty, log.ReadAll());

        log.Append("x", new Exception("e"));
        log.Clear();

        Assert.Equal(string.Empty, log.ReadAll());
        log.Clear();
    }

    [Fact]
    public void Log_IsTrimmedToTheNewestEntries_WhenItExceedsOneMegabyte()
    {
        var log = Create();
        var big = new string('x', 100_000);
        for (var i = 0; i < 15; i++)
        {
            log.Append("entry-" + i, new Exception(big));
        }

        var text = log.ReadAll();

        Assert.True(text.Length <= FileErrorLog.MaxBytes);
        Assert.Contains("entry-14", text);
        Assert.DoesNotContain("entry-0\n", text);
        Assert.StartsWith("[", text);
    }

    [Fact]
    public void Append_ToAnUnwritablePath_DoesNotThrow()
    {
        var log = new FileErrorLog(Path.Combine(_dir, "\0bad", "errors.log"));

        log.Append("x", new Exception("e"));

        Assert.Equal(string.Empty, log.ReadAll());
    }
}
