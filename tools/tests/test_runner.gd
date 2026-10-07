extends Node

var _failures := 0
var _checks := 0
var _trainer: Node
var _player: Node
var _enemy: Node
var _core: Node
var _global: Node


func _ready() -> void:
	await get_tree().physics_frame
	var clean := ConfigFile.new()
	clean.set_value("hotkeys", "schema", 2)
	clean.set_value("hud", "locked", true)
	clean.save("res://userdata/cicada_trainer.cfg")
	_global = load("res://stubs/global_stub.gd").new()
	_global.name = "global"
	get_tree().root.add_child(_global)
	_player = load("res://stubs/player_stub.gd").new()
	_player.name = "Player"
	get_tree().root.add_child(_player)
	_enemy = load("res://stubs/enemy_stub.gd").new()
	_enemy.name = "Enemy"
	get_tree().root.add_child(_enemy)
	var inert := Node.new()
	inert.name = "InertEnemy"
	inert.add_to_group("enemy")
	get_tree().root.add_child(inert)
	_core = load("res://stubs/core.gd").new()
	_core.name = "Core"
	add_child(_core)

	_trainer = load("res://mod/cicada_trainer.gd").new()
	_trainer.name = "CicadaTrainer"
	get_tree().root.add_child(_trainer)
	await _frames(6)

	_check("hud built", _trainer._label != null and _trainer._label.text.contains("god mode"))
	_check("hud full names", _trainer._label.text.contains("underwater safety") and _trainer._label.text.contains("infinite jumps") and _trainer._label.text.contains("no weapon recharge"))
	_check("hud lists bulk keys", _trainer._label.text.contains("hide GUI") and _trainer._label.text.contains("disable all") and _trainer._label.text.contains("enable all") and _trainer._label.text.contains("teleport to core"))
	_check("HUD lists actions in F1 through F11 order", _trainer._label.text.find("hide GUI") < _trainer._label.text.find("disable all") and _trainer._label.text.find("disable all") < _trainer._label.text.find("enable all") and _trainer._label.text.find("enable all") < _trainer._label.text.find("lock layout") and _trainer._label.text.find("lock layout") < _trainer._label.text.find("teleport to core"))
	_check("hud author line", _trainer._author.text == "BY M:/T")
	_check("hotkey map", _trainer.keycodes["god"] == KEY_F1 and _trainer.keycodes["allon"] == KEY_F9 and _trainer.keycodes["layout"] == KEY_F10 and _trainer.keycodes["teleport"] == KEY_F11)
	_check("defaults off", _trainer._state_summary() == "none")
	_check("sound bank size", _trainer._players.size() == 12 and _trainer._allon_player != null and _trainer._teleport_player != null)
	_check("new actions have default keys and HUD labels", _trainer.keycodes["killall"] == KEY_F12 and _trainer.keycodes["teleportexit"] == KEY_END and _trainer._label.text.contains("kill all enemies") and _trainer._label.text.contains("teleport to exit"))
	_check("HUD uses the extracted original Bitpop font", _trainer._label.get_theme_font("font").get_font_name() == "Bitpop" and _trainer._author.get_theme_font("font").get_font_name() == "Bitpop")
	_check("pixel font avoids antialiasing", _trainer._hud_font.antialiasing == TextServer.FONT_ANTIALIASING_NONE)

	await _test_god()
	await _test_breath()
	await _test_jumps()
	await _test_dash()
	await _test_noreload()
	await _test_onehit()
	await _test_toggle_sounds()
	await _test_single_sound()
	await _test_external_config()
	await _test_bulk_change()
	await _test_sound_switch()
	await _test_panic()
	await _test_allon()
	await _test_teleport()
	await _test_heartbeat()
	await _test_f7()
	await _test_cursor()
	await _test_cursor_lifetime()
	await _test_bind_repair()
	await _test_layout()
	await _test_pause()
	await _test_new_actions()
	await _test_config_roundtrip()

	print("checks=%d failures=%d" % [_checks, _failures])
	print("TEST OK" if _failures == 0 else "TEST FAILED")
	get_tree().quit(0 if _failures == 0 else 1)


