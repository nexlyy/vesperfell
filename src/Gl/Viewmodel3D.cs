using System;

namespace Vesperfell
{
    /// <summary>
    /// The hero's own hands and weapon, seen through their eyes. Built every frame in world space through the camera's
    /// axes (so the scene's lights fall on them), then drawn over a cleared depth buffer with a narrower lens so they
    /// never sink into a wall. Poses follow the same state as the third-person body: swings, casts, the drawn bow.
    /// </summary>
    public sealed unsafe partial class Renderer3D
    {
        readonly MeshB selfShadow = new MeshB(), vm = new MeshB(), vmFx = new MeshB();
        readonly GpuMesh selfGpu = new GpuMesh(), vmGpu = new GpuMesh(), vmFxGpu = new GpuMesh();
        readonly float[] vmProj = M4.New(), vmVP = M4.New();
        const float ForeL = 6.4f;            // forearm length in the hands' space (a hero is about 33 units tall)
        const float WS = 1.2f;               // weapons are built at body scale; heroes wear them 1.2x
        Xf eyeXf;                            // the hands' space: +X along the view, +Y up the screen, +Z to the right
        float vmLastYaw = float.NaN, vmLastPitch, vmSwayY, vmSwayZ, vmCast, vmAim, vmBlock;
        MeshB[] vmArms;
        static readonly V3 ShoulderR = new V3(-3f, -11.5f, 8.5f), ShoulderL = new V3(-3f, -11.5f, -8.5f);

        // building
        /// <summary>Poses this frame's hands and weapon. False when there is nothing to show (no hero, or slain).</summary>
        bool BuildViewmodel()
        {
            Player p = g.P;
            if (p == null || p.Dead) { vmLastYaw = float.NaN; return false; }
            float dt = fdt;
            V3 R = new V3(View[0], View[4], View[8]), U = new V3(View[1], View[5], View[9]), F = new V3(-View[2], -View[6], -View[10]);

            // the hands trail the view a little when it turns, bob with the stride and breathe when still
            if (float.IsNaN(vmLastYaw)) { vmLastYaw = FpYaw; vmLastPitch = FpPitch; }
            float dyaw = M.AngDiff(vmLastYaw, FpYaw), dpitch = FpPitch - vmLastPitch;
            vmLastYaw = FpYaw; vmLastPitch = FpPitch;
            float inv = 1f / Math.Max(dt, 1f / 240f);
            float tz = M.Clamp(-dyaw * inv * 0.75f, -2.4f, 2.4f), ty = M.Clamp(-dpitch * inv * 0.75f, -1.8f, 1.8f);
            float k = Math.Min(1f, dt * 9f);
            vmSwayZ += (tz - vmSwayZ) * k; vmSwayY += (ty - vmSwayY) * k;
            float st = g.FpStride, sk = g.Cfg.Shake ? g.FpStrideK : g.FpStrideK * 0.3f;
            float bx = -0.5f * sk, by = -Math.Abs(M.Sin(st)) * 0.45f * sk + M.Sin(g.UiT * 1.8f) * 0.1f, bz = M.Sin(st) * 0.5f * sk;
            Xf frame = new Xf();
            frame.Ax = F.X; frame.Ay = F.Y; frame.Az = F.Z;
            frame.Bx = U.X; frame.By = U.Y; frame.Bz = U.Z;
            frame.Cx = R.X; frame.Cy = R.Y; frame.Cz = R.Z;
            frame.Tx = Eye.X; frame.Ty = Eye.Y; frame.Tz = Eye.Z;
            eyeXf = frame * Xf.Move(bx, by + vmSwayY, bz + vmSwayZ);

            int tint, amt, em;
            HeroTint(p, out tint, out amt, out em);
            Paint(vm, tint, amt, em);
            Item it = p.Equip[Player.SlotWeapon];
            int type = it != null ? it.Base : p.Def.WeaponTypes[0], tier = it != null ? it.Tier : 0;
            switch (p.Class)
            {
                case ClassId.Warrior: VmWarrior(p, type, tier, dt); break;
                case ClassId.Mage: VmMage(p, type, tier, dt); break;
                case ClassId.Rogue: VmRogue(p, type, tier); break;
                default: VmRanger(p, type, tier, dt); break;
            }
            Paint(dyn, 0, 0, -1);
            return vm.N > 0;
        }

