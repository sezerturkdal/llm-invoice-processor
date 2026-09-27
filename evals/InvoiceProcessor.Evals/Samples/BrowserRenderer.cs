using System.Diagnostics;

namespace InvoiceProcessor.Evals.Samples;

/// <summary>
/// Turns the HTML into PDF or PNG with a headless Chromium browser (Edge or Chrome), so sample
/// generation needs no PDF library. Only needed to regenerate samples; the eval itself uses the
/// committed files.
/// </summary>
public sealed class BrowserRenderer(string browserPath)
{
    // A4 at 150 dpi, a typical scan resolution.
    private const string PngWindowSize = "1240,1754";

    public static BrowserRenderer Find()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("EVALS_BROWSER"),
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            "/usr/bin/google-chrome",
            "/usr/bin/chromium",
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        ];

        var path = candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p))
            ?? throw new InvalidOperationException("No Edge or Chrome found. Set EVALS_BROWSER to a Chromium-based browser executable.");
        return new BrowserRenderer(path);
    }

    public async Task RenderAsync(string htmlPath, string outputPath, SampleFormat format)
    {
        var profile = Path.Combine(Path.GetTempPath(), "invoiceprocessor-evals-browser");
        var url = new Uri(Path.GetFullPath(htmlPath)).AbsoluteUri;

        List<string> args = ["--headless=new", "--disable-gpu", "--no-first-run", $"--user-data-dir={profile}"];
        if (format == SampleFormat.Pdf)
        {
            args.AddRange(["--no-pdf-header-footer", $"--print-to-pdf={Path.GetFullPath(outputPath)}"]);
        }
        else
        {
            args.AddRange(["--hide-scrollbars", $"--window-size={PngWindowSize}", $"--screenshot={Path.GetFullPath(outputPath)}"]);
        }
        args.Add(url);

        var startInfo = new ProcessStartInfo(browserPath) { RedirectStandardError = true, RedirectStandardOutput = true };
        args.ForEach(startInfo.ArgumentList.Add);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {browserPath}.");
        await process.WaitForExitAsync();

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"The browser did not write {outputPath}: {await process.StandardError.ReadToEndAsync()}");
        }
    }
}