func _test_god() -> void:
	_reset_player()
	_set_cheat("god", true)
	await _frames(2)
	_check("god refills energie", _player.energie == _player.max_energie)
	_check("god holds iframes", _player.iframes == 40)
	_set_cheat("god", false)


func _test_breath() -> void:
	_reset_player()
	_player.underwater = false
	_set_cheat("breath", true)
	await _frames(2)
	_check("breath idle on land", _player.energie == 1.0 and _player.iframes == 0)
	_player.underwater = true
	await _frames(2)
	_check("breath holds underwater", _player.energie == _player.max_energie and _player.iframes == 40)
	_set_cheat("breath", false)


func _test_jumps() -> void:
	_reset_player()
	_player.can_multijump = false
	_player.jump_amnt = 2.5
	_player.jump_missed = true
	_set_cheat("jumps", true)
	await _frames(2)
	_check("jumps rearm multijump", _player.can_multijump == true)
	_check("jumps clear counter", _player.jump_amnt == 0.0)
	_check("jumps clear miss flag", _player.jump_missed == false)
	_set_cheat("jumps", false)


func _test_dash() -> void:
	_reset_player()
	_player.dash_timer = 99
	_player.dash_timer_start = true
	_player.spawncooldown = 5
	_set_cheat("dash", true)
	await _frames(2)
	_check("dash clears cooldown", _player.dash_timer == 0)
	_check("dash clears timer flag", _player.dash_timer_start == false)
	_check("dash clears spawn gate", _player.spawncooldown == 0)
	_set_cheat("dash", false)


func _test_noreload() -> void:
	_reset_player()
	_player._reload = 70
	_set_cheat("noreload", true)
	await _frames(2)
	_check("weapon reload cleared", _player._reload == 0)
	_set_cheat("noreload", false)


func _test_onehit() -> void:
	_reset_player()
	_enemy.energie = 4.0
	await _frames(6)
	_check("enemy untouched when off", _enemy.energie == 4.0)
	_set_cheat("onehit", true)
	await _frames(6)
	_check("enemy drops to one-bullet health", _enemy.energie == 0.1)
	_set_cheat("onehit", false)


func _test_toggle_sounds() -> void:
	_reset_player()
	var on_player: AudioStreamPlayer = _trainer._players["god:on"]
	var off_player: AudioStreamPlayer = _trainer._players["god:off"]
	on_player.stop()
	off_player.stop()
	_trainer._on_hotkey("god")
	_check("toggle on plays sound", on_player.playing)
	_check("toggle on sets state", _trainer.state["god"] == true)
	_trainer._on_hotkey("god")
	_check("toggle off plays sound", off_player.playing)
	_check("toggle off sets state", _trainer.state["god"] == false)
	await _frames(2)


func _test_single_sound() -> void:
	_all_players_stop()
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("cheats", "jumps", true)
	config.save("res://userdata/cicada_trainer.cfg")
	var flipped := false
	for i in 40:
		await get_tree().physics_frame
		if _trainer.state["jumps"]:
			flipped = true
			break
	_check("external single change applies", flipped)
	_check("external single change plays exactly one sound", _playing_count() == 1)
	_set_cheat("jumps", false)
	await _frames(4)


func _test_external_config() -> void:
	_reset_player()
	_enemy.energie = 4.0
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("cheats", "onehit", true)
	config.set_value("cheats", "god", true)
	config.save("res://userdata/cicada_trainer.cfg")
	var sound: AudioStreamPlayer = _trainer._players["god:on"]
	sound.stop()
	var flipped := -1
	var played := false
	for i in 40:
		await get_tree().physics_frame
		if _trainer.state["god"] and _trainer.state["onehit"]:
			if flipped == -1:
				flipped = i
			if sound.playing:
				played = true
				break
			if i - flipped > 4:
				break
	_check("external toggles reach state", flipped >= 0)
	_check("external toggles play a sound", played)
	await _frames(8)
	_check("external toggles apply cheats", _player.energie == _player.max_energie and _enemy.energie == 0.1)
	_set_cheat("god", false)
	_set_cheat("onehit", false)
	await _frames(4)


