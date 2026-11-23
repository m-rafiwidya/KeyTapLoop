using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using KeyTapLoop.Core;

namespace KeyTapLoop.Ui;

/// <summary>A single-line input with a hairline border that turns green on focus and red on error.</summary>
internal sealed class TextField : Panel
{
    private readonly TextBox box;
    private bool hasError;

    public event EventHandler? ValueChanged;

    public TextField(int width, string accessibleName)
    {
        Size = new Size(width, 32);
        Margin = Padding.Empty;
        BackColor = Theme.Surface;
        Padding = new Padding(9, 7, 9, 0);
        AccessibleName = accessibleName;

        box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.Body(),
            AccessibleName = accessibleName,
            MaxLength = 12,
        };
        box.TextChanged += (_, _) => ValueChanged?.Invoke(this, EventArgs.Empty);
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => Invalidate();
        box.Enter += (_, _) => box.SelectAll();
        Controls.Add(box);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    [AllowNull]
    public override string Text { get => box.Text; set => box.Text = value ?? ""; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasError
    {
        get => hasError;
        set { if (hasError != value) { hasError = value; Invalidate(); } }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        BackColor = Enabled ? Theme.Surface : Theme.Background;
        box.BackColor = BackColor;
        box.ForeColor = Enabled ? Theme.Text : Theme.Disabled;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var color = hasError ? Theme.Danger : box.Focused ? Theme.Accent : Theme.Line;
        using var pen = new Pen(color, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// A button that, when clicked, waits for the next key combination and reports it.
/// Escape cancels; Tab cancels and moves on, so keyboard users are never trapped.
/// </summary>
internal sealed class CaptureButton : Button
{
    private string idleText;
    private long ignoreClicksUntil;

    public bool Listening { get; private set; }
    public event Action<KeyChord>? Captured;

    public CaptureButton(string idleText)
    {
        this.idleText = idleText;
        Text = idleText;
        Theme.StyleButton(this);
        Click += (_, _) =>
        {
            if (Environment.TickCount64 < ignoreClicksUntil) return;
            if (Listening) StopListening(); else StartListening();
        };
        LostFocus += (_, _) => StopListening();
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ShownText
    {
        set { idleText = value; if (!Listening) Text = value; }
    }

    private void StartListening()
    {
        Listening = true;
        Text = "Press keys…";
        ForeColor = Theme.Accent;
    }

    public void StopListening()
    {
        if (!Listening) return;
        Listening = false;
        Text = idleText;
        ForeColor = Theme.Text;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Listening) return base.ProcessCmdKey(ref msg, keyData);

        var key = keyData & Keys.KeyCode;
        var noModifiers = (keyData & Keys.Modifiers) == 0;

        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return true; // a modifier on its own: keep waiting for the main key

        if (key == Keys.Escape && noModifiers) { StopListening(); return true; }

        if (key == Keys.Tab)
        {
            StopListening();
            return base.ProcessCmdKey(ref msg, keyData); // let Tab move focus as usual
        }

        if (!KeyCatalog.TryGet((int)key, out _)) return true; // unsupported key: ignore it

        var chord = new KeyChord((int)key,
            (keyData & Keys.Control) != 0, (keyData & Keys.Alt) != 0, (keyData & Keys.Shift) != 0);
        ignoreClicksUntil = Environment.TickCount64 + 300; // Space/Enter key-up would otherwise "click" us again
        StopListening();
        Captured?.Invoke(chord);
        return true;
    }
}

/// <summary>A drawn checkbox, so it matches the light and dark themes exactly. Behaves like a normal CheckBox.</summary>
internal sealed class CheckRow : CheckBox
{
    private const int BoxSize = 18;
    private const int Gap = 8;

    public CheckRow()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        Font = Theme.Body();
        ForeColor = Theme.Text;
        Margin = Padding.Empty;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        return new Size(BoxSize + Gap + text.Width + 2, Math.Max(BoxSize + 6, text.Height + 6));
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        var box = new Rectangle(1, (Height - BoxSize) / 2, BoxSize - 1, BoxSize - 1);
        var enabled = Enabled;

        using (var path = Rounded(box, 4))
        {
            if (Checked)
            {
                using var fill = new SolidBrush(enabled ? Theme.Accent : Theme.Disabled);
                g.FillPath(fill, path);
            }
            else
            {
                using var fill = new SolidBrush(enabled ? Theme.Surface : Theme.Background);
                g.FillPath(fill, path);
                using var border = new Pen(enabled ? (ClientRectangle.Contains(PointToClient(MousePosition)) ? Theme.Accent : Theme.Muted) : Theme.Disabled, 1.2f);
                g.DrawPath(border, path);
            }
        }

        if (Checked)
        {
            using var tick = new Pen(Theme.Current.OnAccent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(tick, new[]
            {
                new PointF(box.Left + 4.2f, box.Top + 9f),
                new PointF(box.Left + 7.4f, box.Top + 12f),
                new PointF(box.Left + 13.2f, box.Top + 5.2f),
            });
        }

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.Accent, 1f) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(ring, 0, 0, Width - 1, Height - 1);
        }

        TextRenderer.DrawText(g, Text, Font, new Rectangle(BoxSize + Gap, 0, Width - BoxSize - Gap, Height),
            enabled ? ForeColor : Theme.Disabled,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// A drawn drop-down for choosing the key. Click it, press Space, Enter or Down to open the list,
/// use Up and Down to step through keys, or just type a letter or digit to jump to it.
/// </summary>
internal sealed class KeyPicker : Control
{
    private const int RowHeight = 28;

    private readonly IReadOnlyList<KeyInfo> items = KeyCatalog.All;
    private KeyInfo? selected;
    private ToolStripDropDown? dropDown;

    public event EventHandler? SelectedIndexChanged;

    public KeyPicker(int width)
    {
        Size = new Size(width, 32);
        Margin = Padding.Empty;
        Cursor = Cursors.Hand;
        TabStop = true;
        Font = Theme.Body();
        AccessibleRole = AccessibleRole.DropList;
        AccessibleName = "Key";
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public KeyInfo? SelectedItem
    {
        get => selected;
        set
        {
            if (Equals(selected, value)) return;
            selected = value;
            AccessibleName = selected is null ? "Key" : $"Key: {selected.Name}";
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Enabled ? Theme.Surface : Theme.Background);

        var edge = dropDown is not null || Focused ? Theme.Accent : Theme.Line;
        using (var pen = new Pen(edge, 1f)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        TextRenderer.DrawText(g, selected?.Name ?? "Choose…", Font, new Rectangle(10, 0, Width - 32, Height),
            Enabled ? Theme.Text : Theme.Disabled,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        var cx = Width - 16f;
        var cy = Height / 2f;
        using var chevron = new Pen(Enabled ? Theme.Muted : Theme.Disabled, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(chevron, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (dropDown is null) OpenList(); else dropDown.Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var index = selected is null ? -1 : items.ToList().IndexOf(selected);

        if (e.KeyCode is Keys.Space or Keys.Enter or Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            OpenList();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down && !e.Alt) { Step(index + 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Up) { Step(index - 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Home) { Step(0); e.Handled = true; }
        else if (e.KeyCode == Keys.End) { Step(items.Count - 1); e.Handled = true; }
        else if (!e.Control && !e.Alt && (e.KeyCode is >= Keys.A and <= Keys.Z || e.KeyCode is >= Keys.D0 and <= Keys.D9))
        {
            var typed = e.KeyCode is >= Keys.D0 and <= Keys.D9 ? ((char)e.KeyCode).ToString() : e.KeyCode.ToString();
            var match = items.FirstOrDefault(k => k.Name.Equals(typed, StringComparison.OrdinalIgnoreCase));
            if (match is not null) SelectedItem = match;
            e.Handled = true;
        }
    }

    private void Step(int index)
    {
        if (items.Count == 0) return;
        SelectedItem = items[Math.Clamp(index, 0, items.Count - 1)];
    }

    private void OpenList()
    {
        if (dropDown is not null || !Enabled) return;

        var list = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = RowHeight,
            IntegralHeight = false,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Font,
            Size = new Size(Width, Math.Min(items.Count * RowHeight, 280)),
            AccessibleName = "Keys",
        };
        foreach (var k in items) list.Items.Add(k);
        if (selected is not null) { list.SelectedItem = selected; list.TopIndex = Math.Max(0, list.SelectedIndex - 3); }

        var hover = -1;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var isSelected = e.Index == list.SelectedIndex;
            var back = isSelected ? Theme.AccentSoft : e.Index == hover ? Theme.Current.Hover : Theme.Surface;
            using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, ((KeyInfo)list.Items[e.Index]).Name, list.Font,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                isSelected ? Theme.Accent : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        };
        list.MouseMove += (_, e) =>
        {
            var i = list.IndexFromPoint(e.Location);
            if (i == hover) return;
            hover = i;
            list.Invalidate();
        };
        list.MouseUp += (_, e) =>
        {
            var i = list.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0) Choose((KeyInfo)list.Items[i]);
        };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && list.SelectedItem is KeyInfo k) { Choose(k); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { dropDown?.Close(); e.Handled = true; }
        };
        list.HandleCreated += (_, _) => Theme.StyleNative(list);

        var host = new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size };
        var drop = new ToolStripDropDown
        {
            AutoSize = false,
            Padding = new Padding(1),
            BackColor = Theme.Line,
            DropShadowEnabled = false,
            Size = new Size(list.Width + 2, list.Height + 2),
        };
        drop.Items.Add(host);
        drop.Closed += (_, _) =>
        {
            dropDown = null;
            Invalidate();
            list.Dispose();
        };
        dropDown = drop;

        drop.Show(this, new Point(0, Height + 2));
        list.Focus();
        Invalidate();
    }

    private void Choose(KeyInfo key)
    {
        SelectedItem = key;
        dropDown?.Close();
        Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) dropDown?.Close();
        base.Dispose(disposing);
    }
}
