import { useEffect, useRef } from "react";

/**
 * Antigravity-style mouse-trail particle background.
 *
 * Ported from the Three.js/R3F demo: particles are randomly scattered
 * across the canvas. When the cursor approaches, particles within the
 * magnet radius get pulled into an orbiting ring around the cursor —
 * forming a radial starburst / ripple pattern. Capsule-shaped dashes
 * orient radially (pointing toward the cursor center).
 *
 * On a white background with Google brand colors for the login page.
 */

// ── Palette ─────────────────────────────────────────────────────────────
const COLORS = [
  "#E11D48", // Rose Red (Đồng bộ cực tốt với chữ "chấm bài giấy" màu đỏ của bạn)
  "#4F46E5", // Indigo (Màu chủ đạo của các platform SaaS hiện đại)
  "#0EA5E9", // Sky Blue
  "#8B5CF6", // Violet
  "#14B8A6", // Teal
  "#F59E0B", // Amber
  "#EC4899", // Pink
];

// ── Physics tuning (matching the 3D demo's feel) ───────────────────────
// ✏️ TUNING GUIDE — adjust these values to your liking:
const MIN_SPACING = 60;         // ← khoảng cách tối thiểu giữa các hạt (nhỏ = nhiều hạt hơn, lớn = ít hạt hơn)
const MAGNET_RADIUS = 450;      // ← bán kính vùng ảnh hưởng của chuột (px)
const RING_RADII = [90, 120, 160, 210, 260, 310]; // ← bán kính 3 vòng tròn đồng tâm [gần, giữa, xa] (px)
const WAVE_SPEED = 0.4;         // ← tốc độ sóng gợn
const WAVE_AMPLITUDE = 20;      // ← biên độ sóng gợn (lớn = sóng to hơn)
const LERP_SPEED = 0.08;        // ← tốc độ di chuyển hạt về vị trí đích (0.01–0.2)
const PULSE_SPEED = 2.5;          // ← tốc độ nhấp nháy kích thước hạt
const PARTICLE_VARIANCE = 2;    // ← mức độ biến thiên kích thước khi nhấp nháy

// ── Types ───────────────────────────────────────────────────────────────
interface Particle {
  // Home (rest) position — where it sits when cursor is away
  homeX: number;
  homeY: number;
  // Current (rendered) position
  cx: number;
  cy: number;
  // Internal time counter for wave/pulse
  t: number;
  speed: number;
  // Random offset from the ring radius for visual variety
  randomRadiusOffset: number;
  // Which concentric ring this particle belongs to (0, 1, 2)
  ringLayer: number;
  // Visual
  baseSize: number;   // base length of the dash/dot
  color: string;
  colorRGB: [number, number, number];
  isDash: boolean;    // 60% dashes, 40% dots
}

// ── Helpers ─────────────────────────────────────────────────────────────
function hexToRGB(hex: string): [number, number, number] {
  return [
    parseInt(hex.slice(1, 3), 16),
    parseInt(hex.slice(3, 5), 16),
    parseInt(hex.slice(5, 7), 16),
  ];
}

function rgba(rgb: [number, number, number], a: number): string {
  return `rgba(${rgb[0]},${rgb[1]},${rgb[2]},${a})`;
}

// ── Poisson disk sampling for even distribution ─────────────────────────
function poissonDiskSample(w: number, h: number, minDist: number, maxCount: number): Array<[number, number]> {
  const cellSize = minDist / Math.SQRT2;
  const gridW = Math.ceil(w / cellSize);
  const gridH = Math.ceil(h / cellSize);
  const grid: (number | null)[] = new Array(gridW * gridH).fill(null);
  const points: Array<[number, number]> = [];
  const active: number[] = [];

  const gridIndex = (x: number, y: number) =>
    Math.floor(x / cellSize) + Math.floor(y / cellSize) * gridW;

  const addPoint = (x: number, y: number) => {
    const i = points.length;
    points.push([x, y]);
    active.push(i);
    grid[gridIndex(x, y)] = i;
  };

  // Seed with first point
  addPoint(Math.random() * w, Math.random() * h);

  const k = 30; // candidates per attempt
  while (active.length > 0 && points.length < maxCount) {
    const idx = Math.floor(Math.random() * active.length);
    const [px, py] = points[active[idx]];
    let found = false;

    for (let attempt = 0; attempt < k; attempt++) {
      const angle = Math.random() * Math.PI * 2;
      const r = minDist + Math.random() * minDist;
      const nx = px + Math.cos(angle) * r;
      const ny = py + Math.sin(angle) * r;

      if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

      const gi = Math.floor(nx / cellSize);
      const gj = Math.floor(ny / cellSize);
      let tooClose = false;

      // Check 5×5 neighborhood in grid
      for (let di = -2; di <= 2 && !tooClose; di++) {
        for (let dj = -2; dj <= 2 && !tooClose; dj++) {
          const ci = gi + di;
          const cj = gj + dj;
          if (ci < 0 || ci >= gridW || cj < 0 || cj >= gridH) continue;
          const neighbor = grid[ci + cj * gridW];
          if (neighbor !== null) {
            const [npx, npy] = points[neighbor];
            const dx = nx - npx;
            const dy = ny - npy;
            if (dx * dx + dy * dy < minDist * minDist) {
              tooClose = true;
            }
          }
        }
      }

      if (!tooClose) {
        addPoint(nx, ny);
        found = true;
        break;
      }
    }

    if (!found) {
      active.splice(idx, 1);
    }
  }

  return points;
}

