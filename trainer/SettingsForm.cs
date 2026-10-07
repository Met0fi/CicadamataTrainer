using System.Runtime.InteropServices;

namespace CicadamataTrainer;

internal sealed class SettingsForm : Form, IMessageFilter
{
    private const string CaptureButtonText = "CLICK ON ME";
    private static readonly Color Crimson = Color.FromArgb(196, 43, 71);
    private static readonly Color Surface = Color.FromArgb(16, 16, 20);
    private static readonly Color PanelColor = Color.FromArgb(28, 28, 34);
    private static readonly Color Ink = Color.FromArgb(230, 230, 230);
    private readonly CheckBox _sound = new();
    private readonly Dictionary<string, ComboBox> _binds = new();
    private readonly Dictionary<string, Button> _captures = new();
    private readonly KeyCaptureOverlay _captureOverlay = new();
    private readonly List<Control> _hidden = new();
    private string? _captureAction;
    private Keys? _releaseKey;
    private bool _updating;
    private readonly Dictionary<string, Label> _labels = new();
    private readonly Dictionary<string, Label> _sectionLabels = new();

    public SettingsForm()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 640);
        MinimumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Surface;
        ForeColor = Ink;
        Font = GameFont.Create(14f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Text = Localizer.Get("settings_title");
        Build();
        Application.AddMessageFilter(this);
    }

    private void Build()
    {
        var title = new Label { Name = "Title", Text = Localizer.Get("settings_title"), AutoSize = true, Location = new Point(20, 16), Font = GameFont.Create(18f) };
        _sound.Name = "Sound";
        _sound.Text = Localizer.Get("sound");
        _sound.Checked = TrainerConfig.LoadSoundEnabled();
        _sound.SetBounds(20, 55, 180, 28);
        _sound.FlatStyle = FlatStyle.Flat;
        _sound.FlatAppearance.CheckedBackColor = Crimson;
        _sound.CheckedChanged += (_, _) => { if (!_updating) { TrainerConfig.SaveSoundEnabled(_sound.Checked); UiSounds.Enabled = _sound.Checked; } };
        var section = new Label { Name = "BindsTitle", Text = Localizer.Get("binds"), AutoSize = true, Location = new Point(20, 102), ForeColor = Color.FromArgb(170, 170, 180) };
        var keyTitle = new Label { Name = "KeyTitle", Text = Localizer.Get("key"), AutoSize = true, Location = new Point(420, 102), ForeColor = Color.FromArgb(170, 170, 180) };
        _sectionLabels["binds"] = section;
        _sectionLabels["key"] = keyTitle;
        TrainerConfig.UpgradeHotkeys();
        var hotkeys = TrainerConfig.LoadHotkeys();
        var row = 0;
        foreach (var pair in TrainerForm.BindLabelsForSettings)
        {
            var y = 128 + row * 30;
            var label = new Label { Text = Localizer.Get(pair.Key), Bounds = new Rectangle(20, y + 3, 360, 26), TextAlign = ContentAlignment.MiddleLeft };
            _labels[pair.Key] = label;
            var combo = new BindComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(400, y, 190, 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = PanelColor, ForeColor = Ink };
            combo.Items.Add(CaptureButtonText);
            combo.Items.AddRange(TrainerConfig.KeyOptions.Cast<object>().ToArray());
            SelectBind(combo, hotkeys[pair.Key]);
            combo.SelectedIndexChanged += (_, _) => OnBindChanged(pair.Key, combo);
            _binds[pair.Key] = combo;
            var capture = Flat(CaptureButtonText, 604, y, 136, 26, AnchorStyles.Top | AnchorStyles.Right);
            capture.Tag = pair.Key;
            capture.AccessibleName = $"{Localizer.Get("bind")} {Localizer.Get(pair.Key)}";
            capture.Click += (_, _) => BeginCapture(pair.Key);
            _captures[pair.Key] = capture;
            Controls.AddRange(new Control[] { label, combo, capture });
            row++;
        }
        var reset = Flat(Localizer.Get("reset"), 20, 566, 220, 30, AnchorStyles.Bottom | AnchorStyles.Left);
        reset.Name = "Reset";
        reset.Click += (_, _) => { Trainer.ResetHotkeys(_ => { }); ApplyHotkeys(TrainerConfig.LoadHotkeys()); };
        var close = Flat(Localizer.Get("close"), 520, 566, 220, 30, AnchorStyles.Bottom | AnchorStyles.Right);
        close.Name = "Close";
        close.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { title, _sound, section, keyTitle, reset, close, _captureOverlay });
        foreach (Control control in Controls)
        {
            if (control is Button button) button.UseCompatibleTextRendering = true;
            if (control is Label labelControl) labelControl.UseCompatibleTextRendering = true;
            if (control is CheckBox check) check.UseCompatibleTextRendering = true;
        }
    }

    private static Button Flat(string text, int left, int top, int width, int height, AnchorStyles anchor)
    {
        var button = new Button { Text = text, Bounds = new Rectangle(left, top, width, height), Anchor = anchor, FlatStyle = FlatStyle.Flat, BackColor = PanelColor, ForeColor = Ink };
        button.FlatAppearance.BorderColor = Crimson;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 46, 54);
        return button;
    }

    private void ApplyLanguage()
    {
        Text = Localizer.Get("settings_title");
        Font = GameFont.Create(14f);
        foreach (Control control in Controls) ApplyFont(control);
        if (Controls["Title"] is Label title) { title.Text = Localizer.Get("settings_title"); title.Font = GameFont.Create(18f); }
        if (Controls["BindsTitle"] is Label bindsTitle) bindsTitle.Text = Localizer.Get("binds");
        if (Controls["KeyTitle"] is Label keyTitle) keyTitle.Text = Localizer.Get("key");
        _sound.Text = Localizer.Get("sound");
        if (Controls["Reset"] is Button reset) reset.Text = Localizer.Get("reset");
        if (Controls["Close"] is Button close) close.Text = Localizer.Get("close");
        foreach (var pair in _labels) pair.Value.Text = Localizer.Get(pair.Key);
    }

    private static void ApplyFont(Control control)
    {
        if (control is KeyCaptureOverlay) return;
        control.Font = GameFont.Create(control.Font.Size);
        foreach (Control child in control.Controls) ApplyFont(child);
    }

    private void OnBindChanged(string action, ComboBox combo)
    {
        if (_updating) return;
        if (combo.SelectedItem?.ToString() == CaptureButtonText)
        {
            _updating = true;
            SelectBind(combo, combo.Tag?.ToString() ?? TrainerConfig.HotkeyDefaults[action]);
            _updating = false;
            combo.DroppedDown = false;
            BeginCapture(action);
            return;
        }
        var current = _binds.ToDictionary(pair => pair.Key, pair => pair.Value.Tag?.ToString() ?? TrainerConfig.HotkeyDefaults[pair.Key]);
        var selected = combo.SelectedItem?.ToString() ?? current[action];
        try
        {
            current = TrainerConfig.AssignHotkey(current, action, selected);
            ApplyHotkeys(current);
            TrainerConfig.SaveHotkeys(current);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ApplyHotkeys(current);
        }
    }

    private void ApplyHotkeys(IReadOnlyDictionary<string, string> hotkeys)
    {
        _updating = true;
        try { foreach (var pair in _binds) if (hotkeys.TryGetValue(pair.Key, out var key)) SelectBind(pair.Value, key); }
        finally { _updating = false; }
    }

    private static void SelectBind(ComboBox combo, string key)
    {
        if (!combo.Items.Contains(key)) combo.Items.Add(key);
        combo.Tag = key;
        combo.SelectedItem = key;
    }

    private void BeginCapture(string action)
    {
        EndCapture(null);
        _captureAction = action;
        _releaseKey = null;
        _captureOverlay.ActionName = Localizer.Get(action);
        _captureOverlay.Status = "";
        foreach (Control control in Controls)
        {
            if (control != _captureOverlay && control.Visible) { _hidden.Add(control); control.Visible = false; }
        }
        _captureOverlay.Visible = true;
        _captureOverlay.BringToFront();
        _captureOverlay.Focus();
        _captureOverlay.Invalidate();
    }

    private void EndCapture(string? key)
    {
        if (_captureAction == null) return;
        var action = _captureAction;
        if (key != null)
        {
            try
            {
                var current = _binds.ToDictionary(pair => pair.Key, pair => pair.Value.Tag?.ToString() ?? TrainerConfig.HotkeyDefaults[pair.Key]);
                TrainerConfig.SaveHotkeys(TrainerConfig.AssignHotkey(current, action, key));
                ApplyHotkeys(TrainerConfig.LoadHotkeys());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _captureOverlay.Status = Localizer.Get("failed");
                _captureOverlay.Invalidate();
                return;
            }
        }
        _captureAction = null;
        _captureOverlay.Visible = false;
        foreach (var control in _hidden) control.Visible = true;
        _hidden.Clear();
        if (_captures.TryGetValue(action, out var button)) button.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_captureAction == null) return base.ProcessCmdKey(ref msg, keyData);
        CaptureKey(keyData & Keys.KeyCode, (msg.LParam.ToInt64() & (1L << 24)) != 0);
        return true;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (Control.FromChildHandle(message.HWnd)?.FindForm() != this) return false;
        var key = (Keys)(int)message.WParam & Keys.KeyCode;
        if (message.Msg is 0x100 or 0x104)
        {
            if (_releaseKey == key) return true;
            if (_captureAction == null) return false;
            _releaseKey = key;
            CaptureKey(key, (message.LParam.ToInt64() & (1L << 24)) != 0);
            return true;
        }
        if (message.Msg is 0x101 or 0x105)
        {
            if (_releaseKey == key) { _releaseKey = null; return true; }
            return _captureAction != null;
        }
        return message.Msg is 0x102 or 0x106 && (_captureAction != null || _releaseKey != null);
    }

    private void CaptureKey(Keys key, bool extended)
    {
        if (key == Keys.Escape) { EndCapture(null); return; }
        var name = TrainerForm.KeyName(key, extended);
        if (name == null)
        {
            _captureOverlay.Status = Localizer.Get("unsupported");
            _captureOverlay.Invalidate();
            return;
        }
        EndCapture(name);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        EndCapture(null);
        _releaseKey = null;
        base.OnDeactivate(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Application.RemoveMessageFilter(this);
        base.Dispose(disposing);
    }
}