func _test_bulk_change() -> void:
	_all_players_stop()
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	for cheat in _trainer.CHEAT_ORDER:
		config.set_value("cheats", cheat, true)
	config.save("res://userdata/cicada_trainer.cfg")
	var done := false
	for i in 40:
		await get_tree().physics_frame
		if _trainer._enabled_count() == 6:
			done = true
			break
	_check("bulk change reaches state", done)
	_check("bulk change plays one sound", _playing_count() == 1)
	_check("bulk change uses the enable-all sound", _trainer._allon_player.playing)
	_all_players_stop()
	config.load("res://userdata/cicada_trainer.cfg")
	for cheat in _trainer.CHEAT_ORDER:
		config.set_value("cheats", cheat, false)
	config.save("res://userdata/cicada_trainer.cfg")
	for i in 40:
		await get_tree().physics_frame
		if _trainer._enabled_count() == 0:
			break
	_check("bulk disable leaves nothing on", _trainer._enabled_count() == 0)
	_all_players_stop()


func _test_sound_switch() -> void:
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("sound", "enabled", false)
	config.save("res://userdata/cicada_trainer.cfg")
	var disabled := false
	for i in 40:
		await get_tree().physics_frame
		if !_trainer.sounds_enabled:
			disabled = true
			break
	_check("sound switch read", disabled)
	var player: AudioStreamPlayer = _trainer._players["jumps:on"]
	player.stop()
	_trainer._on_hotkey("jumps")
	_check("muted toggle silent", player.playing == false)
	_check("muted toggle still works", _trainer.state["jumps"] == true)
	_trainer._on_hotkey("jumps")
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("sound", "enabled", true)
	config.save("res://userdata/cicada_trainer.cfg")
	var restored := false
	for i in 40:
		await get_tree().physics_frame
		if _trainer.sounds_enabled:
			restored = true
			break
	_check("sound switch restored", restored)


func _test_panic() -> void:
	_set_cheat("god", true)
	_set_cheat("onehit", true)
	_trainer._panic_player.stop()
	_trainer._on_hotkey("panic")
	_check("panic disables everything", _trainer._state_summary() == "none")
	_check("panic plays sound", _trainer._panic_player.playing)
	_check("panic writes config", FileAccess.file_exists("res://userdata/cicada_trainer.cfg"))


func _test_allon() -> void:
	_all_players_stop()
	_trainer._on_hotkey("allon")
	_check("enable all turns every cheat on", _trainer._enabled_count() == 6)
	_check("enable all plays sound", _trainer._allon_player.playing)
	_trainer._on_hotkey("panic")
	_check("disable all turns every cheat off", _trainer._enabled_count() == 0)


func _test_teleport() -> void:
	_reset_player()
	_player.global_position = Vector3.ZERO
	_trainer._teleport_player.stop()
	_trainer._on_hotkey("teleport")
	_check("teleport moves the player to the core", _player.global_position.is_equal_approx(_core.global_position + _trainer.CORE_OFFSET))
	_check("teleport plays sound", _trainer._teleport_player.playing)

	_player.global_position = Vector3.ZERO
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("actions", "teleport", int(config.get_value("actions", "teleport", 0)) + 1)
	config.save("res://userdata/cicada_trainer.cfg")
	var moved := false
	for i in 40:
		await get_tree().physics_frame
		if _player.global_position != Vector3.ZERO:
			moved = true
			break
	_check("teleport request from the app works", moved and _player.global_position.is_equal_approx(_core.global_position + _trainer.CORE_OFFSET))

	_core._collect = true
	_player.global_position = Vector3.ZERO
	_trainer._on_hotkey("teleport")
	_check("collected core is skipped", _player.global_position == Vector3.ZERO)
	_core._collect = false


func _test_heartbeat() -> void:
	var heartbeat := FileAccess.open(_trainer.STATUS_PATH, FileAccess.READ)
	_check("heartbeat file exists", heartbeat != null)
	if heartbeat == null:
		return
	var text := heartbeat.get_as_text()
	heartbeat.close()
	_check("heartbeat carries the process id", text.contains("pid=%d" % OS.get_process_id()))
	_check("heartbeat carries a timestamp", text.contains("time="))


func _key(code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)


