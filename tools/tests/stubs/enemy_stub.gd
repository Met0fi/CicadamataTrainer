extends CharacterBody3D

@export var energie: float = 4.0
var _dead: bool = false
var enemy_type: int = 0
var death_calls := 0


func _ready() -> void:
	add_to_group("enemy")


func _damage(pts, kill, _dmg: float = 0.5, floweroverride: bool = false) -> void:
	if _dead:
		return
	energie -= _dmg
	if energie <= 0.0:
		_dead = true


func _deathstuff() -> void:
	death_calls += 1
	_dead = true
	get_tree().root.get_node("global").kills += 1
