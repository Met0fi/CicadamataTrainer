extends Node

const CONFIG_PATH := "user://cicada_trainer.cfg"
const LOG_PATH := "user://cicada_trainer.log"
const STATUS_PATH := "user://cicada_trainer.status"
const FONT_PATH := "user://cicada_trainer_font.otf"
const HUD_LAYER := 1024
const IFRAME_HOLD := 40
const ENEMY_KILL_ENERGIE := 0.1
const ENEMY_SCAN_INTERVAL := 4
const HUD_REFRESH_INTERVAL := 20
const CONFIG_POLL_INTERVAL := 12
const HEARTBEAT_INTERVAL := 60
const STATUS_LOG_INTERVAL := 900
const SOUND_RATE := 22050
const SOUND_LEVEL := 0.42
const CRUNCH_STEPS := 16.0
const AUTHOR_TEXT := "BY M:/T"
const HUD_MIN_SIZE := Vector2(200.0, 100.0)
const HUD_TEXT_MIN_WIDTH := 170.0
const HUD_DEFAULT_SIZE := Vector2(380.0, 328.0)
const HUD_DEFAULT_POSITION := Vector2(18.0, 18.0)
const HUD_GRIP := 22.0
const HUD_FONT_DEFAULT := 16
const HUD_FONT_MIN := 9
const HUD_FONT_MAX := 30
const DRAG_NONE := 0
const DRAG_MOVE := 1
const DRAG_RESIZE := 2
const CORE_OFFSET := Vector3(0.0, 1.0, 0.0)
const BULK_SOUND_THRESHOLD := 3

const CHEAT_ORDER := ["god", "breath", "jumps", "dash", "onehit", "noreload"]

const CHEAT_NOTES := {
	"god": 523.25,
	"breath": 587.33,
	"jumps": 659.25,
	"dash": 783.99,
	"onehit": 880.0,
	"noreload": 1046.5,
}

const CHEAT_LABELS := {
	"god": "god mode",
	"breath": "underwater safety",
	"jumps": "infinite jumps",
	"dash": "infinite dash",
	"onehit": "one-bullet kill",
	"noreload": "no weapon recharge",
}

const HOTKEY_DEFAULTS := {
	"god": "F1",
	"breath": "F2",
	"jumps": "F3",
	"dash": "F4",
	"onehit": "F5",
	"noreload": "F6",
	"hud": "F7",
	"panic": "F8",
	"allon": "F9",
	"layout": "F10",
	"teleport": "F11",
	"killall": "F12",
	"teleportexit": "End",
}

const SOUND_BUSES := ["SFX", "UI", "Sound", "Master"]

var state: Dictionary = {}
var hotkeys: Dictionary = {}
var keycodes: Dictionary = {}
var hud_visible := true
var hud_locked := true
var hud_position := HUD_DEFAULT_POSITION
var hud_size := HUD_DEFAULT_SIZE
var hud_font_size := HUD_FONT_DEFAULT
var sounds_enabled := true

var _player: Node = null
var _log_file: FileAccess = null
var _layer: CanvasLayer = null
var _panel: PanelContainer = null
var _style: StyleBoxFlat = null
var _box: VBoxContainer = null
var _label: Label = null
var _author: Label = null
var _held: Dictionary = {}
var _frame := 0
var _enemy_count := 0
var _last_status_frame := 0
var _drag_mode := DRAG_NONE
var _showmouse_backup := false
var _cursor_owned := false
var _mouse_mode_backup := Input.MOUSE_MODE_VISIBLE
var _editing := false
var _edit_context: Array = []
var _hud_font: FontFile = null
var _teleport_counter := 0
var _action_counters := {"killall": 0, "teleportexit": 0}
var _players: Dictionary = {}
var _panic_player: AudioStreamPlayer = null
var _allon_player: AudioStreamPlayer = null
var _hud_player: AudioStreamPlayer = null
var _teleport_player: AudioStreamPlayer = null


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	process_priority = 1000
	process_physics_priority = 1000
	_load_config()
	_open_log()
	_build_hud.call_deferred()
	_build_audio.call_deferred()
	_write_heartbeat()
	_log("trainer loaded, config=%s sounds=%s" % [CONFIG_PATH, str(sounds_enabled)])
	print("[CicadaTrainer] ready; cheats=%s" % _state_summary())


