using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Seer.Models;

namespace Seer;

/// <summary>
/// Renders the main window off-screen to PNG files and exits, without ever
/// showing a window or needing a desktop session.
///
/// This is the only way to check the UI without the lead developer running
/// the app: a clean build proves nothing about XAML, because WPF resolves
/// StaticResource keys and template triggers when the theme *loads*, not when
/// it compiles. A missing key throws here, so a render that succeeds is also
/// a proof that every resource key in the visual tree resolves.
///
/// Inert unless "--render-shot" is passed, so it costs a shipped build
/// nothing but the few lines below.
///
/// Usage: Seer.exe --render-shot [outputDirectory]
/// </summary>
internal static class RenderShot
{
    public const string Switch = "--render-shot";

    /// <summary>
    /// Polls to run before rendering. Enough for a visible trend line without
    /// making the shot slow — each one enumerates every process.
    /// </summary>
    private const int WarmupPolls = 30;

    /// <summary>Sizes worth checking: the window's MinWidth/MinHeight, and a roomy desktop.</summary>
    private static readonly (int Width, int Height, string Name)[] Sizes =
    {
        (640, 400, "min"),
        (900, 1100, "default"),
        (1400, 1000, "wide")
    };

    public static bool IsRequested(string[] args)
        => Array.Exists(args, a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Renders every size and returns the directory written to. Any exception
    /// is deliberately left to propagate — a failure to render is the signal.
    /// </summary>
    public static string Run(string[] args)
    {
        var outputDir = ResolveOutputDirectory(args);
        Directory.CreateDirectory(outputDir);

        foreach (var (width, height, name) in Sizes)
        {
            // A fresh window per size: layout caches measurements, and
            // re-arranging one window at several sizes can leave stale
            // desired-size values behind.
            var window = new MainWindow();

            // Fill the trend history before measuring. The constructor polls
            // once, which leaves each chart with a single point and no line,
            // so the very thing a chart change needs to be checked against
            // would be missing from the shot. Every sample here is a real
            // sensor read; only the spacing is artificial.
            for (var i = 0; i < WarmupPolls; i++)
                window.UpdatePanels();

            // Render the CONTENT, not the Window. An unshown Window has no
            // HwndSource and does not render reliably through
            // RenderTargetBitmap; its content tree does.
            if (window.Content is not FrameworkElement root)
                throw new InvalidOperationException("MainWindow.Content is not a FrameworkElement.");

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            // Two passes onto one surface: RenderTargetBitmap composites
            // successive Render calls, so the window ground goes down first
            // and the content over it. Wrapping the content in a VisualBrush
            // instead looked equivalent but left everything below the panels
            // unpainted.
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

            var ground = new DrawingVisual();
            using (var dc = ground.RenderOpen())
                dc.DrawRectangle(GroundBrush(window), null, new Rect(0, 0, width, height));

            bitmap.Render(ground);
            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var path = Path.Combine(outputDir, $"seer-{name}-{width}x{height}.png");
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        RenderOsd(outputDir);

        return outputDir;
    }

    /// <summary>
    /// The desktop overlay, in both lock states. It is a separate window that
    /// the main shot never touched, which is how it kept a hardcoded accent
    /// through a retheme: nothing rendered it, so nothing caught the drift.
    /// Readings are synthetic — the point is the chrome and the severity
    /// colours, and one of each has to be on screen to be checked.
    /// </summary>
    private static void RenderOsd(string outputDir)
    {
        var cpu = new CpuMetrics { Temperature = 91f, TotalLoad = 96f, Clock = 4820f, Power = 142f };
        var gpu = new GpuMetrics { Temperature = 74f, Load = 62f, VramUsedGb = 6.2f, VramTotalGb = 12f };
        var mem = new MemoryMetrics { UsedGb = 26.3f, AvailableGb = 5.6f, Load = 82.4f };

        foreach (var locked in new[] { false, true })
        {
            var settings = new AppSettings { ShowOsd = true, LockOsd = locked };
            var osd = new OsdWindow(settings);
            osd.UpdateStats(cpu, gpu, mem);

            if (osd.Content is not FrameworkElement content)
                throw new InvalidOperationException("OsdWindow.Content is not a FrameworkElement.");

            // The strip sizes itself to whichever metrics are selected, so
            // measure it unconstrained and take the size it asks for.
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = (int)Math.Ceiling(content.DesiredSize.Width);
            var height = (int)Math.Ceiling(content.DesiredSize.Height);

            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();

            // A mid-grey ground: the overlay is transparent by design, and on
            // a transparent surface neither the ground tint nor the locked
            // state's absence of chrome can be judged.
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var ground = new DrawingVisual();
            using (var dc = ground.RenderOpen())
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x42)), null, new Rect(0, 0, width, height));

            bitmap.Render(ground);
            bitmap.Render(content);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var path = Path.Combine(outputDir, $"seer-osd-{(locked ? "locked" : "unlocked")}.png");
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }

    /// <summary>
    /// The window's own ground. The root border's background is the dot
    /// lattice, which is transparent between its dots, so without this every
    /// gap renders as a hole rather than as the app's near-black.
    /// </summary>
    private static Brush GroundBrush(Window window)
        => window.Background
           ?? Application.Current?.TryFindResource("SeerBackground") as Brush
           ?? Brushes.Black;

    private static string ResolveOutputDirectory(string[] args)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));

        // The argument after the switch, when it isn't another switch.
        if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            return args[index + 1];

        // Never a relative path: the UAC relaunch can hand the process
        // C:\Windows\System32 as its working directory.
        return Path.Combine(Path.GetTempPath(), "seer-render");
    }
}
