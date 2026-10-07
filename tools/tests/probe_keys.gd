extends SceneTree


func _initialize() -> void:
	var candidates := [
		"Page Up", "PageUp", "Pageup", "PAGEUP", "Prior", "PgUp", "Pg Up", "Page up",
		"Page Down", "PageDown", "Pagedown", "PAGEDOWN", "Next", "PgDn", "Pg Down", "Page down",
		"Escape", "Esc", "Space", "Spacebar", "Enter", "Return", "Tab", "Backtab",
	]
	for name in candidates:
		print("%-12s -> %d" % [name, OS.find_keycode_from_string(name)])
	print("canonical pageup: ", OS.get_keycode_string(KEY_PAGEUP))
	print("canonical pagedown: ", OS.get_keycode_string(KEY_PAGEDOWN))
	print("canonical insert: ", OS.get_keycode_string(KEY_INSERT))
	print("canonical left: ", OS.get_keycode_string(KEY_LEFT))
	quit(0)
