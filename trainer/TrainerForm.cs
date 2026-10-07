using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CicadamataTrainer;

internal sealed class TrainerForm : Form, IMessageFilter
{
    private const string CaptureButtonText = "CLICK ON ME";
    private const string AuthorUrl = "https://met0fi.github.io/Siteportfoliomt/";
    private static readonly Color Crimson = Color.FromArgb(196, 43, 71);
    private static readonly Color Purple = Color.FromArgb(139, 92, 246);
    private static readonly Color Surface = Color.FromArgb(16, 16, 20);
    private static readonly Color PanelColor = Color.FromArgb(28, 28, 34);
    private static readonly Color Ink = Color.FromArgb(230, 230, 230);

    private readonly TextBox _folder = new();
    private readonly TextBox _log = new();
    private readonly Label _state = new();
    private readonly CheckBox _sound = new();
    internal static readonly IReadOnlyDictionary<string, string> BindLabelsForSettings = new Dictionary<string, string>
    {
        ["god"] = "god", ["breath"] = "breath", ["jumps"] = "jumps", ["dash"] = "dash", ["onehit"] = "onehit", ["noreload"] = "noreload",
        ["hud"] = "hud", ["panic"] = "panic", ["allon"] = "allon", ["layout"] = "layout", ["teleport"] = "teleport", ["killall"] = "killall", ["teleportexit"] = "teleportexit",
    };

    private readonly Dictionary<string, CheckBox> _toggles = new();
    private readonly Dictionary<string, ComboBox> _binds = new();
    private readonly Dictionary<string, Button> _captures = new();
    private readonly KeyCaptureOverlay _captureOverlay = new();
    private readonly List<Control> _captureHiddenControls = new();
    private string? _captureAction;
    private Keys? _captureReleaseKey;
    private bool _updatingBinds;
    private readonly System.Windows.Forms.Timer _statusTimer = new();
    private bool _busy;
    private bool _operationFailed;
    private readonly MemoryStream _bannerStream;
    private readonly Image _bannerImage;
    private bool _bulk;

    private static readonly Dictionary<string, string> ToggleLabels = new()
    {
        ["god"] = "God mode - no damage",
        ["breath"] = "Underwater safety",
        ["jumps"] = "Infinite jumps",
        ["dash"] = "Infinite dash",
        ["onehit"] = "One-bullet kill",
        ["noreload"] = "No weapon recharge",
    };

    private static readonly Dictionary<string, string> BindLabels = new()
    {
        ["god"] = "god",
        ["breath"] = "underwater",
        ["jumps"] = "jumps",
        ["dash"] = "dash",
        ["onehit"] = "one-hit",
        ["noreload"] = "no reload",
        ["hud"] = "hide GUI",
        ["panic"] = "disable all",
        ["allon"] = "enable all",
        ["layout"] = "lock layout",
        ["teleport"] = "teleport",
        ["killall"] = "kill all enemies",
        ["teleportexit"] = "teleport to exit",
    };