func _exit_tree() -> void:
	_release_cursor()
	_save_config()
	_write_heartbeat(0)
	_log("trainer unloaded")
	if _log_file != null:
		_log_file.flush()
		_log_file = null


func _process(_delta: float) -> void:
	_update_panel_interactive()


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and _editing:
		_set_layout_locked(true)


func _input(event: InputEvent) -> void:
	if !_editing:
		return
	if event is InputEventKey and event.pressed and !event.echo:
		if event.keycode == KEY_ESCAPE or !keycodes.values().has(event.keycode):
			_set_layout_locked(true)
	elif event is InputEventMouseButton and event.pressed and event.button_index in [MOUSE_BUTTON_LEFT, MOUSE_BUTTON_RIGHT]:
		if _panel != null and !_panel.get_global_rect().has_point(event.position):
			_set_layout_locked(true)


func _physics_process(_delta: float) -> void:
	_poll_hotkeys()
	_frame += 1
	if _frame % CONFIG_POLL_INTERVAL == 0:
		_pull_config_changes()
	if _frame % HEARTBEAT_INTERVAL == 0:
		_write_heartbeat()
	if get_tree().paused:
		return
	var player := _find_player()
	if player != null:
		_apply_player(player)
	if state["onehit"] and _frame % ENEMY_SCAN_INTERVAL == 0:
		_apply_enemies()
	if _frame % HUD_REFRESH_INTERVAL == 0:
		_refresh_hud()
	if _frame - _last_status_frame >= STATUS_LOG_INTERVAL:
		_last_status_frame = _frame
		_log("status player=%s enemies=%d cheats=%s" % [str(player != null), _enemy_count, _state_summary()])


func _find_player() -> Node:
	if is_instance_valid(_player):
		return _player
	var found := get_tree().get_nodes_in_group("player")
	_player = found[0] if !found.is_empty() else null
	if _player != null:
		_log("player node acquired: %s" % _player.get_path())
	return _player


func _apply_player(player: Node) -> void:
	var max_energie = _read_property(player, "max_energie")
	if state["god"]:
		_hold_player_alive(player, max_energie)
	if state["breath"] and _read_property(player, "underwater", false) == true:
		_hold_player_alive(player, max_energie)
	if state["jumps"]:
		_write_property(player, "can_multijump", true)
		_write_property(player, "jump_amnt", 0.0)
		_write_property(player, "jump_missed", false)
	if state["dash"]:
		_write_property(player, "dash_timer", 0)
		_write_property(player, "dash_timer_start", false)
		_write_property(player, "spawncooldown", 0)
	if state["noreload"]:
		_write_property(player, "_reload", 0)


func _hold_player_alive(player: Node, max_energie) -> void:
	_write_property(player, "iframes", IFRAME_HOLD)
	if max_energie != null and float(max_energie) > 0.0:
		_write_property(player, "energie", max_energie)


func _apply_enemies() -> void:
	var enemies := get_tree().get_nodes_in_group("enemy")
	_enemy_count = enemies.size()
	for enemy in enemies:
		var current = _read_property(enemy, "energie")
		if current == null:
			continue
		if float(current) > ENEMY_KILL_ENERGIE:
			enemy.set("energie", ENEMY_KILL_ENERGIE)


func _teleport_to_core() -> void:
	var player := _find_player()
	if player == null or !(player is Node3D):
		_log("teleport: player not found")
		return
	var cores := _find_cores()
	if cores.is_empty():
		_log("teleport: no cores in this level")
		return
	var origin: Vector3 = player.global_position
	var target: Node3D = cores[0]
	var best := INF
	for core in cores:
		var distance: float = origin.distance_to(core.global_position)
		if distance < best:
			best = distance
			target = core
	player.global_position = target.global_position + CORE_OFFSET
	_write_property(player, "velocity", Vector3.ZERO)
	_write_property(player, "vel_mod", Vector3.ZERO)
	_play(_teleport_player)
	_log("teleported to core, %.2f m away, %d cores left" % [best, cores.size()])


func _find_cores() -> Array:
	var found: Array = []
	var scene := get_tree().current_scene
	if scene != null:
		_collect_cores(scene, found)
	return found