        static void HeroTint(Player p, out int tint, out int amt, out int em)
        {
            tint = 0; amt = 0; em = -1;
            if (p.FlashT > 0) { tint = 0xFFFFFF; amt = 170; em = 90; }
            else if (p.FreezeT > 0) { tint = 0x9BDCF4; amt = 140; }
            else if (p.OathT > 0) { tint = 0xFFD070; amt = 50; em = 40; }
            else if (p.PoisonT > 0) { tint = 0x6FA82E; amt = 60; }
            else if (p.NextCrit) { tint = 0xC08CF0; amt = 45; em = 25; }
            if (p.StealthT > 0) { tint = 0x2A2448; amt = 150; em = -1; }
        }

        /// <summary>One forearm reaching from below the view to the hand at h (the grip), coming from the shoulder side.</summary>
        void VmArm(ClassId c, V3 hand, V3 shoulder)
        {
            V3 d = (hand - shoulder).Norm;
            V3 elbow = hand - d * (ForeL + 0.95f);
            Put(ArmMesh(c), eyeXf * Xf.Move(elbow) * Rigs.Seg(hand - elbow, new V3(0, 1, 0)));
        }

        void VmWeapon(MeshB w, V3 hand, V3 dir, V3 up)
        {
            if (w == null) return;
            Put(w, eyeXf * Xf.Move(hand) * Rigs.Orient(dir, up) * Xf.Scale(WS));
        }

        static V3 L(V3 a, V3 b, float t) { return V3.Lerp(a, b, M.Clamp01(t)); }

        // warrior
        void VmWarrior(Player p, int type, int tier, float dt)
        {
            MeshB w = Rigs.Weapon(type, tier);
            bool axe = type == 1;
            WeaponType wt = Weapons.Types[type];
            vmBlock = M.Approach(vmBlock, p.Blocking ? 1f : 0f, dt * 9f);
            // at rest the blade rides low on the right, its tip short of the crosshair, turned a little edge-on
            V3 hand = new V3(12f, -9.6f, 9.2f), dir = new V3(0.75f, 0.58f, 0.28f), up = new V3(0.35f, 0, 1);
            if (axe) { hand = new V3(12.5f, -9.8f, 8.6f); dir = new V3(0.62f, 0.72f, 0.12f); up = new V3(0.2f, 0, 1); }
            float len = axe ? 23.4f : 21.8f, root = axe ? 0.62f : 0.32f;
            float swingAmt = 0;
            if (p.WhirlT > 0)
            {
                // the blade sweeps across the view again and again
                float turn = (1f - p.WhirlT / 0.5f) * 2f;
                float th = -1.7f + (turn - (float)Math.Floor(turn)) * 3.4f;
                Swing(th, 0.12f, 1, out hand, out dir, out up);
                VmTrail(Math.Max(-1.7f, th - 1.0f), th, 0.12f, 1, len, root, true, Math.Min(1f, p.WhirlT * 6f));
                swingAmt = 1;
            }
            else if (p.AtkT > 0 && !wt.Ranged)
            {
                float pr = 1f - p.AtkT / Math.Max(0.01f, p.AtkDur);
                float th = M.Lerp(p.SwingFrom, p.SwingTo, M.EaseOut(pr));
                float tilt = p.SwingStep == 0 ? 0.5f : (p.SwingStep == 1 ? -0.5f : 1.25f);
                int sd = p.SwingTo >= p.SwingFrom ? 1 : -1;
                Swing(th, tilt, sd, out hand, out dir, out up);
                float tail = p.SwingFrom;
                if (Math.Abs(th - tail) > 1.3f) tail = th - sd * 1.3f;         // only the last stretch of the arc glows
                VmTrail(tail, th, tilt, sd, len, root, p.SwingStep == 2 || axe, 1f - M.Clamp01((pr - 0.55f) / 0.45f));
                swingAmt = 1;
            }
            else if (p.ComboT < p.AtkDur + 0.2f && p.ComboT >= p.AtkDur && p.AtkDur > 0)
            {
                // settle back from where the last swing ended
                float tilt = p.SwingStep == 0 ? 0.5f : (p.SwingStep == 1 ? -0.5f : 1.25f);
                V3 h2, d2, u2;
                Swing(p.SwingTo, tilt, p.SwingTo >= p.SwingFrom ? 1 : -1, out h2, out d2, out u2);
                float t = (p.ComboT - p.AtkDur) / 0.2f;
                t = t * t * (3 - 2 * t);
                hand = L(h2, hand, t); dir = L(d2, dir, t); up = L(u2, up, t);
            }
            else if (p.DashT > 0)
            {
                hand = new V3(14f, -6.2f, 3.2f); dir = new V3(1f, 0.1f, -0.12f); up = new V3(0, 1, 0);
            }
            if (vmBlock > 0.01f && swingAmt == 0)
            {
                // shield up on the left, blade drawn back low on the right
                hand = L(hand, new V3(11f, -8.4f, 7.4f), vmBlock); dir = L(dir, new V3(0.75f, 0.4f, 0.25f), vmBlock);
                V3 sh = L(new V3(9f, -15f, -9f), new V3(12.5f, -6.4f, -4.4f), vmBlock);
                VmArm(ClassId.Warrior, sh, ShoulderL);
                VmWeapon(Rigs.Shield(), sh, new V3(1f, 0.04f, 0.28f), new V3(0, 1, 0));
            }
            VmArm(ClassId.Warrior, hand, ShoulderR);
            VmWeapon(w, hand, dir, up);
            if (axe && vmBlock < 0.5f)
            {
                // both hands on the haft
                V3 lh = hand - dir.Norm * 4.2f;
                VmArm(ClassId.Warrior, lh, ShoulderL);
            }
        }