// ── Particle factory ────────────────────────────────────────────────────
function createParticles(w: number, h: number): Particle[] {
  // Extend the sampling area beyond viewport by MAGNET_RADIUS on every side
  // so edges and corners always have enough particles for the ring effect
  const margin = MAGNET_RADIUS;
  const extW = w + margin * 2;
  const extH = h + margin * 2;
  const targetCount = 99999; // no cap — let MIN_SPACING control density naturally
  const positions = poissonDiskSample(extW, extH, MIN_SPACING, targetCount);

  return positions.map(([px, py]) => {
    // Offset back so the extended area is centered on the viewport
    const x = px - margin;
    const y = py - margin;
    const color = COLORS[Math.floor(Math.random() * COLORS.length)];
    return {
      homeX: x,
      homeY: y,
      cx: x,
      cy: y,
      t: Math.random() * 100,
      speed: 0.01 + Math.random() / 200,
      randomRadiusOffset: (Math.random() - 0.5) * 2,
      ringLayer: Math.floor(Math.random() * RING_RADII.length),
      baseSize: 4 + Math.random() * 5,
      color,
      colorRGB: hexToRGB(color),
      isDash: Math.random() < 0.6,
    };
  });
}

// ── Component ───────────────────────────────────────────────────────────
export function LoginParticleBackground() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rafRef = useRef(0);
  const particlesRef = useRef<Particle[]>([]);
  const mouseRef = useRef({ x: -9999, y: -9999 });
  const virtualMouseRef = useRef({ x: -9999, y: -9999 });
  const sizeRef = useRef({ w: 0, h: 0, dpr: 1 });

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) return;

    // ── Resize ──────────────────────────────────────────────────────
    const resize = () => {
      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      const w = window.innerWidth;
      const h = window.innerHeight;
      canvas.width = Math.floor(w * dpr);
      canvas.height = Math.floor(h * dpr);
      canvas.style.width = `${w}px`;
      canvas.style.height = `${h}px`;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      sizeRef.current = { w, h, dpr };
      particlesRef.current = createParticles(w, h);
    };

    // ── Mouse ───────────────────────────────────────────────────────
    const onMouseMove = (e: MouseEvent) => {
      mouseRef.current = { x: e.clientX, y: e.clientY };
    };
    const onMouseLeave = () => {
      mouseRef.current = { x: -9999, y: -9999 };
    };
    const onTouchMove = (e: TouchEvent) => {
      const t = e.touches[0];
      if (t) mouseRef.current = { x: t.clientX, y: t.clientY };
    };
    const onTouchEnd = () => {
      mouseRef.current = { x: -9999, y: -9999 };
    };

    // ── Animation ───────────────────────────────────────────────────
    const tick = () => {
      rafRef.current = requestAnimationFrame(tick);
      if (document.visibilityState === "hidden") return;

      const { w, h } = sizeRef.current;
      const mouse = mouseRef.current;
      const vMouse = virtualMouseRef.current;
      const particles = particlesRef.current;

      // Smooth the virtual mouse toward the real mouse
      const smoothFactor = 0.08;
      if (mouse.x > -1000) {
        vMouse.x += (mouse.x - vMouse.x) * smoothFactor;
        vMouse.y += (mouse.y - vMouse.y) * smoothFactor;
      } else {
        vMouse.x = -9999;
        vMouse.y = -9999;
      }

      const targetX = vMouse.x;
      const targetY = vMouse.y;
      const mouseActive = targetX > -1000;

      // ── Clear ─────────────────────────────────────────────────────
      ctx.fillStyle = "#FFFFFF";
      ctx.fillRect(0, 0, w, h);

      // ── Update & draw particles ───────────────────────────────────
      for (let i = 0; i < particles.length; i++) {
        const p = particles[i];

        // Advance internal time
        p.t += p.speed / 2;

        // Compute distance from home to virtual cursor
        const dx = p.homeX - targetX;
        const dy = p.homeY - targetY;
        const dist = Math.sqrt(dx * dx + dy * dy);

        let destX = p.homeX;
        let destY = p.homeY;

        // Per-particle ring radius
        const myRingRadius = RING_RADII[p.ringLayer];

        // If within magnet radius, pull to ring orbit around cursor
        if (mouseActive && dist < MAGNET_RADIUS) {
          const angle = Math.atan2(dy, dx);
          const wave = Math.sin(p.t * WAVE_SPEED + angle) * WAVE_AMPLITUDE;
          const deviation = p.randomRadiusOffset * 10;
          const currentRingRadius = myRingRadius + wave + deviation;

          destX = targetX + currentRingRadius * Math.cos(angle);
          destY = targetY + currentRingRadius * Math.sin(angle);
        }

        // Lerp toward target (smooth, organic movement)
        p.cx += (destX - p.cx) * LERP_SPEED;
        p.cy += (destY - p.cy) * LERP_SPEED;

        // Compute current distance to cursor (for scaling)
        const curDistX = p.cx - targetX;
        const curDistY = p.cy - targetY;
        const currentDist = Math.sqrt(curDistX * curDistX + curDistY * curDistY);
        const distFromRing = Math.abs(currentDist - myRingRadius);

        // Scale factor: particles ON their ring are full-size, fade off
        let scaleFactor: number;
        if (mouseActive && dist < MAGNET_RADIUS) {
          scaleFactor = 1 - distFromRing / (myRingRadius * 0.5);
          scaleFactor = Math.max(0.05, Math.min(1, scaleFactor));
        } else {
          // Not in magnet range → very small, barely visible ambient dots
          scaleFactor = 0.15;
        }

        // Pulse animation
        const pulse = 0.8 + Math.sin(p.t * PULSE_SPEED) * 0.2 * PARTICLE_VARIANCE;
        const finalScale = scaleFactor * pulse;
        const drawSize = p.baseSize * finalScale;

        // Opacity: ring particles are vivid, ambient particles are very faint
        let alpha: number;
        if (mouseActive && dist < MAGNET_RADIUS) {
          alpha = scaleFactor * 0.95;
        } else {
          alpha = 0.08;
        }

        // Skip if too small or transparent
        if (drawSize < 0.2 || alpha < 0.01) continue;

        // Angle pointing toward cursor center (for radial orientation)
        const angleToCenter = Math.atan2(targetY - p.cy, targetX - p.cx);

        if (p.isDash) {
          // ── Capsule / rounded-rect dash ──────────────────────────
          const dashLen = drawSize * 3.5;
          const dashW = drawSize * 0.6;
          const r = dashW * 0.45;

          ctx.save();
          ctx.translate(p.cx, p.cy);
          // Orient radially: point toward cursor center
          ctx.rotate(angleToCenter + Math.PI / 2);
          ctx.fillStyle = rgba(p.colorRGB, alpha);
          ctx.beginPath();
          ctx.roundRect(-dashW / 2, -dashLen / 2, dashW, dashLen, r);
          ctx.fill();
          ctx.restore();
        } else {
          // ── Round dot ────────────────────────────────────────────
          ctx.beginPath();
          ctx.arc(p.cx, p.cy, drawSize * 0.45, 0, Math.PI * 2);
          ctx.fillStyle = rgba(p.colorRGB, alpha);
          ctx.fill();
        }
      }
    };

    // ── Setup ───────────────────────────────────────────────────────
    window.addEventListener("resize", resize);
    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseleave", onMouseLeave);
    window.addEventListener("touchmove", onTouchMove, { passive: true });
    window.addEventListener("touchend", onTouchEnd);

    resize();
    rafRef.current = requestAnimationFrame(tick);

    return () => {
      cancelAnimationFrame(rafRef.current);
      window.removeEventListener("resize", resize);
      window.removeEventListener("mousemove", onMouseMove);
      window.removeEventListener("mouseleave", onMouseLeave);
      window.removeEventListener("touchmove", onTouchMove);
      window.removeEventListener("touchend", onTouchEnd);
    };
  }, []);

  return (
    <canvas
      ref={canvasRef}
      style={{
        position: "absolute",
        top: 0,
        left: 0,
        width: "100%",
        height: "100%",
        zIndex: 1,
        pointerEvents: "none",
      }}
      aria-hidden
    />
  );
}
