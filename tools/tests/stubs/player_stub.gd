extends CharacterBody3D

var energie: float = 5.0
var max_energie: float = 5.0
var iframes: int = 0
var underwater: bool = false
var can_multijump: bool = true
var jump_amnt: float = 0.0
var jump_missed: bool = false
var dash_timer: int = 0
var dash_timer_start: bool = false
var spawncooldown: int = 5
var _reload: int = 0
var death: bool = false
var active: bool = true
var exfil := false
var _prevelocity := Vector3.ZERO
var vel_mod := Vector3.ZERO


func _ready() -> void:
	add_to_group("player")
