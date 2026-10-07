using System.Drawing.Drawing2D;

namespace CicadamataTrainer;

internal sealed class KeyCaptureOverlay : Control
{
    public string ActionName { get; set; } = "";
    public string Status { get; set; } = "";

    public KeyCaptureOverlay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(16, 16, 20);
        ForeColor = Color.FromArgb(230, 230, 230);
        TabStop = true;
        AccessibleRole = AccessibleRole.Pane;
        AccessibleName = "Press any key";
        Visible = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var scale = DeviceDpi / 96f;
        var centerX = ClientSize.Width / 2f;
        var top = ClientSize.Height / 2f - 120f * scale;
        var key = new RectangleF(centerX - 44f * scale, top, 88f * scale, 76f * scale);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Color.FromArgb(196, 43, 71), 2f * scale);
        using var arrow = new Pen(ForeColor, 2.5f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var keyFill = new SolidBrush(Color.FromArgb(28, 28, 34));
        e.Graphics.FillRectangle(keyFill, key);
        e.Graphics.DrawRectangle(border, key.X, key.Y, key.Width, key.Height);
        e.Graphics.DrawLines(arrow, new[]
        {
            new PointF(centerX + 16f * scale, top + 24f * scale),
            new PointF(centerX + 16f * scale, top + 42f * scale),
            new PointF(centerX - 16f * scale, top + 42f * scale),
            new PointF(centerX - 8f * scale, top + 34f * scale),
            new PointF(centerX - 16f * scale, top + 42f * scale),
            new PointF(centerX - 8f * scale, top + 50f * scale),
        });
        using var promptFont = GameFont.Create(32f * scale);
        using var detailFont = GameFont.Create(16f * scale);
        DrawText(e.Graphics, Localizer.Get("press"), promptFont, top + 98f * scale, 48f * scale, ForeColor);
        DrawText(e.Graphics, $"{Localizer.Get("bind")}: {ActionName}", detailFont, top + 150f * scale, 26f * scale, ForeColor);
        DrawText(e.Graphics, Localizer.Get("cancel"), detailFont, top + 186f * scale, 26f * scale, Color.FromArgb(170, 170, 180));
        DrawText(e.Graphics, Status, detailFont, top + 226f * scale, 58f * scale, Color.FromArgb(224, 85, 110));
    }

    private void DrawText(Graphics graphics, string text, Font font, float top, float height, Color color)
    {
        var bounds = new Rectangle(16, (int)top, Math.Max(1, ClientSize.Width - 32), (int)height);
        TextRenderer.DrawText(graphics, text, font, bounds, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}
