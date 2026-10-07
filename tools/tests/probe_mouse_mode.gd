extends SceneTree

func _initialize() -> void:
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	print("Captured requested: ", Input.mouse_mode)
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	print("Visible requested: ", Input.mouse_mode)
	quit(0)