func _kill_all_enemies() -> void:
	var player := _find_player()
	if player == null or _read_property(player, "death", false) or _read_property(player, "exfil", false) or !_read_property(player, "active", true):
		_log("kill all enemies: no active round")
		return
	var killed := 0
	for enemy in get_tree().get_nodes_in_group("enemy"):
		if !is_instance_valid(enemy) or enemy.is_queued_for_deletion() or _read_property(enemy, "_dead", false):
			continue
		if enemy.has_method("_deathstuff"):
			_write_property(enemy, "energie", 0.0)
			enemy.call("_deathstuff")
			killed += 1
	if killed > 0:
		_play(_panic_player)
	_log("kill all enemies: dispatched %d enemies" % killed)


func _teleport_to_exit() -> void:
	var player := _find_player()
	if player == null or !(player is Node3D) or _read_property(player, "death", false) or _read_property(player, "exfil", false) or !_read_property(player, "active", true):
		_log("teleport to exit: no active round")
		return
	var exits: Array = []
	var scene := get_tree().current_scene
	if scene != null:
		_collect_exits(scene, exits)
	if exits.is_empty():
		_log("teleport to exit: no exit in this level")
		return
	var target: Node3D = exits[0]
	var best := INF
	for exit in exits:
		var distance: float = player.global_position.distance_to(exit.global_position)
		if distance < best:
			best = distance
			target = exit
	player.global_position = target.global_position + CORE_OFFSET
	_write_property(player, "velocity", Vector3.ZERO)
	_write_property(player, "vel_mod", Vector3.ZERO)
	_write_property(player, "_prevelocity", Vector3.ZERO)
	_write_property(player, "spawncooldown", 0)
	_play(_teleport_player)
	_log("teleported to exit, %.2f m away; core requirement unchanged" % best)


func _collect_exits(node: Node, out: Array) -> void:
	if node is Node3D and !node.is_queued_for_deletion():
		var script: Script = node.get_script()
		if script != null:
			var path := script.resource_path
			if (path.ends_with("/exit.gd") or path.ends_with("/exit.gdc")) and _read_property(node, "_touched", false) != true:
				out.append(node)
	for child in node.get_children():
		_collect_exits(child, out)


func _collect_cores(node: Node, out: Array) -> void:
	for child in node.get_children():
		if _is_core(child):
			out.append(child)
		_collect_cores(child, out)


func _is_core(node: Node) -> bool:
	var script: Script = node.get_script()
	if script == null:
		return false
	var path := str(script.get("resource_path"))
	if !path.ends_with("core.gd") and !path.ends_with("core.gdc"):
		return false
	return node.get("_collect") != true


func _poll_hotkeys() -> void:
	for action in hotkeys:
		var code: int = keycodes.get(action, 0)
		if code == 0:
			continue
		var down := Input.is_key_pressed(code)
		if down and !_held.get(action, false):
			_on_hotkey(action)
		_held[action] = down


func _on_hotkey(action: String) -> void:
	if action == "hud":
		hud_visible = !hud_visible
		if !hud_visible:
			_set_layout_locked(true)
		_drag_mode = DRAG_NONE
		_apply_hud_visibility()
		_update_panel_interactive()
		_save_config()
		_play(_hud_player)
		return
	if action == "layout":
		_set_layout_locked(!hud_locked)
		return
	if action == "teleport":
		_teleport_to_core()
		return
	if action == "killall":
		_kill_all_enemies()
		return
	if action == "teleportexit":
		_teleport_to_exit()
		return
	if action == "panic":
		_set_all_cheats(false)
		_play(_panic_player)
		return
	if action == "allon":
		_set_all_cheats(true)
		_play(_allon_player)
		return
	state[action] = !state[action]
	_save_config()
	_play_toggle(action, state[action])
	_log("%s %s" % [action, "on" if state[action] else "off"])
	_refresh_hud()


func _set_all_cheats(enabled: bool) -> void:
	for cheat in CHEAT_ORDER:
		state[cheat] = enabled
	_save_config()
	_log("all cheats %s" % ("enabled" if enabled else "disabled"))
	_refresh_hud()


func _set_layout_locked(locked: bool) -> void:
	if !locked and !hud_visible:
		return
	if hud_locked == locked and _editing == !locked:
		return
	hud_locked = locked
	_editing = !locked
	_edit_context = _cursor_context() if _editing else []
	_drag_mode = DRAG_NONE
	_update_panel_interactive()
	_save_config()
	_log("layout %s" % ("locked" if locked else "unlocked"))
	_refresh_hud()