        /// <summary>
        /// The light a swing leaves across the view: the band the blade swept from the tail heading to the head, brightest
        /// along the edge, fading toward the tail. Drawn with the hands so it sits exactly on the blade.
        /// </summary>
        void VmTrail(float thTail, float thHead, float tilt, int dir, float len, float root, bool heavy, float fade)
        {
            float span = Math.Abs(thHead - thTail);
            if (span < 0.03f || fade <= 0.01f) return;
            int n = M.Clamp((int)(span * 16), 4, 32);
            int glow = heavy ? 0xFFC890 : 0xC8DCFF;
            if (vmTip == null) vmTip = new V3[33];
            // the sheet the blade swept (faint: from the eye it is often seen nearly edge-on)
            V3 pb0 = default(V3), pt0 = default(V3); byte e0 = 0;
            for (int k = 0; k <= n; k++)
            {
                float u = k / (float)n, th = thTail + (thHead - thTail) * u;
                V3 h, b, e;
                Swing(th, tilt, dir, out h, out b, out e);
                vmTip[k] = h + b * (len * 1.02f);
                V3 pb = eyeXf.P(h + b * (len * root)), pt = eyeXf.P(vmTip[k]);
                byte ea = A(u * u * fade * 0.16f);
                if (k > 0)
                {
                    vmFx.Raw(pb0, 0, 1, glow, e0, 2); vmFx.Raw(pt0, 0, 0, glow, e0, 2); vmFx.Raw(pt, 1, 0, glow, ea, 2);
                    vmFx.Raw(pb0, 0, 1, glow, e0, 2); vmFx.Raw(pt, 1, 0, glow, ea, 2); vmFx.Raw(pb, 1, 1, glow, ea, 2);
                }
                pb0 = pb; pt0 = pt; e0 = ea;
            }
            // the path of the edge, as a streak that always faces the eye
            for (int layer = 0; layer < 2; layer++)
            {
                float wid = layer == 0 ? 2.4f : 0.7f, al = layer == 0 ? 0.26f : 0.5f;      // a whisper of light, not a beam
                int col = layer == 0 ? glow : 0xFFFFFF;
                V3 l0 = default(V3), r0 = default(V3); byte a0 = 0;
                for (int k = 0; k <= n; k++)
                {
                    float u = k / (float)n;
                    V3 t = k < n ? vmTip[k + 1] - vmTip[k] : vmTip[k] - vmTip[k - 1];
                    V3 side = V3.Cross(t, vmTip[k]);
                    if (side.Len < 1e-4f) side = new V3(0, 1, 0); else side = side.Norm;
                    float w = wid * (0.2f + 0.8f * u);
                    V3 l = eyeXf.P(vmTip[k] + side * w), r = eyeXf.P(vmTip[k] - side * w);
                    byte a = A(u * u * fade * al);
                    if (k > 0)
                    {
                        vmFx.Raw(l0, 0, -1, col, a0, 2); vmFx.Raw(r0, 0, 1, col, a0, 2); vmFx.Raw(r, 1, 1, col, a, 2);
                        vmFx.Raw(l0, 0, -1, col, a0, 2); vmFx.Raw(r, 1, 1, col, a, 2); vmFx.Raw(l, 1, -1, col, a, 2);
                    }
                    l0 = l; r0 = r; a0 = a;
                }
            }
        }
        V3[] vmTip;

