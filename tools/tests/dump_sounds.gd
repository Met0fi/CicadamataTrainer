extends SceneTree

var _frames := 0
var _trainer: Node


func _initialize() -> void:
	_trainer = load("res://mod/cicada_trainer.gd").new()
	root.add_child.call_deferred(_trainer)


func _process(_delta: float) -> bool:
	_frames += 1
	if _frames < 10:
		return false
	_dump("jumps:on", "res://userdata/dumped_on.wav")
	_dump("jumps:off", "res://userdata/dumped_off.wav")
	_dump_panic("res://userdata/dumped_panic.wav")
	print("dumped sounds")
	return true


func _dump(key: String, path: String) -> void:
	var player: AudioStreamPlayer = _trainer._players.get(key)
	if player == null:
		print("missing player ", key)
		return
	_write(path, player.stream.data)


func _dump_panic(path: String) -> void:
	_write(path, _trainer._panic_player.stream.data)


func _write(path: String, pcm: PackedByteArray) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	var rate: int = _trainer.SOUND_RATE
	file.store_buffer("RIFF".to_ascii_buffer())
	file.store_32(36 + pcm.size())
	file.store_buffer("WAVEfmt ".to_ascii_buffer())
	file.store_32(16)
	file.store_16(1)
	file.store_16(1)
	file.store_32(rate)
	file.store_32(rate * 2)
	file.store_16(2)
	file.store_16(16)
	file.store_buffer("data".to_ascii_buffer())
	file.store_32(pcm.size())
	file.store_buffer(pcm)
	file.close()
	print("wrote %s (%d bytes)" % [path, pcm.size()])