func _update_panel_interactive() -> void:
	if _panel == null:
		return
	_update_cursor()
	var interactive := hud_visible and !hud_locked and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE
	var wanted := Control.MOUSE_FILTER_STOP if interactive else Control.MOUSE_FILTER_IGNORE
	if _panel.mouse_filter != wanted:
		_panel.mouse_filter = wanted
		_update_panel_style(interactive)
	if !interactive:
		_drag_mode = DRAG_NONE


func _update_cursor() -> void:
	if !_editing or !hud_visible or hud_locked:
		hud_locked = true
		_release_cursor()
		return
	if _edit_context != _cursor_context():
		_set_layout_locked(true)
		return
	var global_state := get_tree().root.get_node_or_null("global")
	if !_cursor_owned:
		_showmouse_backup = bool(_read_property(global_state, "showmouse", Input.mouse_mode == Input.MOUSE_MODE_VISIBLE))
		_mouse_mode_backup = Input.mouse_mode
		_cursor_owned = true
	elif _read_property(global_state, "showmouse", true) == false:
		_showmouse_backup = false
		_mouse_mode_backup = Input.MOUSE_MODE_CAPTURED
	_write_property(global_state, "showmouse", true)
	if Input.mouse_mode != Input.MOUSE_MODE_VISIBLE:
		Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)


func _cursor_context() -> Array:
	var scene := get_tree().current_scene
	var player := _find_player()
	return [scene.get_instance_id() if scene != null else 0, player.get_instance_id() if player != null else 0,
		_read_property(player, "active", false), _read_property(player, "death", false),
		_read_property(player, "exfil", false), get_tree().paused]


func _gameplay_controls() -> bool:
	var player := _find_player()
	return player != null and !get_tree().paused and bool(_read_property(player, "active", false)) and !_read_property(player, "death", false) and !_read_property(player, "exfil", false)


func _release_cursor() -> void:
	if !_cursor_owned:
		return
	var global_state := get_tree().root.get_node_or_null("global")
	var restore_visible := _showmouse_backup
	if _gameplay_controls():
		restore_visible = false
	elif _read_property(global_state, "showmouse", _showmouse_backup) == false:
		restore_visible = false
	_write_property(global_state, "showmouse", restore_visible)
	var mode := _mouse_mode_backup
	if global_state != null and "showmouse" in global_state:
		mode = Input.MOUSE_MODE_VISIBLE if global_state.get("showmouse") and !_read_property(global_state, "_gamepad", false) else Input.MOUSE_MODE_CAPTURED
	_cursor_owned = false
	if Input.mouse_mode != mode:
		Input.set_mouse_mode(mode)


func _on_panel_gui_input(event: InputEvent) -> void:
	if _panel.mouse_filter != Control.MOUSE_FILTER_STOP:
		return
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP and event.pressed:
			_scale_font(1)
			_save_config()
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN and event.pressed:
			_scale_font(-1)
			_save_config()
		elif event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_drag_mode = DRAG_RESIZE if _in_grip(event.position) else DRAG_MOVE
			else:
				_drag_mode = DRAG_NONE
				_save_config()
		_panel.accept_event()
		return
	if event is InputEventMouseMotion and _drag_mode != DRAG_NONE:
		if _drag_mode == DRAG_RESIZE:
			_resize_hud(hud_size + event.relative)
		else:
			hud_position = _clamp_position(hud_position + event.relative)
			_panel.position = hud_position
		_panel.accept_event()


func _in_grip(point: Vector2) -> bool:
	return point.x >= hud_size.x - HUD_GRIP and point.y >= hud_size.y - HUD_GRIP


func _resize_hud(target: Vector2) -> void:
	hud_size = _clamp_size(target)
	_panel.size = hud_size


func _scale_font(step: int) -> void:
	hud_font_size = clampi(hud_font_size + step, HUD_FONT_MIN, HUD_FONT_MAX)
	_apply_font()


func _apply_font() -> void:
	if _label == null:
		return
	if _hud_font != null:
		_label.add_theme_font_override("font", _hud_font)
		_author.add_theme_font_override("font", _hud_font)
	_label.add_theme_font_size_override("font_size", hud_font_size)
	_author.add_theme_font_size_override("font_size", maxi(HUD_FONT_MIN, hud_font_size - 3))


