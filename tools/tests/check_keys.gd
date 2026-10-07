extends SceneTree


func _initialize() -> void:
	var names: PackedStringArray = []
	for argument in OS.get_cmdline_user_args():
		names.append_array(argument.split(",", false))
	var bad: PackedStringArray = []
	for name in names:
		if OS.find_keycode_from_string(name) == 0:
			bad.append(name)
	print("checked %d key names, unknown: %s" % [names.size(), "none" if bad.is_empty() else ", ".join(bad)])
	print("KEYS OK" if bad.is_empty() else "KEYS FAILED")
	quit(0 if bad.is_empty() else 1)
