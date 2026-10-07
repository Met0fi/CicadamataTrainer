import sys
import wave
import numpy as np


def read(path):
    with wave.open(path, "rb") as f:
        rate = f.getframerate()
        data = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2").astype(np.float64) / 32768.0
    return rate, data


def stats(name, rate, data):
    peak = np.max(np.abs(data))
    rms = float(np.sqrt(np.mean(data ** 2)))
    print("%-14s %6d Hz  %6.1f ms  peak %6.2f dBFS  rms %6.2f dBFS  dc %+.4f"
          % (name, rate, 1000.0 * len(data) / rate,
             20 * np.log10(max(peak, 1e-9)), 20 * np.log10(max(rms, 1e-9)), float(np.mean(data))))
    return peak, rms


def correlate(a, b):
    n = min(len(a), len(b))
    a, b = a[:n] - a[:n].mean(), b[:n] - b[:n].mean()
    denom = np.sqrt(np.sum(a ** 2) * np.sum(b ** 2))
    return float(np.sum(a * b) / denom) if denom > 0 else 0.0


if __name__ == "__main__":
    dumped_dir, preview_dir = sys.argv[1], sys.argv[2]
    pairs = [("on", "dumped_on.wav", "toggle_on.wav"), ("off", "dumped_off.wav", "toggle_off.wav"), ("panic", "dumped_panic.wav", "panic.wav")]
    ok = True
    for label, dumped, preview in pairs:
        r1, d1 = read(f"{dumped_dir}/{dumped}")
        r2, d2 = read(f"{preview_dir}/{preview}")
        p1, _ = stats("in-game " + label, r1, d1)
        p2, _ = stats("exe " + label, r2, d2)
        corr = correlate(d1, d2)
        duration_delta = abs(len(d1) - len(d2)) / max(len(d1), len(d2))
        rms_delta = abs(20 * np.log10(np.sqrt(np.mean(d1 ** 2))) - 20 * np.log10(np.sqrt(np.mean(d2 ** 2))))
        verdict = "MATCH" if (corr > 0.9 and duration_delta < 0.02 and rms_delta < 1.0) else "DIFFERENT"
        print("%-8s correlation %.4f  duration delta %.3f%%  rms delta %.2f dB  peak delta %.3f -> %s\n"
              % (label, corr, duration_delta * 100, rms_delta, abs(p1 - p2), verdict))
        ok = ok and verdict == "MATCH"
    print("SOUNDS OK" if ok else "SOUNDS DIFFER")
    sys.exit(0 if ok else 1)