func _test_f7() -> void:
	if !_trainer.hud_visible:
		_trainer._on_hotkey("hud")
	_key(KEY_F7, true)
	await _frames(3)
	_check("F7 keyboard event hides the entire HUD", !_trainer.hud_visible and !_trainer._layer.visible)
	await _frames(45)
	_check("F7 stays hidden after config refresh and while key held", !_trainer.hud_visible and !_trainer._layer.visible)
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	_check("F7 persists visibility to disk", config.get_value("hud", "visible", true) == false)
	var reloaded: Node = load("res://mod/cicada_trainer.gd").new()
	reloaded.name = "HiddenReload"
	get_tree().root.add_child(reloaded)
	await _frames(3)
	_check("hidden HUD remains hidden after reloading the script", !reloaded.hud_visible and !reloaded._layer.visible)
	reloaded.queue_free()
	await _frames(2)
	_key(KEY_F7, false)
	await _frames(2)
	var path := ProjectSettings.globalize_path("res://userdata/cicada_trainer.cfg")
	DirAccess.rename_absolute(path, path + ".read-test")
	_trainer._pull_config_changes()
	_check("temporarily unavailable config cannot restore hidden HUD", !_trainer.hud_visible)
	DirAccess.rename_absolute(path + ".read-test", path)
	_key(KEY_F7, true)
	await _frames(3)
	_check("second F7 keyboard press restores HUD", _trainer.hud_visible and _trainer._layer.visible)
	_key(KEY_F7, false)
	await _frames(2)
	get_tree().paused = true
	_key(KEY_F7, true)
	await _frames(3)
	_check("F7 also works while paused", !_trainer.hud_visible)
	_key(KEY_F7, false)
	await _frames(2)
	get_tree().paused = false
	_trainer._on_hotkey("hud")
	_check("F7 does not change cheat states", _trainer._enabled_count() == 0)


func _test_cursor() -> void:
	_global.showmouse = false
	_global._gamepad = false
	_trainer._set_layout_locked(true)
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	_key(KEY_F10, true)
	await _frames(3)
	_key(KEY_F10, false)
	await _frames(2)
	_check("F10 unlocks layout and obtains visible cursor", !_trainer.hud_locked and _trainer._cursor_owned and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE)
	_check("cursor owner runs after game mouse policy", _trainer.process_priority > _global.process_priority)
	var start: Vector2 = _trainer.hud_position
	_press(Vector2(30.0, 20.0), true)
	_global.showmouse = false
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	_trainer._process(0.0)
	_check("game cursor recapture cannot interrupt panel drag", Input.mouse_mode == Input.MOUSE_MODE_VISIBLE and _trainer._drag_mode != _trainer.DRAG_NONE)
	_move(Vector2(20.0, 10.0))
	_check("panel keeps moving after cursor recovery", _trainer.hud_position == start + Vector2(20.0, 10.0))
	_global._gamepad = true
	await _frames(6)
	_check("controller mouse policy cannot leave editing cursor captured", Input.mouse_mode == Input.MOUSE_MODE_VISIBLE)
	_global._gamepad = false
	_trainer._on_hotkey("hud")
	_check("hiding HUD ends drag and restores game cursor policy", !_trainer._cursor_owned and _trainer._drag_mode == _trainer.DRAG_NONE and !_global.showmouse)
	_check("hidden HUD ignores mouse input", _trainer._panel.mouse_filter == Control.MOUSE_FILTER_IGNORE)
	_trainer._on_hotkey("hud")
	_check("showing HUD cannot reopen a stale editing session", !_trainer._cursor_owned and _trainer.hud_locked and !_global.showmouse)
	_trainer._set_layout_locked(true)
	_check("locking HUD returns cursor policy to gameplay", !_trainer._cursor_owned and !_global.showmouse)
	_global.showmouse = true
	get_tree().paused = true
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	_trainer._set_layout_locked(false)
	_trainer._set_layout_locked(true)
	_check("locking from a menu preserves its visible cursor", _global.showmouse and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE)
	get_tree().paused = false
	_global.showmouse = false
	_trainer.hud_position = _trainer.HUD_DEFAULT_POSITION
	_trainer._apply_layout()
	_trainer._save_config()