func _apply_layout() -> void:
	if _panel == null:
		return
	hud_size = _clamp_size(hud_size)
	hud_position = _clamp_position(hud_position)
	hud_font_size = clampi(hud_font_size, HUD_FONT_MIN, HUD_FONT_MAX)
	_panel.size = hud_size
	_panel.position = hud_position
	_panel.custom_minimum_size = HUD_MIN_SIZE
	_apply_font()


func _clamp_size(size: Vector2) -> Vector2:
	var room := _viewport_size()
	var safe := Vector2(
		size.x if is_finite(size.x) else HUD_DEFAULT_SIZE.x,
		size.y if is_finite(size.y) else HUD_DEFAULT_SIZE.y)
	return Vector2(
		clampf(safe.x, HUD_MIN_SIZE.x, maxf(HUD_MIN_SIZE.x, room.x)),
		clampf(safe.y, HUD_MIN_SIZE.y, maxf(HUD_MIN_SIZE.y, room.y)))


func _clamp_position(position: Vector2) -> Vector2:
	var room := _viewport_size()
	var safe := Vector2(
		position.x if is_finite(position.x) else HUD_DEFAULT_POSITION.x,
		position.y if is_finite(position.y) else HUD_DEFAULT_POSITION.y)
	return Vector2(
		clampf(safe.x, 0.0, maxf(0.0, room.x - 64.0)),
		clampf(safe.y, 0.0, maxf(0.0, room.y - 32.0)))


func _viewport_size() -> Vector2:
	var size := Vector2(1920.0, 1080.0)
	if is_inside_tree():
		var rect := get_viewport().get_visible_rect().size
		if is_finite(rect.x) and is_finite(rect.y) and rect.x >= 480.0 and rect.y >= 270.0:
			size = rect
	return size


func _on_viewport_resized() -> void:
	if _panel == null:
		return
	_apply_layout()


func _build_hud() -> void:
	if FileAccess.file_exists(FONT_PATH):
		_hud_font = FontFile.new()
		_hud_font.data = FileAccess.get_file_as_bytes(FONT_PATH)
	elif ResourceLoader.exists("res://Assets/Fonts/bitpop.otf"):
		_hud_font = load("res://Assets/Fonts/bitpop.otf").duplicate() as FontFile
	if _hud_font != null:
		_hud_font.antialiasing = TextServer.FONT_ANTIALIASING_NONE
		_hud_font.hinting = TextServer.HINTING_NONE
	_layer = CanvasLayer.new()
	_layer.name = "CicadaTrainerHud"
	_layer.layer = HUD_LAYER
	add_child(_layer)

	_panel = PanelContainer.new()
	_panel.name = "CicadaTrainerPanel"
	_panel.set_anchors_preset(Control.PRESET_TOP_LEFT)
	_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_panel.gui_input.connect(_on_panel_gui_input)
	_layer.add_child(_panel)

	_style = StyleBoxFlat.new()
	_style.bg_color = Color(0.06, 0.02, 0.03, 0.72)
	_style.set_corner_radius_all(3)
	_style.content_margin_left = 10.0
	_style.content_margin_right = 10.0
	_style.content_margin_top = 7.0
	_style.content_margin_bottom = 7.0
	_panel.add_theme_stylebox_override("panel", _style)
	_update_panel_style(false)

	_box = VBoxContainer.new()
	_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_box.add_theme_constant_override("separation", 1)
	_panel.add_child(_box)

	_label = Label.new()
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_label.custom_minimum_size = Vector2(HUD_TEXT_MIN_WIDTH, 0.0)

	_author = Label.new()
	_author.text = AUTHOR_TEXT
	_author.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_author.add_theme_color_override("font_color", Color(0.66, 0.42, 0.98))

	_box.add_child(_label)
	_box.add_child(_author)

	_apply_layout()
	_apply_hud_visibility()
	_refresh_hud()

	var viewport := get_viewport()
	if viewport != null and !viewport.size_changed.is_connected(_on_viewport_resized):
		viewport.size_changed.connect(_on_viewport_resized)


func _update_panel_style(interactive: bool) -> void:
	if _style == null:
		return
	_style.border_color = Color(1.0, 0.45, 0.52, 0.95) if interactive else Color(0.77, 0.17, 0.28, 0.9)
	_style.set_border_width_all(2 if interactive else 1)


