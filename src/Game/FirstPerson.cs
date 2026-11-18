using System;

namespace Vesperfell
{
    /// <summary>
    /// Seeing the world through the hero's eyes: mouse look, what the crosshair rests on, and the camera's own motion
    /// (eye height over uneven ground, the stride, the dodge, the fall when slain). The simulation underneath is the
    /// same as in the overhead view: the hero still aims along a heading on the map.
    /// </summary>
    public sealed partial class Game
    {
        /// <summary>Where the hero looks: heading on the map (0 = east, +pi/2 = south) and pitch (up is positive).</summary>
        public float LookYaw = -M.PI / 2, LookPitch = -0.06f;
        /// <summary>What the crosshair rests on: a foe, and the point on the ground (map coordinates) either way.</summary>
        public Enemy AimFoe; public float AimGX, AimGY;
        /// <summary>Eye height above the ground. Heroes stand about 33 units tall.</summary>
        public const float EyeH = 27.5f;
        const float AimReach = 640f;
        float fpGround = float.NaN, fpLastX, fpLastY;
        /// <summary>The stride (radians, one half-turn per footfall) and how much of it shows; the hands bob with it.</summary>
        public float FpStride, FpStrideK;

        /// <summary>True when the 3D view is seen from the hero's own eyes (the default), false for the overhead camera.</summary>
        public bool FirstPerson { get { return R3 != null && Cfg.Camera != "top"; } }

        /// <summary>The window should hold the mouse for looking around.</summary>
        public bool WantMouseLook()
        {
            return FirstPerson && Scr == Screen.Play && P != null && Ov == Overlay.None;
        }

        /// <summary>Turns the view with the mouse; the arrow keys turn it too, for playing on the keyboard alone.</summary>
        void FpLook(float dt, bool control)
        {
            if (Ov != Overlay.None) return;
            float k = 0.0015f * Cfg.MouseSens;
            LookYaw += In.MDX * k;
            LookPitch -= In.MDY * k * (Cfg.InvertY ? -1f : 1f);
            if (control && !chatOpen)
            {
                if (In.Down[Key.Left]) LookYaw -= 2.3f * dt;
                if (In.Down[Key.Right]) LookYaw += 2.3f * dt;
            }
            if (LookYaw > M.PI) LookYaw -= M.TAU; else if (LookYaw < -M.PI) LookYaw += M.TAU;
            LookPitch = M.Clamp(LookPitch, -1.3f, 1.2f);
        }

        /// <summary>
        /// Follows the crosshair ray from the eye: the nearest foe it passes through, otherwise the spot where it meets
        /// the ground (or the farthest reach when it meets nothing). Ground-targeted abilities use that spot.
        /// </summary>
        void PickFp()
        {
            Player p = P;
            AimFoe = null;
            if (p == null || R3 == null) return;
            V3 eye = new V3(p.X, R3.HeightAt(p.X, p.Y) + EyeH, p.Y);
            float cp = M.Cos(LookPitch);
            V3 d = new V3(M.Cos(LookYaw) * cp, M.Sin(LookPitch), M.Sin(LookYaw) * cp);
            float best = AimReach;
            float hd2 = d.X * d.X + d.Z * d.Z;
            if (hd2 > 1e-4f)
                for (int i = 0; i < Foes.Count; i++)
                {
                    Enemy e = Foes[i];
                    if (e.Dead || e.FadeIn < 0.5f) continue;
                    float ex = e.X - eye.X, ez = e.Y - eye.Z;
                    float t = (ex * d.X + ez * d.Z) / hd2;
                    if (t < 0 || t > best) continue;
                    float qx = eye.X + d.X * t - e.X, qz = eye.Z + d.Z * t - e.Y;
                    float r = e.Radius + 4f;
                    if (qx * qx + qz * qz > r * r) continue;
                    float y = eye.Y + d.Y * t, fg = R3.HeightAt(e.X, e.Y);
                    if (y < fg - 3 || y > fg + FoeTop(e) + 3) continue;
                    best = t; AimFoe = e;
                }
            // march along the ray until it dips under the ground; a hill in front hides a foe behind it
            float gx = eye.X + d.X * best, gz = eye.Z + d.Z * best, prev = 0;
            for (float t = 4; t <= best; t += 4)
            {
                float x = eye.X + d.X * t, z = eye.Z + d.Z * t;
                if (eye.Y + d.Y * t > R3.HeightAt(x, z)) { prev = t; continue; }
                float lo = prev, hi = t;
                for (int k = 0; k < 6; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (eye.Y + d.Y * mid <= R3.HeightAt(eye.X + d.X * mid, eye.Z + d.Z * mid)) hi = mid; else lo = mid;
                }
                gx = eye.X + d.X * hi; gz = eye.Z + d.Z * hi;
                AimFoe = null;
                break;
            }
            if (AimFoe != null) { AimGX = AimFoe.X; AimGY = AimFoe.Y; }
            else { AimGX = M.Clamp(gx, 0, Cur.PW); AimGY = M.Clamp(gz, 0, Cur.PH); }
        }

