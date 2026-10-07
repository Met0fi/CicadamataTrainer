extends SceneTree

func _initialize() -> void:
	for code in [KEY_META, KEY_PAGEUP, KEY_PAGEDOWN, KEY_KP_ENTER, KEY_CTRL, KEY_ALT, KEY_SHIFT]:
		print("canonical: ", OS.get_keycode_string(code))
	quit(0)