        /// <summary>
        /// A sword stroke: th is the blade's heading across the view (negative to the left), tilt turns the plane of the
        /// stroke from level toward an overhead chop, dir is +1 for a left-to-right stroke (the edge leads).
        /// </summary>
        static void Swing(float th, float tilt, int dir, out V3 hand, out V3 blade, out V3 edge)
        {
            Xf plane = Xf.Move(4.5f, -7f, 2.6f) * Xf.RotX(tilt);
            float c = M.Cos(th), s = M.Sin(th);
            hand = plane.P(c * 7.5f, 0.6f, s * 7.5f);
            blade = plane.D(new V3(c, 0.16f, s));
            edge = plane.D(new V3(-s * dir, 0, c * dir));
        }

        // mage
        void VmMage(Player p, int type, int tier, float dt)
        {
            MeshB w = Rigs.Weapon(type, tier);
            bool staff = type == 2;
            vmCast = M.Approach(vmCast, p.CastT > 0 || p.AtkT > 0 ? 1f : 0f, dt * (p.CastT > 0 ? 16f : 5f));
            float c = vmCast * vmCast * (3 - 2 * vmCast);
            V3 hand, dir;
            if (staff)
            {
                hand = L(new V3(13f, -7.8f, 7.2f), new V3(16f, -6.4f, 5.8f), c);
                dir = L(new V3(0.72f, 0.66f, -0.16f), new V3(0.86f, 0.48f, -0.18f), c);
            }
            else
            {
                hand = L(new V3(13f, -7.2f, 6.6f), new V3(15.5f, -5.6f, 4.4f), c);
                dir = L(new V3(0.8f, 0.5f, -0.25f), new V3(0.94f, 0.26f, -0.24f), c);
            }
            VmArm(ClassId.Mage, hand, ShoulderR);
            VmWeapon(w, hand, dir, new V3(0, 0, 1));
            // the free hand comes up to shape the spell
            if (c > 0.02f)
            {
                V3 lh = L(new V3(9f, -15f, -8f), new V3(13.5f, -5.4f, -4.2f), c);
                VmArm(ClassId.Mage, lh, ShoulderL);
                V3 palm = eyeXf.P(lh.X + 1.6f, lh.Y + 1.2f, lh.Z + 0.4f);
                int col = tier == 3 ? 0xB8A0FF : 0xFF9A40;
                Bill(fxAdd, palm, 3.2f * c, col, 0.55f * c, 0);
                Bill(fxAdd, palm, 1.4f * c, 0xFFF0C0, 0.6f * c, 0);
            }
        }

        // rogue
        void VmRogue(Player p, int type, int tier)
        {
            MeshB w = Rigs.Weapon(type, tier);
            bool dual = type == 4;
            // at rest the blades ride low in both corners, tips turned out and away from the crosshair
            V3 rh = new V3(12f, -9.6f, 9.4f), rd = new V3(0.6f, 0.72f, 0.28f);
            V3 lh = new V3(12f, -9.8f, -9.4f), ld = new V3(0.6f, 0.72f, -0.28f);
            if (p.AtkT > 0)
            {
                float pr = 1f - p.AtkT / Math.Max(0.01f, p.AtkDur);
                float th = M.Sin(pr * M.PI);
                bool left = dual && p.SwingStep == 1;
                if (!left) { rh = L(rh, new V3(19f, -4.6f, 2f), th); rd = L(rd, new V3(1f, 0.08f, -0.1f), th); }
                else { lh = L(lh, new V3(19f, -4.6f, -2f), th); ld = L(ld, new V3(1f, 0.08f, 0.1f), th); }
            }
            else if (p.DashT > 0)
            {
                rh = new V3(10f, -8.6f, 7.6f); rd = new V3(0.4f, -0.2f, 0.9f);
                lh = new V3(10f, -8.6f, -7.6f); ld = new V3(0.4f, -0.2f, -0.9f);
            }
            VmArm(ClassId.Rogue, rh, ShoulderR);
            VmWeapon(w, rh, rd, new V3(0, 0, 1));
            if (dual)
            {
                VmArm(ClassId.Rogue, lh, ShoulderL);
                VmWeapon(w, lh, ld, new V3(0, 0, -1));
            }
        }

