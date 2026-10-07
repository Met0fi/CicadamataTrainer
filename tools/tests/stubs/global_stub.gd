extends Node

var showmouse := false
var _gamepad := false
var cores := 0
var kills := 0

func _process(_delta: float) -> void:
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE if showmouse and !_gamepad else Input.MOUSE_MODE_CAPTURED)