func _test_bind_repair() -> void:
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.erase_section_key("hotkeys", "schema")
	config.set_value("hotkeys", "breath", "F3")
	config.set_value("hotkeys", "onehit", "F6")
	config.set_value("custom", "keep", "unchanged")
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()
	_check("duplicate saved binds are repaired to consecutive defaults", _trainer.hotkeys == _trainer.HOTKEY_DEFAULTS)
	config.load("res://userdata/cicada_trainer.cfg")
	_check("repaired binds are persisted", config.get_value("hotkeys", "breath") == "F2" and config.get_value("hotkeys", "onehit") == "F5" and config.get_value("hotkeys", "schema") == 2)
	config.set_value("hotkeys", "god", "A")
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()
	_check("valid custom binds survive repair logic", _trainer.hotkeys["god"] == "A")
	_trainer._save_config()
	config.load("res://userdata/cicada_trainer.cfg")
	_check("game saves preserve unknown config sections", config.get_value("custom", "keep") == "unchanged")
	_check("atomic saves do not leave a temporary file", !FileAccess.file_exists("res://userdata/cicada_trainer.cfg.godot.tmp"))
	config.set_value("hotkeys", "god", "F1")
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("hotkeys", "god", "F12")
	config.erase_section_key("hotkeys", "killall")
	config.erase_section_key("hotkeys", "teleportexit")
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()
	_check("adding actions does not overwrite existing F12 binds", _trainer.hotkeys["god"] == "F12" and _trainer.hotkeys["killall"] == "Home" and _trainer.hotkeys["teleportexit"] == "End")
	config.load("res://userdata/cicada_trainer.cfg")
	for action in _trainer.HOTKEY_DEFAULTS:
		config.set_value("hotkeys", action, _trainer.HOTKEY_DEFAULTS[action])
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()


func _test_cursor_lifetime() -> void:
	_trainer._set_layout_locked(true)
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("hud", "locked", false)
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()
	_check("saved unlocked layout cannot acquire the cursor during gameplay", _trainer.hud_locked and !_trainer._editing and !_trainer._cursor_owned and !_global.showmouse)
	_trainer._set_layout_locked(false)
	_trainer._pull_config_changes()
	_check("polling preserves an explicitly opened editing session", _trainer._editing and _trainer._cursor_owned)
	_key(KEY_W, true)
	await _frames(3)
	_key(KEY_W, false)
	_check("movement input returns cursor control to gameplay", _trainer.hud_locked and !_trainer._cursor_owned and !_global.showmouse)
	_trainer._set_layout_locked(false)
	_key(KEY_ESCAPE, true)
	await _frames(2)
	_key(KEY_ESCAPE, false)
	_check("Escape ends editing", _trainer.hud_locked and !_trainer._editing and !_global.showmouse)
	_trainer._set_layout_locked(false)
	var outside := InputEventMouseButton.new()
	outside.button_index = MOUSE_BUTTON_LEFT
	outside.pressed = true
	outside.position = Vector2(1500.0, 800.0)
	_trainer._input(outside)
	_check("clicking outside HUD returns control to gameplay", _trainer.hud_locked and !_trainer._cursor_owned)
	_trainer._set_layout_locked(false)
	_trainer._notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	_check("losing focus cannot leave cursor ownership active", _trainer.hud_locked and !_trainer._editing and !_trainer._cursor_owned)
	get_tree().paused = true
	_global.showmouse = true
	_trainer._set_layout_locked(false)
	get_tree().paused = false
	_global.showmouse = false
	_trainer._process(0.0)
	_check("resuming a round ends editing and recaptures gameplay cursor", _trainer.hud_locked and !_trainer._cursor_owned and !_global.showmouse)
	_player.active = false
	_global.showmouse = true
	_trainer._set_layout_locked(false)
	_player.active = true
	_trainer._process(0.0)
	_check("deployment becoming active cannot inherit menu cursor", _trainer.hud_locked and !_trainer._cursor_owned and !_global.showmouse)
	_trainer._set_layout_locked(false)
	var previous_scene := get_tree().current_scene
	var next_scene := Node.new()
	get_tree().root.add_child(next_scene)
	get_tree().current_scene = next_scene
	_trainer._process(0.0)
	_check("round or scene changes discard editing session", _trainer.hud_locked and !_trainer._cursor_owned and !_global.showmouse)
	get_tree().current_scene = previous_scene
	next_scene.queue_free()
	await _frames(2)


