import numpy as np
import wave
import os
import sys

RATE = 22050
LEVEL = 0.42
STEPS = 16.0


def tone(start_hz, end_hz, milliseconds):
    count = int(RATE * milliseconds / 1000.0)
    i = np.arange(count, dtype=np.float64)
    progress = i / count
    freq = start_hz + (end_hz - start_hz) * (progress ** 2)
    phase = np.cumsum(2.0 * np.pi * freq / RATE)
    square = np.where(np.sin(phase) >= 0.0, 1.0, -1.0)
    saw = (phase % (2.0 * np.pi)) / np.pi - 1.0
    rng = np.random.default_rng(int(start_hz * 1000.0))
    click = rng.uniform(-1.0, 1.0, count) * 0.35 * np.exp(-progress * 26.0)
    attack = max(1, int(count * 0.02))
    envelope = np.ones(count)
    envelope[:attack] = np.arange(attack) / attack
    decay = count / 2.4
    sample = (square * 0.6 + saw * 0.4 + click) * envelope * np.exp(-i / decay) * LEVEL
    sample = np.clip(sample, -1.0, 1.0)
    sample = np.round(sample * STEPS) / STEPS
    return (sample * 32000.0).astype("<i2")


def write(path, samples):
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(samples.tobytes())
    print("%-28s %6d bytes  %5.0f ms" % (os.path.basename(path), os.path.getsize(path), 1000.0 * len(samples) / RATE))


out = sys.argv[1]
os.makedirs(out, exist_ok=True)
write(os.path.join(out, "toggle_on.wav"), tone(659.25, 659.25 * 1.5, 120))
write(os.path.join(out, "toggle_off.wav"), tone(659.25, 659.25 * 0.62, 95))
panic = np.concatenate([tone(hz, hz * 0.6, 58) for hz in (880.0, 659.25, 440.0)])
write(os.path.join(out, "panic.wav"), panic)
all_on = np.concatenate([tone(hz, hz * 1.4, 58) for hz in (440.0, 659.25, 880.0)])
write(os.path.join(out, "all_on.wav"), all_on)
write(os.path.join(out, "teleport.wav"), tone(300.0, 1200.0, 140))