    public TrainerForm()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Text = Localizer.Get("title");
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(800, 730);
        MinimumSize = Size;
        StartPosition = FormStartPosition.CenterScreen;
        Font = GameFont.Create();
        BackColor = Surface;
        ForeColor = Ink;
        Icon = LoadIcon();
        _bannerStream = new MemoryStream(ModPayload.Banner);
        _bannerImage = Image.FromStream(_bannerStream);
        var banner = new PictureBox { Image = _bannerImage, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(20, 12, 270, 126), BackColor = Color.Black, TabStop = false };
        var author = Flat("BY M:/T", 656, 12, 124, 32, AnchorStyles.Top | AnchorStyles.Right);
        author.Name = "Website";
        author.BackColor = Purple;
        author.FlatAppearance.BorderColor = Purple;
        author.AccessibleName = "Open author's website";
        author.Click += (_, _) => Run(() => Process.Start(new ProcessStartInfo(AuthorUrl) { UseShellExecute = true }), false);
        var folderCaption = new Label { Name = "FolderCaption", Text = Localizer.Get("folder"), AutoSize = true, Location = new Point(312, 49) };
        _folder.SetBounds(312, 70, 332, 26);
        _folder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _folder.Text = GameInstall.Resolve(null)?.Folder ?? "";
        _folder.BackColor = PanelColor;
        _folder.ForeColor = Ink;
        _folder.BorderStyle = BorderStyle.FixedSingle;
        _folder.Font = GameFont.Create(12f);
        _folder.AccessibleName = "Game folder";
        var browse = Flat(Localizer.Get("browse"), 656, 68, 124, 30, AnchorStyles.Top | AnchorStyles.Right);
        browse.Name = "Browse";
        browse.Click += (_, _) => Browse();
        _state.SetBounds(312, 103, 468, 22);
        _state.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _state.AutoEllipsis = true;
        var load = Flat(Localizer.Get("load"), 312, 136, 220, 34, AnchorStyles.Top | AnchorStyles.Left);
        load.Name = "LoadTrainer";
        load.BackColor = Crimson;
        load.Click += async (_, _) => { var folder = _folder.Text; await RunLong(() => Trainer.Load(folder, true, Append)); };
        var eject = Flat(Localizer.Get("eject"), 544, 136, 236, 34, AnchorStyles.Top | AnchorStyles.Left);
        eject.Name = "EjectTrainer";
        eject.Click += async (_, _) => { var folder = _folder.Text; await RunLong(() => Trainer.Eject(folder, Append)); };
        var bindsCaption = new Label { Name = "CheatsCaption", Text = Localizer.Get("cheats"), AutoSize = true, Location = new Point(20, 182), ForeColor = Color.FromArgb(170, 170, 180) };
        var keyCaption = new Label { Name = "KeyCaption", Text = Localizer.Get("key"), AutoSize = true, Location = new Point(392, 182), Anchor = AnchorStyles.Top | AnchorStyles.Right, ForeColor = Color.FromArgb(170, 170, 180) };
        var saved = TrainerConfig.LoadCheats();
        TrainerConfig.UpgradeHotkeys();
        var hotkeys = TrainerConfig.LoadHotkeys();
        var row = 0;
        foreach (var (name, label) in BindLabels)
        {
            var y = 208 + row * 30;
            Control function;
            if (ToggleLabels.TryGetValue(name, out var toggleLabel))
            {
                var box = new CheckBox { Text = toggleLabel, Bounds = new Rectangle(20, y, 356, 28), Checked = saved[name], FlatStyle = FlatStyle.Flat };
                box.FlatAppearance.CheckedBackColor = Crimson;
                box.CheckedChanged += (_, _) => Run(() => PushToggles(box.Checked), false);
                _toggles[name] = box;
                function = box;
            }
            else if (name is "hud" or "layout")
            {
                function = new Label { Name = $"Action_{name}", Text = Localizer.Get(name), Bounds = new Rectangle(20, y, 356, 28), TextAlign = ContentAlignment.MiddleLeft };
            }
            else
            {
                var actionLabel = Localizer.Get(name);
                var action = Flat(actionLabel, 20, y, 356, 28, AnchorStyles.Top | AnchorStyles.Left);
                action.Name = $"Action_{name}";
                action.Click += (_, _) => Run(() =>
                {
                    switch (name)
                    {
                        case "panic": SetAllCheats(false); break;
                        case "allon": SetAllCheats(true); break;
                        case "teleport": Trainer.TeleportToCore(Append); break;
                        case "killall": Trainer.KillAllEnemies(Append); break;
                        case "teleportexit": Trainer.TeleportToExit(Append); break;
                    }
                }, false);
                function = action;
            }
            var combo = new BindComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Left = ClientSize.Width - 408,
                Top = y,
                Width = 208,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = PanelColor,
                ForeColor = Ink,
            };
            combo.Items.Add(CaptureButtonText);
            combo.Items.AddRange(TrainerConfig.KeyOptions.Cast<object>().ToArray());
            var current = hotkeys.TryGetValue(name, out var key) ? key : TrainerConfig.HotkeyDefaults[name];
            SelectBind(combo, current);
            combo.SelectedIndexChanged += (_, _) =>
            {
                if (_updatingBinds)
                {
                    return;
                }
                if (combo.SelectedItem?.ToString() == CaptureButtonText)
                {
                    _updatingBinds = true;
                    try
                    {
                        SelectBind(combo, combo.Tag?.ToString() ?? current);
                    }
                    finally
                    {
                        _updatingBinds = false;
                    }
                    combo.DroppedDown = false;
                    BeginCapture(name);
                    return;
                }
                var previous = combo.Tag?.ToString() ?? current;
                var selected = combo.SelectedItem?.ToString() ?? previous;
                var hotkeys = _binds.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedItem?.ToString() ?? TrainerConfig.HotkeyDefaults[pair.Key]);
                hotkeys[name] = previous;
                var previousHotkeys = new Dictionary<string, string>(hotkeys);
                try
                {
                    hotkeys = TrainerConfig.AssignHotkey(hotkeys, name, selected);
                    TrainerConfig.SaveHotkeys(hotkeys);
                    ApplyHotkeys(hotkeys);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    ApplyHotkeys(previousHotkeys);
                    Append($"binding failed: {error.Message}");
                }
            };
            _binds[name] = combo;
            combo.AccessibleName = $"Key for {label}";
            var capture = Flat(CaptureButtonText, ClientSize.Width - 188, y, 168, 28, AnchorStyles.Top | AnchorStyles.Right);
            capture.AccessibleName = $"Assign key for {label}";
            capture.Tag = name;
            capture.Click += (_, _) => BeginCapture(name);
            _captures[name] = capture;
            Controls.AddRange(new[] { function, combo, capture });
            row++;
        }
        _sound.Text = Localizer.Get("sound");
        _sound.SetBounds(20, 614, 200, 28);
        _sound.Checked = TrainerConfig.LoadSoundEnabled();
        _sound.FlatStyle = FlatStyle.Flat;
        _sound.FlatAppearance.CheckedBackColor = Crimson;
        UiSounds.Enabled = _sound.Checked;
        _sound.CheckedChanged += (_, _) => Run(() => PushToggles(_sound.Checked), false);
        var resetBinds = Flat(Localizer.Get("reset"), 392, 614, 208, 28, AnchorStyles.Top | AnchorStyles.Right);
        resetBinds.Name = "ResetBinds";
        resetBinds.Click += (_, _) => Run(() =>
        {
            Trainer.ResetHotkeys(Append);
            ApplyHotkeys(TrainerConfig.LoadHotkeys());
        }, false);
        var settings = Flat(Localizer.Get("settings"), 612, 614, 168, 28, AnchorStyles.Top | AnchorStyles.Right);
        settings.Name = "Settings";
        settings.Click += (_, _) =>
        {
            using var dialog = new SettingsForm();
            dialog.ShowDialog(this);
            ApplyLanguage();
        };
        var details = Flat(Localizer.Get("details"), 612, 646, 168, 28, AnchorStyles.Top | AnchorStyles.Right);
        details.Name = "Details";
        var layoutHint = new Label { Name = "Hint", Text = Localizer.Get("hint"), Bounds = new Rectangle(20, 690, 760, 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = Color.FromArgb(170, 170, 180) };
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.SetBounds(20, 700, 760, 150);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _log.BackColor = Color.FromArgb(10, 10, 13);
        _log.ForeColor = Ink;
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = GameFont.Create(12f);
        _log.Visible = false;
        _log.AccessibleName = "Trainer diagnostics";
        var openLog = Flat(Localizer.Get("open_log"), 20, 862, 160, 28, AnchorStyles.Top | AnchorStyles.Left);
        openLog.Visible = false;
        openLog.Click += (_, _) => Run(() => Trainer.OpenLog(Append), false);
        details.Click += (_, _) =>
        {
            _log.Visible = !_log.Visible;
            openLog.Visible = _log.Visible;
            details.Text = _log.Visible ? "Hide details" : "Details";
            ClientSize = new Size(ClientSize.Width, _log.Visible ? 940 : 730);
        };
        Controls.AddRange(new Control[] { banner, author, folderCaption, _folder, browse, _state, load, eject, bindsCaption, keyCaption, _sound, resetBinds, settings, details, layoutHint, _log, openLog, _captureOverlay });
        foreach (Control control in Controls)
        {
            if (control is Button button) button.UseCompatibleTextRendering = true;
            else if (control is Label label) label.UseCompatibleTextRendering = true;
            else if (control is CheckBox checkbox) checkbox.UseCompatibleTextRendering = true;
        }
        foreach (var combo in _binds.Values) combo.Visible = false;
        foreach (var capture in _captures.Values) capture.Visible = false;
        foreach (Control control in Controls)
        {
            if (control is Label label && (label.Name == "CheatsCaption" || label.Name == "KeyCaption" || label.Left == 496)) label.Visible = false;
        }
        _sound.Visible = false;
        if (Controls["ResetBinds"] is Button reset) reset.Visible = false;
        Application.AddMessageFilter(this);
        _statusTimer.Interval = 2000;
        _statusTimer.Tick += (_, _) => { if (!_busy) RefreshState(); };
        _statusTimer.Start();
        Load += (_, _) => { Run(() => Trainer.Status(_folder.Text, Append), false); RefreshState(); };
    }

    private static Button Flat(string text, int left, int top, int width, int height, AnchorStyles anchor)
    {
        var button = new Button
        {
            Text = text,
            Bounds = new Rectangle(left, top, width, height),
            Anchor = anchor,
            FlatStyle = FlatStyle.Flat,
            BackColor = PanelColor,
            ForeColor = Ink,
        };
        button.FlatAppearance.BorderColor = Crimson;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 46, 54);
        return button;
    }

    private static Icon? LoadIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch (Exception)
        {
            return SystemIcons.Application;
        }
    }

    private void ApplyLanguage()
    {
        Text = Localizer.Get("title");
        Font = GameFont.Create(14f);
        if (Controls["FolderCaption"] is Label folder) folder.Text = Localizer.Get("folder");
        if (Controls["Browse"] is Button browse) browse.Text = Localizer.Get("browse");
        if (Controls["LoadTrainer"] is Button load) load.Text = Localizer.Get("load");
        if (Controls["EjectTrainer"] is Button eject) eject.Text = Localizer.Get("eject");
        if (Controls["CheatsCaption"] is Label cheats) cheats.Text = Localizer.Get("cheats");
        if (Controls["Hint"] is Label hint) hint.Text = Localizer.Get("hint");
        if (Controls["Settings"] is Button settings) settings.Text = Localizer.Get("settings");
        if (Controls["Details"] is Button details) details.Text = _log.Visible ? Localizer.Get("hide_details") : Localizer.Get("details");
        if (Controls["ResetBinds"] is Button reset) reset.Text = Localizer.Get("reset");
        foreach (var action in BindLabels.Keys)
        {
            if (Controls[$"Action_{action}"] is Control control) control.Text = Localizer.Get(action);
        }
        foreach (var pair in _toggles) pair.Value.Text = Localizer.Get(pair.Key);
        ApplyFonts(this);
    }

    private static void ApplyFonts(Control control)
    {
        if (control is not PictureBox) control.Font = GameFont.Create(control.Font.Size);
        foreach (Control child in control.Controls) ApplyFonts(child);
    }

    private void SetAllCheats(bool enabled)
    {
        _bulk = true;
        try
        {
            foreach (var box in _toggles.Values)
            {
                box.Checked = enabled;
            }
        }
        finally
        {
            _bulk = false;
        }
        var cheats = _toggles.ToDictionary(kv => kv.Key, kv => kv.Value.Checked);
        TrainerConfig.SaveCheats(cheats, _sound.Checked);
        UiSounds.Enabled = _sound.Checked;
        if (!Trainer.TrainerActiveInRunningGame())
        {
            UiSounds.Play(enabled ? "all_on" : "panic");
        }
    }

    private void PushToggles(bool value)
    {
        if (_bulk) return;
        var cheats = _toggles.ToDictionary(kv => kv.Key, kv => kv.Value.Checked);
        TrainerConfig.SaveCheats(cheats, _sound.Checked);
        UiSounds.Enabled = _sound.Checked;
        if (_bulk || Trainer.TrainerActiveInRunningGame())
        {
            return;
        }
        UiSounds.Play(value ? "toggle_on" : "toggle_off");
    }

    private void ApplyHotkeys(IReadOnlyDictionary<string, string> hotkeys)
    {
        _updatingBinds = true;
        try
        {
            foreach (var (name, combo) in _binds)
            {
                if (hotkeys.TryGetValue(name, out var key))
                {
                    SelectBind(combo, key);
                }
            }
        }
        finally
        {
            _updatingBinds = false;
        }
    }

    private static void SelectBind(ComboBox combo, string key)
    {
        if (!combo.Items.Contains(key))
        {
            combo.Items.Add(key);
        }
        combo.Tag = key;
        combo.SelectedItem = key;
    }

    private void BeginCapture(string action)
    {
        EndCapture(null, false);
        _captureAction = action;
        _captureReleaseKey = (GetKeyState((int)Keys.Enter) & 0x8000) != 0 ? Keys.Enter
            : (GetKeyState((int)Keys.Space) & 0x8000) != 0 ? Keys.Space : null;
        if (_captures.TryGetValue(action, out var button))
        {
            button.BackColor = Crimson;
            button.ForeColor = Color.White;
        }
        _captureOverlay.ActionName = Localizer.Get(action);
        _captureOverlay.Status = "";
        _captureOverlay.AccessibleDescription = $"{Localizer.Get("bind")} {Localizer.Get(action)}. {Localizer.Get("cancel")}";
        foreach (Control control in Controls)
        {
            if (control != _captureOverlay && control.Visible)
            {
                _captureHiddenControls.Add(control);
                control.Visible = false;
            }
        }
        _captureOverlay.Visible = true;
        _captureOverlay.BringToFront();
        _captureOverlay.Focus();
        _captureOverlay.Invalidate();
        Append($"binding {action}: press any key (Escape cancels)");
    }

    private void EndCapture(string? key, bool announce = true)
    {
        var action = _captureAction;
        if (action != null && key != null)
        {
            var hotkeys = _binds.ToDictionary(kv => kv.Key, kv => kv.Value.SelectedItem?.ToString() ?? TrainerConfig.HotkeyDefaults[kv.Key]);
            hotkeys = TrainerConfig.AssignHotkey(hotkeys, action, key);
            try
            {
                TrainerConfig.SaveHotkeys(hotkeys);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            _captureOverlay.Status = Localizer.Get("failed");
                _captureOverlay.Invalidate();
                Append($"binding failed: {error.Message}");
                return;
            }
            ApplyHotkeys(hotkeys);
        }
        _captureAction = null;
        _captureOverlay.Visible = false;
        foreach (var control in _captureHiddenControls)
        {
            control.Visible = true;
        }
        _captureHiddenControls.Clear();
        if (action != null && _captures.TryGetValue(action, out var button))
        {
            button.Text = CaptureButtonText;
            button.BackColor = PanelColor;
            button.ForeColor = Ink;
            if (ContainsFocus)
            {
                button.Focus();
            }
        }
        if (action == null || key == null)
        {
            return;
        }
        if (announce)
        {
            Append($"bound {action} to {key}");
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_captureAction == null)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }
        CaptureKey(keyData & Keys.KeyCode, (msg.LParam.ToInt64() & (1L << 24)) != 0);
        return true;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (Control.FromChildHandle(message.HWnd)?.FindForm() != this)
        {
            return false;
        }
        var key = (Keys)(int)message.WParam & Keys.KeyCode;
        if (message.Msg is 0x100 or 0x104)
        {
            if (_captureReleaseKey == key)
            {
                return true;
            }
            if (_captureAction == null)
            {
                return false;
            }
            _captureReleaseKey = key;
            CaptureKey(key, (message.LParam.ToInt64() & (1L << 24)) != 0);
            return true;
        }
        if (message.Msg is 0x101 or 0x105)
        {
            if (_captureReleaseKey == key)
            {
                _captureReleaseKey = null;
                return true;
            }
            return _captureAction != null;
        }
        return message.Msg is 0x102 or 0x106 && (_captureAction != null || _captureReleaseKey != null);
    }

    private void CaptureKey(Keys key, bool extended)
    {
        if (key == Keys.Escape)
        {
            EndCapture(null);
            Append("binding cancelled");
            return;
        }
        var name = GodotKeyName(key, extended);
        if (name == null)
        {
            _captureOverlay.Status = Localizer.Get("unsupported");
            _captureOverlay.Invalidate();
            return;
        }
        EndCapture(name);
    }

    private static string? GodotKeyName(Keys key, bool extended)
    {
        if (key >= Keys.F1 && key <= Keys.F24)
        {
            return $"F{key - Keys.F1 + 1}";
        }
        if (key >= Keys.A && key <= Keys.Z)
        {
            return ((char)('A' + (key - Keys.A))).ToString();
        }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            return ((char)('0' + (key - Keys.D0))).ToString();
        }
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return $"Kp {key - Keys.NumPad0}";
        }
        return key switch
        {
            Keys.Space => "Space",
            Keys.Tab => "Tab",
            Keys.Enter => extended ? "Kp Enter" : "Enter",
            Keys.Back => "Backspace",
            Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "Shift",
            Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "Ctrl",
            Keys.Menu or Keys.LMenu or Keys.RMenu => "Alt",
            Keys.LWin or Keys.RWin => "Windows",
            Keys.CapsLock => "CapsLock",
            Keys.NumLock => "NumLock",
            Keys.Scroll => "ScrollLock",
            Keys.Pause => "Pause",
            Keys.PrintScreen => "Print",
            Keys.Insert => "Insert",
            Keys.Delete => "Delete",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.Left => "Left",
            Keys.Up => "Up",
            Keys.Right => "Right",
            Keys.Down => "Down",
            Keys.Multiply => "Kp Multiply",
            Keys.Add => "Kp Add",
            Keys.Subtract => "Kp Subtract",
            Keys.Decimal => "Kp Period",
            Keys.Divide => "Kp Divide",
            Keys.OemSemicolon => "Semicolon",
            Keys.Oemplus => "Equal",
            Keys.Oemcomma => "Comma",
            Keys.OemMinus => "Minus",
            Keys.OemPeriod => "Period",
            Keys.OemQuestion => "Slash",
            Keys.Oemtilde => "QuoteLeft",
            Keys.OemOpenBrackets => "BracketLeft",
            Keys.OemPipe or Keys.OemBackslash => "BackSlash",
            Keys.OemCloseBrackets => "BracketRight",
            Keys.OemQuotes => "Apostrophe",
            Keys.Apps => "Menu",
            _ => null,
        };
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);

    protected override void OnDeactivate(EventArgs e)
    {
        if (_captureAction != null)
        {
            EndCapture(null);
            Append("binding cancelled: trainer lost focus");
        }
        _captureReleaseKey = null;
        base.OnDeactivate(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            _statusTimer.Dispose();
            _bannerImage.Dispose();
            _bannerStream.Dispose();

        }
        base.Dispose(disposing);
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select the CICADAMATA folder" };
        if (Directory.Exists(_folder.Text))
        {
            dialog.SelectedPath = _folder.Text;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folder.Text = dialog.SelectedPath;
            Append($"game folder: {dialog.SelectedPath}");
        }
    }

    private void Run(Action action, bool disableWindow = false)
    {
        _operationFailed = false;
        if (disableWindow)
        {
            Enabled = false;
        }
        try
        {
            action();
        }
        catch (Exception error)
        {
            _operationFailed = true;
            Append($"failed: {error.Message}");
        }
        finally
        {
            if (disableWindow)
            {
                Enabled = true;
            }
            RefreshState();
        }
    }

    private void RefreshState()
    {
        string text;
        try
        {
            var install = GameInstall.Resolve(_folder.Text);
            if (install == null)
            {
                SetStateText(Localizer.Get("missing"));
                return;
            }
            using var process = Loader.FindGameProcess();
            text = Loader.TrainerAlive() ? Localizer.Get("active") : process != null ? Localizer.Get("running") : Localizer.Get("closed");
        }
        catch (Exception error)
        {
            text = $"game: {error.Message}";
        }
        SetStateText(_operationFailed ? Localizer.Get("operation_failed") : text);
    }

    private void SetStateText(string text)
    {
        if (_state.Text != text)
        {
            _state.Text = text;
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x02000000;
            return parameters;
        }
    }

    private async Task RunLong(Action action)
    {
        if (_busy) return;
        _busy = true;
        _operationFailed = false;
        var disabled = Controls.Cast<Control>().Where(control => control.Enabled && control.Name != "Website" && control.Name != "Details" && control != _log && control is not Label).ToArray();
        foreach (var control in disabled) control.Enabled = false;
        _state.Text = Localizer.Get("working");
        try
        {
            await Task.Run(action);
        }
        catch (Exception error)
        {
            _operationFailed = true;
            Append($"failed: {error.Message}");
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                foreach (var control in disabled) control.Enabled = true;
                RefreshState();
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            Append("Wait for the current operation to finish before closing.");
        }
        base.OnFormClosing(e);
    }

    private void Append(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Append(message));
            return;
        }
        if (_log.TextLength > 64000) _log.Text = _log.Text[^32000..];
        _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }
    internal static string? KeyName(Keys key, bool extended)
    {
        if (key >= Keys.F1 && key <= Keys.F24) return $"F{key - Keys.F1 + 1}";
        if (key >= Keys.A && key <= Keys.Z) return ((char)('A' + (key - Keys.A))).ToString();
        if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return $"Kp {key - Keys.NumPad0}";
        return key switch
        {
            Keys.Space => "Space", Keys.Tab => "Tab", Keys.Enter => extended ? "Kp Enter" : "Enter", Keys.Back => "Backspace",
            Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "Shift", Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "Ctrl", Keys.Menu or Keys.LMenu or Keys.RMenu => "Alt", Keys.LWin or Keys.RWin => "Windows", Keys.CapsLock => "CapsLock", Keys.NumLock => "NumLock", Keys.Scroll => "ScrollLock", Keys.Pause => "Pause", Keys.PrintScreen => "Print", Keys.Insert => "Insert", Keys.Delete => "Delete", Keys.Home => "Home", Keys.End => "End", Keys.PageUp => "PageUp", Keys.PageDown => "PageDown", Keys.Left => "Left", Keys.Up => "Up", Keys.Right => "Right", Keys.Down => "Down", Keys.Multiply => "Kp Multiply", Keys.Add => "Kp Add", Keys.Subtract => "Kp Subtract", Keys.Decimal => "Kp Period", Keys.Divide => "Kp Divide", Keys.OemSemicolon => "Semicolon", Keys.Oemplus => "Equal", Keys.Oemcomma => "Comma", Keys.OemMinus => "Minus", Keys.OemPeriod => "Period", Keys.OemQuestion => "Slash", Keys.Oemtilde => "QuoteLeft", Keys.OemOpenBrackets => "BracketLeft", Keys.OemPipe or Keys.OemBackslash => "BackSlash", Keys.OemCloseBrackets => "BracketRight", Keys.OemQuotes => "Apostrophe", Keys.Apps => "Menu", _ => null,
        };
    }

}