func _test_new_actions() -> void:
	_reset_player()
	var second: Node = load("res://stubs/enemy_stub.gd").new()
	get_tree().root.add_child(second)
	_enemy._dead = false
	_enemy.energie = 4.0
	var dead_before: int = _enemy.death_calls
	var kills_before: int = _global.kills
	var inert := get_tree().root.get_node("InertEnemy")
	_key(KEY_F12, true)
	await _frames(3)
	_check("F12 kills every living enemy through its normal death pipeline", _enemy._dead and second._dead and _enemy.energie == 0.0 and second.energie == 0.0 and _enemy.death_calls == dead_before + 1)
	_check("kill all preserves kill bookkeeping and ignores unrelated nodes", _global.kills == kills_before + 2 and is_instance_valid(inert))
	await _frames(25)
	_check("holding F12 cannot repeatedly kill dead enemies", _enemy.death_calls == dead_before + 1 and second.death_calls == 1)
	_key(KEY_F12, false)
	await _frames(2)
	_trainer._on_hotkey("killall")
	_check("repeated kill action is safe for already-dead enemies", second.death_calls == 1)
	_enemy._dead = false
	_enemy.energie = 4.0
	_request_action("killall")
	_check("kill all button request reaches the game", _enemy._dead and _enemy.death_calls == dead_before + 2)
	_enemy._dead = false
	_enemy.energie = 4.0
	_trainer._pull_config_changes()
	_check("request counters cannot replay a kill action", !_enemy._dead and _enemy.energie == 4.0)
	var reloaded: Node = load("res://mod/cicada_trainer.gd").new()
	get_tree().root.add_child(reloaded)
	await _frames(3)
	_check("loading a trainer cannot replay a previous kill request", !_enemy._dead and _enemy.energie == 4.0)
	reloaded.queue_free()
	await _frames(2)
	_player.death = true
	_trainer._on_hotkey("killall")
	_check("kill all refuses a finished or dead round", !_enemy._dead)
	_player.death = false
	second.queue_free()
	var exit: Node3D = load("res://stubs/exit.gd").new()
	var far: Node3D = load("res://stubs/exit.gd").new()
	add_child(exit)
	add_child(far)
	exit.global_position = Vector3(10.0, 2.0, 5.0)
	far.global_position = Vector3(100.0, 4.0, 10.0)
	_player.global_position = Vector3.ZERO
	_player.velocity = Vector3(100.0, -30.0, 40.0)
	_player._prevelocity = Vector3(100.0, 0.0, 40.0)
	_player.vel_mod = Vector3(20.0, 10.0, 20.0)
	_player.spawncooldown = 5
	_key(KEY_END, true)
	await _frames(3)
	_key(KEY_END, false)
	await _frames(2)
	_check("End teleports to the nearest exit", _player.global_position.is_equal_approx(exit.global_position + _trainer.CORE_OFFSET))
	_check("exit teleport cancels momentum and entry cooldown", _player.velocity == Vector3.ZERO and _player._prevelocity == Vector3.ZERO and _player.vel_mod == Vector3.ZERO and _player.spawncooldown == 0)
	_check("exit teleport never bypasses core or completion rules", !exit._open and !exit._touched and _global.cores == 0 and !_player.exfil)
	_player.global_position = Vector3.ZERO
	_request_action("teleportexit")
	_check("exit button request reaches the game", _player.global_position.is_equal_approx(exit.global_position + _trainer.CORE_OFFSET))
	_player.global_position = Vector3.ZERO
	_trainer._pull_config_changes()
	_check("exit requests cannot replay on config refresh", _player.global_position == Vector3.ZERO)
	exit._touched = true
	_trainer._on_hotkey("teleportexit")
	_check("completed exit is skipped", _player.global_position.is_equal_approx(far.global_position + _trainer.CORE_OFFSET))
	far._touched = true
	_player.global_position = Vector3.ZERO
	_trainer._on_hotkey("teleportexit")
	_check("missing exit leaves player untouched", _player.global_position == Vector3.ZERO)
	exit._touched = false
	_player.active = false
	_trainer._on_hotkey("teleportexit")
	_check("exit teleport refuses inactive player", _player.global_position == Vector3.ZERO)
	_player.active = true
	exit.queue_free()
	far.queue_free()
	await _frames(3)


