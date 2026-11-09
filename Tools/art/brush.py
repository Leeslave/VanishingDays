"""Small procedural drawing kit for Vanishing Days art: shaded shapes, grain, hatching and rough strokes.

Everything is drawn on a float RGB canvas at a supersampled size and reduced at the end, so edges stay soft.
Shapes are lists of (x, y) points in output pixels; the canvas scales them by its supersampling factor.
"""
import numpy as np
import cv2
from scipy.ndimage import gaussian_filter


def curve(points, closed=False, steps=14):
    """Catmull-Rom spline through the points: hand-placed control points become smooth outlines."""
    p = np.asarray(points, float)
    if len(p) < 3:
        return p
    if closed:
        p = np.vstack([p[-1], p, p[0], p[1]])
    else:
        p = np.vstack([p[0], p, p[-1]])
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for t in np.linspace(0, 1, steps, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(p[-2])
    return np.array(out)


def ellipse(cx, cy, rx, ry, start=0, end=360, rotation=0, steps=64):
    a = np.radians(np.linspace(start, end, steps))
    r = np.radians(rotation)
    x, y = rx * np.cos(a), ry * np.sin(a)
    return np.stack([cx + x * np.cos(r) - y * np.sin(r), cy + x * np.sin(r) + y * np.cos(r)], 1)


def quad(a, b, width_a, width_b=None):
    """A bar from point a to point b, width_a wide at a and width_b wide at b."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    width_b = width_a if width_b is None else width_b
    d = b - a
    n = np.array([-d[1], d[0]]) / (np.hypot(*d) + 1e-9)
    return np.array([a + n * width_a / 2, b + n * width_b / 2, b - n * width_b / 2, a - n * width_a / 2])


class Canvas:
    def __init__(self, width, height, background, seed=1, ss=2):
        self.w, self.h, self.ss = width, height, ss
        self.W, self.H = width * ss, height * ss
        self.rng = np.random.default_rng(seed)
        self.img = np.ones((self.H, self.W, 3), np.float32) * np.asarray(background, np.float32) / 255
        # Two scales of paper tooth: fine grain and soft blotches.
        fine = gaussian_filter(self.rng.normal(0, 1, (self.H, self.W)), 0.7 * ss)
        soft = gaussian_filter(self.rng.normal(0, 1, (self.H, self.W)), 9 * ss)
        self.fine = fine / (fine.std() + 1e-6)
        self.soft = soft / (soft.std() + 1e-6)

    # ---- masks -------------------------------------------------------------------------------
    def _pts(self, pts):
        return np.round(np.asarray(pts, float) * self.ss * 16).astype(np.int32)

    def poly(self, pts):
        m = np.zeros((self.H, self.W), np.uint8)
        cv2.fillPoly(m, [self._pts(pts)], 255, cv2.LINE_AA, shift=4)
        return m.astype(np.float32) / 255

    def line(self, pts, width, closed=False):
        m = np.zeros((self.H, self.W), np.uint8)
        cv2.polylines(m, [self._pts(pts)], closed, 255, max(1, int(round(width * self.ss))), cv2.LINE_AA, shift=4)
        return m.astype(np.float32) / 255

    def blob(self, pts):
        return self.poly(curve(pts, closed=True))

    # ---- paint -------------------------------------------------------------------------------
    def put(self, alpha, color):
        a = np.clip(alpha, 0, 1)[..., None]
        c = np.asarray(color, np.float32)
        if c.ndim == 1:
            c = c / 255
        self.img = self.img * (1 - a) + c * a

    def multiply(self, alpha, factor):
        a = np.clip(alpha, 0, 1)[..., None]
        self.img = self.img * (1 - a + a * factor)

    def lighting(self, mask, soft=10, light=(-0.55, -0.65, 0.52), relief=9.0):
        """Lambert light on the mask treated as a soft height field: round shapes from flat outlines."""
        h = gaussian_filter(mask, soft * self.ss)
        gy, gx = np.gradient(h)
        n = np.stack([-gx * relief * soft * self.ss, -gy * relief * soft * self.ss, np.ones_like(h)], -1)
        n /= np.linalg.norm(n, axis=-1, keepdims=True)
        l = np.asarray(light, np.float32)
        l /= np.linalg.norm(l)
        return np.clip((n * l).sum(-1), 0, 1)

    def shape(self, mask, color, soft=10, ambient=0.42, diffuse=0.75, rim=0.25, grain=0.10, blotch=0.06,
              gloss=0.0, outline=0.85, outline_color=(6, 10, 11), light=(-0.55, -0.65, 0.52), relief=9.0):
        """A solid object part: lit body colour, darkened edges, paper grain, optional gloss and a dark contour."""
        lit = self.lighting(mask, soft, light, relief)
        inner = gaussian_filter(mask, 2.5 * self.ss)
        edge_dark = 1 - rim * np.clip(1 - inner * 1.6, 0, 1)
        tone = (ambient + diffuse * lit) * edge_dark * (1 + grain * self.fine + blotch * self.soft)
        col = np.asarray(color, np.float32)[None, None, :] / 255 * tone[..., None]
        if gloss > 0:
            spec = np.clip(lit - 0.82, 0, 1) / 0.18
            col = col + gloss * (spec ** 2)[..., None]
        if outline > 0:
            k = max(3, int(2.2 * self.ss) | 1)
            ring = np.clip(cv2.dilate(mask, np.ones((k, k), np.uint8)) - mask, 0, 1)
            self.put(ring * outline, outline_color)
        self.put(mask, np.clip(col, 0, 1))

    def hatch(self, mask, angle=45, spacing=7, width=1.2, color=(8, 12, 13), alpha=0.5, jitter=0.6, shadow=None):
        """Uneven parallel strokes inside a mask; with `shadow`, strokes only where the shape turns from the light."""
        m = np.zeros((self.H, self.W), np.uint8)
        r = np.radians(angle)
        d = np.array([np.cos(r), np.sin(r)])
        n = np.array([-d[1], d[0]])
        span = np.hypot(self.w, self.h)
        centre = np.array([self.w / 2, self.h / 2])
        for off in np.arange(-span / 2, span / 2, spacing):
            o = off + self.rng.normal(0, spacing * 0.18)
            a = centre + n * o - d * span / 2 + self.rng.normal(0, jitter, 2)
            b = centre + n * o + d * span / 2 + self.rng.normal(0, jitter, 2)
            w = width * self.rng.uniform(0.6, 1.4)
            cv2.line(m, tuple(self._pts([a])[0]), tuple(self._pts([b])[0]), int(self.rng.uniform(150, 255)),
                     max(1, int(round(w * self.ss))), cv2.LINE_AA, shift=4)
        strokes = m.astype(np.float32) / 255 * mask
        # Broken charcoal: the tooth of the paper eats part of every stroke.
        strokes *= np.clip(0.75 + 0.6 * self.fine, 0, 1)
        if shadow is not None:
            strokes *= shadow
        self.put(strokes * alpha, color)

    def stroke(self, pts, width, color, alpha=0.9, passes=3, wobble=0.8, broken=0.5, smooth=True):
        """A rough hand-drawn line: several jittered passes, each partly eaten by the paper grain."""
        base = curve(pts) if smooth and len(pts) > 2 else np.asarray(pts, float)
        acc = np.zeros((self.H, self.W), np.float32)
        for _ in range(passes):
            p = base + self.rng.normal(0, wobble, base.shape)
            acc = np.maximum(acc, self.line(p, width * self.rng.uniform(0.6, 1.15)) * self.rng.uniform(0.55, 1))
        acc *= np.clip(1 - broken * 0.5 + broken * 0.55 * self.fine, 0, 1)
        self.put(acc * alpha, color)

    def shadow(self, mask, offset=(6, 10), blur=10, alpha=0.6):
        m = np.roll(mask, (int(offset[1] * self.ss), int(offset[0] * self.ss)), (0, 1))
        self.multiply(gaussian_filter(m, blur * self.ss) * alpha, 0.25)

    def vignette(self, strength=0.35):
        y, x = np.mgrid[0:self.H, 0:self.W]
        d = np.hypot((x - self.W / 2) / (self.W / 2), (y - self.H / 2) / (self.H / 2))
        self.img *= (1 - strength * np.clip(d - 0.35, 0, 1))[..., None]

    def finish(self, grain=0.035):
        self.img = np.clip(self.img * (1 + grain * self.fine[..., None]), 0, 1)
        out = (self.img * 255).astype(np.uint8)
        return cv2.resize(out, (self.w, self.h), interpolation=cv2.INTER_AREA)