func _apply_hud_visibility() -> void:
	if _layer != null:
		_layer.visible = hud_visible


func _refresh_hud() -> void:
	if _label == null:
		return
	var lines := PackedStringArray()
	for cheat in CHEAT_ORDER:
		lines.append("%-3s  %-19s %s" % [hotkeys[cheat], CHEAT_LABELS[cheat], _on_off(state[cheat])])
	lines.append("%-3s  hide GUI" % hotkeys["hud"])
	lines.append("%-3s  disable all" % hotkeys["panic"])
	lines.append("%-3s  enable all" % hotkeys["allon"])
	lines.append("%-3s  lock layout" % hotkeys["layout"])
	lines.append("%-3s  teleport to core" % hotkeys["teleport"])
	lines.append("%-3s  kill all enemies" % hotkeys["killall"])
	lines.append("%-3s  teleport to exit" % hotkeys["teleportexit"])
	if _editing:
		lines.append("Editing HUD - Esc to finish")
	_label.text = "\n".join(lines)


func _read_config() -> Dictionary:
	var data := {
		"cheats": {},
		"hotkeys": hotkeys.duplicate() if !hotkeys.is_empty() else HOTKEY_DEFAULTS.duplicate(),
		"hud": hud_visible,
		"locked": hud_locked,
		"sound": sounds_enabled,
		"position": hud_position,
		"size": hud_size,
		"font": hud_font_size,
		"actions": _teleport_counter,
		"requests": _action_counters.duplicate(),
		"repair": false,
		"write_hotkeys": false,
	}
	for cheat in CHEAT_ORDER:
		data["cheats"][cheat] = state.get(cheat, false)
	var config := ConfigFile.new()
	if config.load(CONFIG_PATH) == OK:
		for cheat in CHEAT_ORDER:
			data["cheats"][cheat] = bool(config.get_value("cheats", cheat, false))
		for action in HOTKEY_DEFAULTS:
			data["hotkeys"][action] = str(config.get_value("hotkeys", action, HOTKEY_DEFAULTS[action]))
		for action in ["killall", "teleportexit"]:
			if config.has_section_key("hotkeys", action):
				continue
			data["write_hotkeys"] = true
			var occupied := []
			for other in HOTKEY_DEFAULTS:
				if other != action:
					occupied.append(str(data["hotkeys"][other]).to_upper())
			if occupied.has(str(data["hotkeys"][action]).to_upper()):
				var candidates := ["F12", "End", "Home", "Insert", "PageUp", "PageDown", "Delete"]
				for code in range(KEY_A, KEY_Z + 1):
					candidates.append(OS.get_keycode_string(code))
				for key in candidates:
					if !occupied.has(key.to_upper()):
						data["hotkeys"][action] = key
						break
		data["hud"] = bool(config.get_value("hud", "visible", true))
		data["locked"] = bool(config.get_value("hud", "locked", true))
		data["sound"] = bool(config.get_value("sound", "enabled", true))
		data["position"] = Vector2(
			float(config.get_value("hud", "position_x", HUD_DEFAULT_POSITION.x)),
			float(config.get_value("hud", "position_y", HUD_DEFAULT_POSITION.y)))
		data["size"] = Vector2(
			float(config.get_value("hud", "size_x", HUD_DEFAULT_SIZE.x)),
			float(config.get_value("hud", "size_y", HUD_DEFAULT_SIZE.y)))
		data["font"] = int(config.get_value("hud", "font_size", HUD_FONT_DEFAULT))
		data["actions"] = int(config.get_value("actions", "teleport", 0))
		for action in _action_counters:
			data["requests"][action] = int(config.get_value("actions", action, 0))
		var legacy_defaults := true
		var used := {}
		for action in HOTKEY_DEFAULTS:
			var key: String = data["hotkeys"][action]
			if key == "Page Up":
				key = "PageUp"
			elif key == "Page Down":
				key = "PageDown"
			data["hotkeys"][action] = key
			if used.has(key.to_upper()):
				data["repair"] = true
			used[key.to_upper()] = true
			var old_default: String = "F10" if action == "allon" else "F9" if action == "layout" else HOTKEY_DEFAULTS[action]
			if key != old_default:
				legacy_defaults = false
		if int(config.get_value("hotkeys", "schema", 0)) < 2 and legacy_defaults:
			data["repair"] = true
		if data["repair"]:
			data["hotkeys"] = HOTKEY_DEFAULTS.duplicate()
	return data