func _request_action(action: String) -> void:
	var config := ConfigFile.new()
	config.load("res://userdata/cicada_trainer.cfg")
	config.set_value("actions", action, int(config.get_value("actions", action, 0)) + 1)
	config.save("res://userdata/cicada_trainer.cfg")
	_trainer._pull_config_changes()


func _test_layout() -> void:
	_trainer.hud_position = _trainer.HUD_DEFAULT_POSITION
	_trainer.hud_size = _trainer.HUD_DEFAULT_SIZE
	_trainer.hud_font_size = _trainer.HUD_FONT_DEFAULT
	_trainer._set_layout_locked(false)
	_trainer._apply_layout()

	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	_trainer._update_panel_interactive()
	_check("panel is clickable when unlocked with a cursor", _trainer._panel.mouse_filter == Control.MOUSE_FILTER_STOP)

	_trainer._set_layout_locked(true)
	_trainer._update_panel_interactive()
	_check("locked panel ignores the mouse", _trainer._panel.mouse_filter == Control.MOUSE_FILTER_IGNORE)
	_press(_trainer.hud_position + Vector2(30.0, 20.0), true)
	_move(Vector2(60.0, 40.0))
	_press(_trainer.hud_position + Vector2(30.0, 20.0), false)
	_check("locked panel keeps its place", _trainer.hud_position == _trainer.HUD_DEFAULT_POSITION)

	_trainer._on_hotkey("layout")
	_check("layout key unlocks", _trainer.hud_locked == false and _trainer._panel.mouse_filter == Control.MOUSE_FILTER_STOP)

	var start: Vector2 = _trainer.hud_position
	_press(start + Vector2(30.0, 20.0), true)
	_move(Vector2(60.0, 40.0))
	_press(start + Vector2(30.0, 20.0), false)
	_check("panel drags", _trainer.hud_position == start + Vector2(60.0, 40.0))
	_check("panel follows drag", _trainer._panel.position == _trainer.hud_position)

	var size_before: Vector2 = _trainer.hud_size
	var font_before: int = _trainer.hud_font_size
	_press(size_before - Vector2(4.0, 4.0), true)
	_move(Vector2(0.0, 200.0))
	_press(size_before - Vector2(4.0, 4.0), false)
	_check("corner resizes panel", _trainer.hud_size.y > size_before.y)
	_check("resize keeps text size", _trainer.hud_font_size == font_before)
	_check("panel follows resize", _trainer._panel.size == _trainer.hud_size)
	_wheel(true)
	_check("wheel scales text", _trainer.hud_font_size == font_before + 1)
	_wheel(false)
	_check("wheel scales text back", _trainer.hud_font_size == font_before)

	_trainer._resize_hud(Vector2(99999.0, 99999.0))
	var room: Vector2 = _trainer._viewport_size()
	_check("size clamped to viewport", _trainer.hud_size.x <= room.x and _trainer.hud_size.y <= room.y)
	_trainer._resize_hud(Vector2(-50.0, -50.0))
	_check("size clamped to minimum", _trainer.hud_size == _trainer.HUD_MIN_SIZE)
	_trainer.hud_position = Vector2(-500.0, -500.0)
	_trainer._apply_layout()
	_check("position clamped into screen", _trainer.hud_position == Vector2.ZERO)
	_trainer.hud_size = Vector2(NAN, 0.0)
	_trainer._apply_layout()
	_check("broken size repaired", _trainer.hud_size.x >= _trainer.HUD_MIN_SIZE.x and _trainer.hud_size.y >= _trainer.HUD_MIN_SIZE.y)

	_trainer.hud_position = Vector2(120.0, 90.0)
	_trainer.hud_size = Vector2(300.0, 200.0)
	_trainer.hud_font_size = 16
	_trainer._apply_layout()
	_trainer._save_config()

	var reloaded: Node = load("res://mod/cicada_trainer.gd").new()
	reloaded.name = "CicadaTrainerLayout"
	get_tree().root.add_child(reloaded)
	await _frames(6)
	_check("layout persists", reloaded.hud_position == Vector2(120.0, 90.0) and reloaded.hud_size == Vector2(300.0, 200.0) and reloaded.hud_font_size == 16)
	_check("position persists but editing does not survive reloading", reloaded.hud_locked and !reloaded._cursor_owned and !reloaded._editing)
	reloaded.queue_free()
	await _frames(2)

	_trainer._set_layout_locked(false)
	_trainer._on_hotkey("layout")
	_check("layout key locks again", _trainer.hud_locked == true and _trainer._panel.mouse_filter == Control.MOUSE_FILTER_IGNORE)

	_trainer.hud_position = _trainer.HUD_DEFAULT_POSITION
	_trainer.hud_size = _trainer.HUD_DEFAULT_SIZE
	_trainer.hud_font_size = _trainer.HUD_FONT_DEFAULT
	_trainer._set_layout_locked(true)
	_trainer._apply_layout()
	_trainer._save_config()


