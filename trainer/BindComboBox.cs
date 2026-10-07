namespace CicadamataTrainer;

internal sealed class BindComboBox : ComboBox
{
    public BindComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 22;
        DropDownWidth = 240;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        e.DrawBackground();
        var text = e.Index >= 0 && e.Index < Items.Count ? Items[e.Index]?.ToString() : SelectedItem?.ToString();
        using var brush = new SolidBrush(e.ForeColor);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        var bounds = new RectangleF(e.Bounds.X + 4, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 8), e.Bounds.Height);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        e.Graphics.DrawString(text ?? "", Font, brush, bounds, format);
        e.DrawFocusRectangle();
        base.OnDrawItem(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (DroppedDown)
        {
            base.OnMouseWheel(e);
        }
        else if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }
    }
}