        // ranger
        void VmRanger(Player p, int type, int tier, float dt)
        {
            float half = type == 6 ? 13.5f : 9.8f;
            bool aiming = p.AtkT > 0 || p.BowT > 0 || p.CastT > 0 || p.CombatT < 1.6f;
            vmAim = M.Approach(vmAim, aiming ? 1f : 0f, dt * (aiming ? 7f : 2.5f));
            float a = vmAim * vmAim * (3 - 2 * vmAim);
            float draw = 0;
            if (a > 0.6f) draw = p.BowT > 0.05f ? 0f : 0.85f;      // just loosed: the string snaps forward, then draws again
            // the bow arm: held low and canted at rest, raised in front when aiming
            V3 bh = L(new V3(15f, -8f, -8.6f), new V3(17f, -4.8f, -5.4f), a);
            V3 bf = L(new V3(1f, 0.1f, 0.22f), new V3(1f, 0.03f, 0.12f), a);
            V3 bu = L(new V3(0.08f, 0.9f, -0.42f), new V3(0f, 0.98f, 0.18f), a);
            MeshB bow = Rigs.Bow(half, tier, draw, false);
            VmArm(ClassId.Ranger, bh, ShoulderL);
            VmWeapon(bow, bh, bf, bu);
            // the string hand rides the nock
            float d4 = (float)Math.Round(draw * 4) / 4f;
            float tipX = 0.4f - 4.2f * (half / 13.5f) + 0.9f, nock = tipX - 0.4f - d4 * 7.5f;
            Xf bowXf = Rigs.Orient(bf, bu) * Xf.Scale(WS);
            V3 rh = bh + bowXf.D(new V3(nock - 0.7f, -0.2f, 0.9f));
            if (a < 0.6f) rh = L(new V3(11f, -9.6f, 8f), rh, a / 0.6f);
            VmArm(ClassId.Ranger, rh, ShoulderR);
        }