func _test_pause() -> void:
	_reset_player()
	_set_cheat("god", true)
	get_tree().paused = true
	await _frames(2)
	_check("paused tree is left alone", _player.energie == 1.0 and _player.iframes == 0)
	get_tree().paused = false
	await _frames(2)
	_check("resume restores cheats", _player.energie == _player.max_energie)
	_set_cheat("god", false)


func _test_config_roundtrip() -> void:
	_set_cheat("jumps", true)
	_trainer._save_config()
	var config := ConfigFile.new()
	_check("config readable", config.load("res://userdata/cicada_trainer.cfg") == OK)
	_check("config stores cheat", bool(config.get_value("cheats", "jumps", false)) == true)
	_check("config stores hotkey", str(config.get_value("hotkeys", "god", "")) == "F1")
	_set_cheat("jumps", false)
	_trainer._save_config()
	var reloaded: Node = load("res://mod/cicada_trainer.gd").new()
	reloaded.name = "CicadaTrainerReload"
	get_tree().root.add_child(reloaded)
	await _frames(6)
	_check("reloaded node reads config", reloaded.hotkeys["teleport"] == "F11" and reloaded.state["jumps"] == false)
	_check("reloaded node builds sounds", reloaded._players.size() == 12)
	reloaded.queue_free()


func _playing_count() -> int:
	var count := 0
	for player in _trainer._players.values():
		if player.playing:
			count += 1
	for player in [_trainer._panic_player, _trainer._allon_player, _trainer._hud_player, _trainer._teleport_player]:
		if player.playing:
			count += 1
	return count


func _all_players_stop() -> void:
	for player in _trainer._players.values():
		player.stop()
	for player in [_trainer._panic_player, _trainer._allon_player, _trainer._hud_player, _trainer._teleport_player]:
		player.stop()


func _press(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	event.position = position
	_trainer._on_panel_gui_input(event)


func _wheel(up: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_WHEEL_UP if up else MOUSE_BUTTON_WHEEL_DOWN
	event.pressed = true
	_trainer._on_panel_gui_input(event)


func _move(relative: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.relative = relative
	event.position = _trainer.hud_position + relative
	_trainer._on_panel_gui_input(event)


func _set_cheat(name: String, enabled: bool) -> void:
	if _trainer.state[name] != enabled:
		_trainer._on_hotkey(name)


func _reset_player() -> void:
	_player.energie = 1.0
	_player.iframes = 0
	_player.can_multijump = false
	_player.jump_amnt = 0.0
	_player.jump_missed = false
	_player.dash_timer = 0
	_player.dash_timer_start = false
	_player.spawncooldown = 0
	_player._reload = 0
	_player.underwater = false
	_player.global_position = Vector3.ZERO


func _frames(count: int) -> void:
	for i in count:
		await get_tree().physics_frame


func _check(label: String, condition: bool) -> void:
	_checks += 1
	if condition:
		print("PASS  %s" % label)
		return
	_failures += 1
	print("FAIL  %s" % label)
