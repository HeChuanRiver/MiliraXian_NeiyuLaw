"""Build linear alpha distance data from the authored arcane stroke masks.

Run after PortableVfxAutomation exports textures. Requires Pillow, NumPy, SciPy;
these are authoring dependencies only. No Unity project or game state is changed.
The original PNGs remain the canonical geometry; derived fields are runtime data.
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt


def build_fields():
    root = Path(__file__).resolve().parents[2]
    source = root / "Content/Textures/Common/WorldArcaneStrike/arcane_circle"
    destination = source / "StrokeFields"
    destination.mkdir(exist_ok=True)
    count = 0
    for path in sorted(source.glob("*.png")):
        if path.stem in {"Halo", "Pulse", "Spark", "RuneParticle"}:
            continue
        with Image.open(path) as image:
            alpha = np.asarray(image.convert("RGBA"), dtype=np.uint8)[:, :, 3]
        inside = alpha >= 128
        # The half texel offset locates the original antialiased boundary.
        distance = np.where(inside, distance_transform_edt(inside) - 0.5,
                            0.5 - distance_transform_edt(~inside))
        partial = (alpha > 0) & (alpha < 255)
        distance[partial] = alpha[partial] / 255.0 - 0.5
        data = np.full((*alpha.shape, 4), 255, dtype=np.uint8)
        data[:, :, 3] = np.rint(np.clip(0.5 + distance / 32.0, 0, 1) * 255).astype(np.uint8)
        Image.fromarray(data, "RGBA").save(destination / path.name, optimize=True)
        count += 1
    print(f"Built {count} arcane distance fields in {destination}")


if __name__ == "__main__":
    build_fields()