        // arms
        /// <summary>Forearm, cuff and hand for each class, built hanging along -Y from the elbow, rounded rather than faceted.</summary>
        MeshB ArmMesh(ClassId c)
        {
            if (vmArms == null) vmArms = new MeshB[4];
            int i = (int)c & 3;
            if (vmArms[i] != null) return vmArms[i];
            HumanSpec s = Rigs.Hero(c);
            MeshB m = new MeshB();
            m.Smooth = true;
            int handCol = s.Hand; byte handLayer = 0;
            switch (c)
            {
                case ClassId.Warrior:
                    m.Layer = Mat3.Metal; m.Col = s.Sleeve;
                    m.Cyl(0, 0, -ForeL, 0.6f, 1.2f, 1.4f, 14, false, false, 0);
                    m.Col = 0xB8C2D0;                                           // vambrace
                    m.Cyl(0, 0, -ForeL + 0.15f, -ForeL + 3.6f, 1.62f, 1.46f, 14, false, false, 0);
                    m.Col = 0x8A94A8;
                    m.Cyl(0, 0, -ForeL + 3.6f, -ForeL + 3.95f, 1.5f, 1.3f, 14, false, false, 0);
                    handLayer = Mat3.Metal;
                    break;
                case ClassId.Mage:
                    m.Layer = Mat3.Cloth; m.Col = s.Sleeve;                     // a wide sleeve, trimmed in ember
                    m.Cyl(0, 0, -ForeL + 0.8f, 0.6f, 2.2f, 1.35f, 14, false, false, 0);
                    m.Col = 0xDA5A2A;
                    m.Cyl(0, 0, -ForeL + 0.45f, -ForeL + 0.95f, 2.28f, 2.24f, 14, false, false, 0);
                    m.Col = 0x2A2140;
                    m.Cyl(0, 0, -ForeL + 0.2f, -ForeL + 1.2f, 0.95f, 1.1f, 10, false, false, 0);
                    break;
                case ClassId.Rogue:
                    m.Layer = Mat3.Cloth; m.Col = s.Sleeve;
                    m.Cyl(0, 0, -ForeL, 0.6f, 1.05f, 1.3f, 14, false, false, 0);
                    m.Col = 0x5A4A6E;                                           // wrapped bands
                    for (int k = 0; k < 3; k++) m.Cyl(0, 0, -ForeL + 0.5f + k * 1.1f, -ForeL + 1.1f + k * 1.1f, 1.12f, 1.1f, 14, false, false, 0);
                    handLayer = Mat3.Cloth;
                    break;
                default:
                    m.Layer = Mat3.Cloth; m.Col = s.Sleeve;
                    m.Cyl(0, 0, -ForeL + 3.2f, 0.6f, 1.1f, 1.32f, 14, false, false, 0);
                    m.Col = 0x5A3E2C;                                           // leather bracer
                    m.Cyl(0, 0, -ForeL + 0.1f, -ForeL + 3.4f, 1.2f, 1.28f, 14, false, false, 0);
                    m.Col = 0xC8A050;
                    m.Cyl(0, 0, -ForeL + 1.6f, -ForeL + 1.9f, 1.3f, 1.3f, 14, false, false, 0);
                    break;
            }
            // the hand closed around a grip: palm, knuckles, thumb
            m.Layer = handLayer; m.Col = handCol;
            m.Blob(0.1f, -ForeL - 0.9f, 0, 1.0f, 1.25f, 0.92f, 1, 9, 0);
            m.Blob(0.7f, -ForeL - 1.25f, 0, 0.72f, 0.95f, 0.88f, 1, 4, 0);
            m.Blob(0.55f, -ForeL - 0.35f, -0.75f, 0.48f, 0.62f, 0.45f, 1, 6, 0);
            m.T = Xf.Id; m.Em = 0; m.Layer = 0;
            vmArms[i] = m;
            return m;
        }

        // drawing
        void DrawViewmodel(MapGeo geo, bool indoor, bool day, bool play)
        {
            GL.Clear(GL.DEPTH_BUFFER_BIT);
            GL.Enable(GL.DEPTH_TEST); GL.DepthFunc(GL.LEQUAL); GL.DepthMask(1); GL.Disable(GL.BLEND);
            float aspect = VW / (float)VH;
            // same lens as the world (effects placed beside the hands line up), but a near plane at the fingertips
            M4.Perspective(vmProj, FpFov + FpFovKick, aspect, 0.25f, 400f);
            M4.Mul(vmProj, View, vmVP);
            BindWorldTextures(geo, false);
            pWorld.Use();
            WorldUniforms(pWorld, geo, false, indoor, day, play);
            pWorld.Mat("uVP", vmVP);
            pWorld.F("uShadowOn", 0);
            pWorld.F("uFogD", 0);
            pWorld.F2("uFogDist", 1e5f, 2e5f);
            pWorld.F4("uCut", 0, 0, 0, 0);
            pWorld.I("uMode", 0);
            vmGpu.Draw();
            if (vmFx.N > 0)
            {
                // swing trails, added over the hands
                pFx.Use();
                pFx.Mat("uVP", vmVP); pFx.F3("uCam", Eye); pFx.F2("uFogDist", 1e5f, 2e5f);
                GL.ActiveTexture(GL.TEXTURE0 + 4); GL.BindTexture(GL.TEXTURE_2D, atlasTex);
                GL.Enable(GL.BLEND); GL.DepthMask(0); GL.BlendFunc(GL.ONE, GL.ONE);
                pFx.F("uAdd", 1); pFx.F("uBoost", 1f);
                vmFxGpu.Draw();
                GL.DepthMask(1); GL.Disable(GL.BLEND);
            }
            GL.BindVertexArray(0);
        }
    }
}
