using System.Diagnostics;

namespace KeyTapLoop.Ui;

/// <summary>
/// A local window for testing. It counts the raw key messages Windows actually delivers to it and shows
/// what was typed, so you can see for yourself whether a key arrived. It is the only target where
/// KeyTapLoop can confirm delivery.
/// </summary>
internal sealed class PracticePadForm : Form, IMessageFilter
{
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int LogLimit = 300;

    private readonly TextBox typed = new();
    private readonly Label summary = Theme.Label("", size: 10f, style: FontStyle.Bold);
    private readonly ListBox log = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly HashSet<int> pendingDown = new();

    private int keyDowns;
    private int keyUps;
    private int tapCycles;
    private int repeats;

    /// <summary>Complete press-and-release cycles of non-modifier keys the pad received.</summary>
    public int TapCycles => tapCycles;

    public PracticePadForm()
    {
        Text = "KeyTapLoop practice pad";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 540);
        Padding = new Padding(24, 22, 24, 20);

        var heading = Theme.Label("Practice pad", size: 15f, style: FontStyle.Bold);
        var blurb = Theme.Label("Keys sent here show up below. This is the only target where KeyTapLoop can confirm a key arrived.", muted: true, size: 9f);
        blurb.MaximumSize = new Size(392, 0);

        typed.Multiline = true;
        typed.AcceptsReturn = true;
        typed.AcceptsTab = true;
        typed.BorderStyle = BorderStyle.FixedSingle;
        typed.BackColor = Theme.Surface;
        typed.ForeColor = Theme.Text;
        typed.Font = Theme.Body(13f);
        typed.ScrollBars = ScrollBars.Vertical;
        typed.AccessibleName = "Text received by the practice pad";
        typed.Size = new Size(392, 96);

        log.BorderStyle = BorderStyle.FixedSingle;
        log.BackColor = Theme.Surface;
        log.ForeColor = Theme.Text;
        log.Font = new Font("Consolas", 9f);
        log.IntegralHeight = false;
        log.AccessibleName = "Key events received";
        log.Size = new Size(392, 230);

        var clear = new Button { Text = "Clear", Size = new Size(84, 32), AccessibleName = "Clear the pad" };
        Theme.StyleButton(clear);
        clear.Click += (_, _) => { ResetCounters(); typed.Clear(); typed.Focus(); };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
        };
        heading.Margin = new Padding(0, 0, 0, 4);
        blurb.Margin = new Padding(0, 0, 0, 14);
        typed.Margin = new Padding(0, 0, 0, 12);
        summary.Margin = new Padding(0, 0, 0, 8);
        log.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.AddRange(new Control[] { heading, blurb, typed, summary, log, clear });
        Controls.Add(layout);

        UpdateSummary();
        Application.AddMessageFilter(this);
    }

    public void ResetCounters()
    {
        keyDowns = keyUps = tapCycles = repeats = 0;
        pendingDown.Clear();
        log.Items.Clear();
        clock.Restart();
        UpdateSummary();
    }

    public void FocusTypingArea() => typed.Focus();

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.StyleWindow(this);
        Theme.StyleNative(typed);
        Theme.StyleNative(log);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        typed.Focus();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(this);
        base.OnFormClosed(e);
    }

    /// <summary>Sees every key message headed for this window, before any control handles it.</summary>
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is not (WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP)) return false;
        if (Control.FromChildHandle(m.HWnd)?.FindForm() != this) return false;

        var vk = (int)(long)m.WParam;
        var down = m.Msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        var isRepeat = down && (((long)m.LParam) & 0x40000000) != 0;
        Record(vk, down, isRepeat);
        return false;
    }

    private static bool IsModifier(int vk) => vk is 0x10 or 0x11 or 0x12 or (>= 0xA0 and <= 0xA5) or 0x5B or 0x5C;

    private void Record(int vk, bool down, bool isRepeat)
    {
        if (down && isRepeat)
        {
            repeats++;
        }
        else if (down)
        {
            if (!IsModifier(vk)) { keyDowns++; pendingDown.Add(vk); }
            AddLog("down", vk);
        }
        else
        {
            if (!IsModifier(vk))
            {
                keyUps++;
                if (pendingDown.Remove(vk)) tapCycles++;
            }
            AddLog("up  ", vk);
        }
        UpdateSummary();
    }

    private void AddLog(string kind, int vk)
    {
        log.Items.Add($"{clock.Elapsed.TotalSeconds,7:0.000}s  {kind}  {(Keys)vk}");
        if (log.Items.Count > LogLimit) log.Items.RemoveAt(0);
        log.TopIndex = Math.Max(0, log.Items.Count - 1);
    }

    private void UpdateSummary()
    {
        var text = $"{tapCycles} tap cycles   ·   {keyDowns} down   ·   {keyUps} up";
        if (repeats > 0) text += $"   ·   {repeats} auto-repeat";
        summary.Text = text;
    }
}