func _apply_config(data: Dictionary, apply_layout: bool) -> void:
	state = data["cheats"].duplicate()
	hotkeys = data["hotkeys"].duplicate()
	hud_visible = data["hud"]
	hud_locked = data["locked"] if _editing else true
	if hud_locked or !hud_visible:
		_editing = false
	sounds_enabled = data["sound"]
	if apply_layout:
		hud_position = data["position"]
		hud_size = data["size"]
		hud_font_size = data["font"]
		_apply_layout()
	var previous_codes := keycodes
	keycodes = {}
	for action in hotkeys:
		keycodes[action] = OS.find_keycode_from_string(hotkeys[action])
		if previous_codes.get(action, -1) != keycodes[action]:
			_held[action] = Input.is_key_pressed(keycodes[action]) if keycodes[action] != 0 else false
	_apply_hud_visibility()
	_update_panel_interactive()


func _load_config() -> void:
	var data := _read_config()
	_apply_config(data, true)
	_teleport_counter = int(data["actions"])
	_action_counters = data["requests"].duplicate()
	if data["repair"] or data["write_hotkeys"]:
		_save_config()


func _pull_config_changes() -> void:
	var fresh := _read_config()
	var previous := state.duplicate()
	var previous_sounds := sounds_enabled
	_apply_config(fresh, _drag_mode == DRAG_NONE)
	if sounds_enabled != previous_sounds:
		_log("sounds %s" % ("on" if sounds_enabled else "off"))
	var changed := 0
	for cheat in CHEAT_ORDER:
		if bool(previous[cheat]) != bool(state[cheat]):
			changed += 1
	if changed >= BULK_SOUND_THRESHOLD:
		_play(_allon_player if _enabled_count() == CHEAT_ORDER.size() else _panic_player)
		_log("cheats changed externally: %s" % _state_summary())
	elif changed > 0:
		for cheat in CHEAT_ORDER:
			if bool(previous[cheat]) != bool(state[cheat]):
				_play_toggle(cheat, state[cheat])
				_log("%s %s (external)" % [cheat, "on" if state[cheat] else "off"])
	if int(fresh["actions"]) != _teleport_counter:
		_teleport_counter = int(fresh["actions"])
		_teleport_to_core()
	for action in _action_counters:
		if int(fresh["requests"][action]) != int(_action_counters[action]):
			_action_counters[action] = int(fresh["requests"][action])
			if action == "killall":
				_kill_all_enemies()
			else:
				_teleport_to_exit()
	_refresh_hud()
	if fresh["repair"] or fresh["write_hotkeys"]:
		_save_config()


func _save_config() -> void:
	var config := ConfigFile.new()
	config.load(CONFIG_PATH)
	for cheat in CHEAT_ORDER:
		config.set_value("cheats", cheat, state[cheat])
	for action in hotkeys:
		config.set_value("hotkeys", action, hotkeys[action])
	config.set_value("hotkeys", "schema", 2)
	config.set_value("hud", "visible", hud_visible)
	config.set_value("hud", "locked", hud_locked)
	config.set_value("hud", "position_x", hud_position.x)
	config.set_value("hud", "position_y", hud_position.y)
	config.set_value("hud", "size_x", hud_size.x)
	config.set_value("hud", "size_y", hud_size.y)
	config.set_value("hud", "font_size", hud_font_size)
	config.set_value("sound", "enabled", sounds_enabled)
	config.set_value("actions", "teleport", _teleport_counter)
	for action in _action_counters:
		config.set_value("actions", action, _action_counters[action])
	var temporary := CONFIG_PATH + ".godot.tmp"
	var error := config.save(temporary)
	if error == OK:
		error = DirAccess.rename_absolute(ProjectSettings.globalize_path(temporary), ProjectSettings.globalize_path(CONFIG_PATH))
	if error != OK:
		_log("failed to save config, error %d" % error)


func _write_heartbeat(pid := -1) -> void:
	var file := FileAccess.open(STATUS_PATH, FileAccess.WRITE)
	if file == null:
		return
	file.store_line("pid=%d" % (OS.get_process_id() if pid < 0 else pid))
	file.store_line("time=%d" % Time.get_unix_time_from_system())
	file.store_line("cheats=%s" % _state_summary())
	file.close()