        /// <summary>Hands the renderer this tick's eye: position, heading, pitch, roll and field-of-view kick.</summary>
        void FeedFpCamera(float dt)
        {
            Player p = P;
            Renderer3D r = R3;
            float ground = r.HeightAt(p.X, p.Y);
            float jump = M.Dist(p.X, p.Y, fpLastX, fpLastY);
            if (float.IsNaN(fpGround) || jump > 90) fpGround = ground;
            else fpGround += (ground - fpGround) * M.Damp(18f, dt);
            fpLastX = p.X; fpLastY = p.Y;

            float eye = EyeH, yaw = LookYaw, pitch = LookPitch, roll = 0, fov = 0;
            float side = 0, bob = 0;
            bool motion = Cfg.Shake;
            // the stride: one dip per footfall, a slow sway from foot to foot
            bool walking = p.Moving && p.DodgeT <= 0 && p.DashT <= 0 && !p.Dead;
            FpStrideK = M.Approach(FpStrideK, walking ? 1f : 0f, dt * 5f);
            if (walking && jump < 90) FpStride += jump / 21f * M.PI;
            if (motion && FpStrideK > 0.001f)
            {
                bob = (Math.Abs(M.Sin(FpStride)) - 0.6f) * 1.1f * FpStrideK;
                side = M.Sin(FpStride) * 0.5f * FpStrideK;
                roll += M.Sin(FpStride) * 0.006f * FpStrideK;
            }
            // the dodge: a quick duck that leans the way the hero rolls
            if (p.DodgeT > 0)
            {
                float pr = M.Clamp01(1f - p.DodgeT / 0.26f), k = M.Sin(pr * M.PI);
                eye -= 9f * k;
                pitch -= 0.1f * k;
                float rx = -M.Sin(LookYaw), rz = M.Cos(LookYaw);
                float lat = p.DodgeDX * rx + p.DodgeDY * rz;
                roll += (lat > 1 ? 1 : (lat < -1 ? -1 : 0)) * 0.1f * k;
            }
            if (p.DashT > 0 && p.DashDur > 0) fov += 0.12f * M.Clamp01(p.DashT / p.DashDur);
            if (p.WhirlT > 0) roll += M.Sin(p.WhirlT * 25f) * 0.02f;
            // slain: the view sinks to the ground and tips over
            if (p.Dead)
            {
                float k = M.Clamp01(DeathT / 0.9f);
                k = k * k * (3 - 2 * k);
                eye = M.Lerp(EyeH, 5f, k);
                roll = 0.62f * k;
                pitch = M.Lerp(pitch, 0.12f, k);
            }
            if (ShakeT > 0 && Cfg.Shake)
            {
                float a = ShakeAmp * M.Clamp01(ShakeT / 0.25f) * 0.0035f;
                yaw += Rnd.Sym(a); pitch += Rnd.Sym(a);
            }
            r.FirstPerson = true;
            r.FpEye = new V3(p.X - M.Sin(LookYaw) * side, fpGround + eye + bob, p.Y + M.Cos(LookYaw) * side);
            r.FpYaw = yaw; r.FpPitch = M.Clamp(pitch, -1.45f, 1.45f); r.FpRoll = roll; r.FpFovKick = fov;
        }

        /// <summary>Puts the hero at a named landmark looking a given way (degrees). Used by the automated tour.</summary>
        public bool TourTo(string mapId, string mark, float dx, float dy, float yawDeg, float pitchDeg)
        {
            Map m = MapById(mapId);
            Marker k;
            if (m == null || P == null || !m.Markers.TryGetValue(mark, out k)) return false;
            if (Cur != m) EnterMap(m, k.X + dx, k.Y + dy); else DebugTeleport(k.X + dx, k.Y + dy);
            LookYaw = yawDeg * M.PI / 180f; LookPitch = pitchDeg * M.PI / 180f; P.AimAngle = LookYaw;
            Ov = Overlay.None;
            return true;
        }

        /// <summary>Points the view somewhere sensible after arriving on a map: at the current objective, else down the longest open way.</summary>
        void FaceOnArrival()
        {
            if (P == null || Cur == null) return;
            LookPitch = -0.06f;
            fpGround = float.NaN;
            Quest q = Q.Get(Q.Tracked);
            if (q == null || !q.Active) q = Q.Get("bell");
            float tx, ty;
            if (q != null && Q.Target(this, q, out tx, out ty) && M.Dist(tx, ty, P.X, P.Y) > 40 && M.Dist(tx, ty, P.X, P.Y) < 700)
            {
                LookYaw = M.Atan2(ty - P.Y, tx - P.X);
                P.AimAngle = LookYaw;
                return;
            }
            float bestA = LookYaw, bestD = -1;
            for (int i = 0; i < 24; i++)
            {
                float a = i * M.TAU / 24f;
                float d = 0;
                while (d < 400 && !Cur.Blocked(P.X + M.Cos(a) * (d + 8), P.Y + M.Sin(a) * (d + 8), 3, 1)) d += 8;
                // prefer north a little: the vale's landmarks rise that way
                float score = d + (M.Sin(a) < -0.5f ? 30 : 0);
                if (score > bestD) { bestD = score; bestA = a; }
            }
            LookYaw = bestA;
            if (LookYaw > M.PI) LookYaw -= M.TAU;
            P.AimAngle = LookYaw;
        }
    }
}
