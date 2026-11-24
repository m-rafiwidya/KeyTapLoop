using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using KeyTapLoop.Core;
using KeyTapLoop.Ui;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace KeyTapLoop.UiTests;

/// <summary>Runs only on Windows, where the windows can actually be created.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Needs Windows";
    }
}

/// <summary>
/// Renders the real windows in both themes, saves screenshots to <c>ui-screenshots/</c>, and checks for the
/// kinds of mistakes that only show up on screen: black patches, content spilling out of its container, and
/// colours that didn't switch with the theme.
/// </summary>
public class RenderTests : IDisposable
{
    private readonly string settingsFile = Path.Combine(Path.GetTempPath(), "keytaploop-ui-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { File.Delete(settingsFile); } catch (IOException) { }
        Theme.Choose("Light");
    }

    // ---- scenarios ---------------------------------------------------------------------------

    [WindowsFact]
    public void Main_window_looks_right_in_light_and_dark() => OnStaThread(() =>
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            Theme.Choose(mode);
            using var form = new MainForm(settingsFile);
            var shot = Capture(form, $"main-{mode.ToLowerInvariant()}");
            AssertHealthy(form, shot, mode);
            form.Close();
        }
    });

    [WindowsFact]
    public void Error_messages_are_visible_next_to_their_fields_in_both_themes() => OnStaThread(() =>
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            Theme.Choose(mode);
            using var form = new MainForm(settingsFile);
            form.Show();
            Find<TextField>(form, "Period in milliseconds, start to start").Text = "abc";
            Find<TextField>(form, "Key-down time in milliseconds").Text = "";
            var shot = Capture(form, $"main-{mode.ToLowerInvariant()}-errors");

            var errors = All(form).OfType<Label>().Where(l => l.Visible && l.ForeColor.ToArgb() == Theme.Danger.ToArgb()).ToList();
            Assert.True(errors.Count >= 2, $"{mode}: expected error text next to the bad fields, found {errors.Count}");
            AssertHealthy(form, shot, mode + " errors");
            form.Close();
        }
    });

    [WindowsFact]
    public void Hold_and_any_window_modes_render_cleanly() => OnStaThread(() =>
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            Theme.Choose(mode);
            using var form = new MainForm(settingsFile);
            form.Show();
            Find<RadioButton>(form, "Hold mode: keep the key down").Checked = true;
            Find<RadioButton>(form, "Send keys to whichever window is in front").Checked = true;
            var shot = Capture(form, $"main-{mode.ToLowerInvariant()}-hold-any");
            AssertHealthy(form, shot, mode + " hold/any");
            form.Close();
        }
    });

    [WindowsFact]
    public void Switching_theme_live_recolours_everything() => OnStaThread(() =>
    {
        Theme.Choose("Light");
        using var form = new MainForm(settingsFile);
        form.Show();
        Application.DoEvents();

        Theme.Switch(Theme.Dark, form);
        Application.DoEvents();
        var shot = Capture(form, "main-switched-light-to-dark");

        Assert.Equal(Theme.Dark.Background.ToArgb(), ClientPixel(form, shot, 2, 2).ToArgb());
        AssertHealthy(form, shot, "switched to dark");

        Theme.Switch(Theme.Light, form);
        Application.DoEvents();
        var back = Capture(form, "main-switched-dark-to-light");
        Assert.Equal(Theme.Light.Background.ToArgb(), ClientPixel(form, back, 2, 2).ToArgb());
        AssertHealthy(form, back, "switched back to light");
        form.Close();
    });

    [WindowsFact]
    public void Settings_window_looks_right_in_both_themes() => OnStaThread(() =>
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            Theme.Choose(mode);
            using var form = new SettingsForm(AppSettings.DefaultStartHotkey, AppSettings.DefaultStopHotkey,
                AppSettings.DefaultToggleHotkey, "Auto", soundCues: true, useScanCodes: true);
            var shot = Capture(form, $"settings-{mode.ToLowerInvariant()}");
            AssertHealthy(form, shot, mode + " settings");
            form.Close();
        }
    });

    [WindowsFact]
    public void Practice_pad_looks_right_in_both_themes() => OnStaThread(() =>
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            Theme.Choose(mode);
            using var form = new PracticePadForm();
            var shot = Capture(form, $"pad-{mode.ToLowerInvariant()}");
            AssertHealthy(form, shot, mode + " pad");
            form.Close();
        }
    });

    [WindowsFact]
    public void The_drawn_checkbox_never_paints_black_even_on_a_transparent_panel() => OnStaThread(() =>
    {
        foreach (var mode in new[] { "Light", "Dark" })
        {
            Theme.Choose(mode);
            using var host = new Form { BackColor = Theme.Background, ClientSize = new Size(320, 80), StartPosition = FormStartPosition.Manual };
            var transparent = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var row = new CheckRow { Text = "Stop after", Checked = true };
            var off = new CheckRow { Text = "Not ticked" };
            transparent.Controls.Add(row);
            transparent.Controls.Add(off);
            host.Controls.Add(transparent);
            var shot = Capture(host, $"checkbox-{mode.ToLowerInvariant()}");
            Assert.Equal(0, PureBlackPixels(host, shot));
            host.Close();
        }
    });

    // ---- helpers ---------------------------------------------------------------------------

    private static string OutputDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        var path = Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "ui-screenshots");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Shows the window, lets it lay out, and saves a picture of it.</summary>
    private static Bitmap Capture(Form form, string name)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(40, 40);
        form.Show();
        Application.DoEvents();
        form.PerformLayout();
        Application.DoEvents();

        var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
        bmp.Save(Path.Combine(OutputDirectory(), name + ".png"), ImageFormat.Png);
        return bmp;
    }

    /// <summary>The part of the picture that is the window's own content, without the title bar and frame.</summary>
    private static Rectangle ClientArea(Form form)
    {
        var origin = form.PointToScreen(Point.Empty);
        return new Rectangle(origin.X - form.Location.X, origin.Y - form.Location.Y, form.ClientSize.Width, form.ClientSize.Height);
    }

    private static Color ClientPixel(Form form, Bitmap shot, int x, int y)
    {
        var area = ClientArea(form);
        return Color.FromArgb(255, shot.GetPixel(area.X + x, area.Y + y));
    }

    private static int PureBlackPixels(Form form, Bitmap shot)
    {
        var area = Rectangle.Intersect(ClientArea(form), new Rectangle(0, 0, shot.Width, shot.Height));
        var count = 0;
        for (var y = area.Top; y < area.Bottom; y++)
            for (var x = area.Left; x < area.Right; x++)
            {
                var p = shot.GetPixel(x, y);
                if (p.R == 0 && p.G == 0 && p.B == 0) count++;
            }
        return count;
    }

    private static void AssertHealthy(Form form, Bitmap shot, string label)
    {
        // Nothing in either theme is pure black, so any black means something was painted wrongly.
        var black = PureBlackPixels(form, shot);
        Assert.True(black == 0, $"{label}: {black} pure-black pixels. Something painted a black background.");

        // Every visible control sits inside its container, so nothing is clipped or spilling out.
        var spilling = new List<string>();
        foreach (var c in All(form))
        {
            if (!c.Visible || c.Parent is null || c.Parent is Form) continue;
            var inner = Rectangle.Inflate(c.Parent.ClientRectangle, 2, 2);
            if (!inner.Contains(c.Bounds))
                spilling.Add($"{c.GetType().Name} '{(string.IsNullOrEmpty(c.AccessibleName) ? c.Text : c.AccessibleName)}' {c.Bounds} outside {c.Parent.GetType().Name} {c.Parent.ClientRectangle}");
        }
        Assert.True(spilling.Count == 0, $"{label}: controls spill outside their container:\n" + string.Join("\n", spilling));
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var inner in All(child)) yield return inner;
        }
    }

    private static T Find<T>(Control root, string accessibleName) where T : Control =>
        All(root).OfType<T>().First(c => c.AccessibleName == accessibleName);

    /// <summary>Windows Forms needs a single-threaded apartment, which test threads aren't by default.</summary>
    private static void OnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