func _build_audio() -> void:
	var bus := _sound_bus()
	for cheat in CHEAT_ORDER:
		var note: float = CHEAT_NOTES[cheat]
		_players["%s:on" % cheat] = _make_player(_wrap(_render_tone(note, note * 1.5, 120)), bus)
		_players["%s:off" % cheat] = _make_player(_wrap(_render_tone(note, note * 0.62, 95)), bus)
	var panic_pcm := PackedByteArray()
	for hz in [880.0, 659.25, 440.0]:
		panic_pcm.append_array(_render_tone(hz, hz * 0.6, 58))
	_panic_player = _make_player(_wrap(panic_pcm), bus)
	var allon_pcm := PackedByteArray()
	for hz in [440.0, 659.25, 880.0]:
		allon_pcm.append_array(_render_tone(hz, hz * 1.4, 58))
	_allon_player = _make_player(_wrap(allon_pcm), bus)
	_hud_player = _make_player(_wrap(_render_tone(392.0, 330.0, 45)), bus)
	_teleport_player = _make_player(_wrap(_render_tone(300.0, 1200.0, 140)), bus)


func _make_player(stream: AudioStreamWAV, bus: String) -> AudioStreamPlayer:
	var player := AudioStreamPlayer.new()
	player.stream = stream
	player.bus = bus
	player.volume_db = -6.0
	add_child(player)
	return player


func _sound_bus() -> String:
	for name in SOUND_BUSES:
		if AudioServer.get_bus_index(name) != -1:
			return name
	return "Master"


func _wrap(pcm: PackedByteArray) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = SOUND_RATE
	stream.stereo = false
	stream.data = pcm
	return stream


func _render_tone(start_hz: float, end_hz: float, milliseconds: int) -> PackedByteArray:
	var count := int(SOUND_RATE * milliseconds / 1000.0)
	var pcm := PackedByteArray()
	pcm.resize(count * 2)
	var rng := RandomNumberGenerator.new()
	rng.seed = int(start_hz * 1000.0)
	var phase := 0.0
	var attack := maxi(1, int(count * 0.02))
	var decay := float(count) / 2.4
	for i in count:
		var progress := float(i) / float(count)
		phase += TAU * lerpf(start_hz, end_hz, progress * progress) / float(SOUND_RATE)
		var square := 1.0 if sin(phase) >= 0.0 else -1.0
		var saw := fmod(phase, TAU) / PI - 1.0
		var click := rng.randf_range(-1.0, 1.0) * 0.35 * exp(-progress * 26.0)
		var envelope := float(i) / float(attack) if i < attack else 1.0
		var sample := (square * 0.6 + saw * 0.4 + click) * envelope * exp(-float(i) / decay) * SOUND_LEVEL
		sample = clampf(sample, -1.0, 1.0)
		sample = round(sample * CRUNCH_STEPS) / CRUNCH_STEPS
		pcm.encode_s16(i * 2, int(sample * 32000.0))
	return pcm


func _play_toggle(cheat: String, enabled: bool) -> void:
	_play(_players.get("%s:%s" % [cheat, "on" if enabled else "off"]))


func _play(player) -> void:
	if !sounds_enabled or player == null:
		return
	player.play()


func _open_log() -> void:
	_log_file = FileAccess.open(LOG_PATH, FileAccess.WRITE)
	if _log_file == null:
		push_warning("CicadaTrainer could not open %s" % LOG_PATH)


func _log(line: String) -> void:
	if _log_file == null:
		return
	_log_file.store_line("%s  %s" % [Time.get_time_string_from_system(), line])
	_log_file.flush()


func _enabled_count() -> int:
	var count := 0
	for cheat in CHEAT_ORDER:
		if state[cheat]:
			count += 1
	return count


func _state_summary() -> String:
	var parts := PackedStringArray()
	for cheat in CHEAT_ORDER:
		if state[cheat]:
			parts.append(cheat)
	return "none" if parts.is_empty() else ",".join(parts)


func _on_off(value: bool) -> String:
	return "ON" if value else "off"


func _read_property(object: Object, property: String, fallback = null):
	if object == null or !(property in object):
		return fallback
	return object.get(property)


func _write_property(object: Object, property: String, value) -> bool:
	if object == null or !(property in object):
		return false
	object.set(property, value)
	return true
