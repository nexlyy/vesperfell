using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Particles, floating combat text and ambient motes.</summary>
    public sealed partial class Game
    {
        public const byte PPixel = 0, PGlow = 1, PSmoke = 2, PSquare = 3, PStreak = 4;

        public void AddPart(Particle p)
        {
            if (PartCount < Parts.Length) Parts[PartCount++] = p;
            else Parts[Rnd.Int(Parts.Length)] = p;
        }

        /// <summary>Bright additive sparks flying outwards.</summary>
        public void Sparks(float x, float y, float z, int color, int count, float speed, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rnd.F() * M.TAU, s = speed * (0.35f + Rnd.F() * 0.65f);
                Particle p = new Particle();
                p.X = x; p.Y = y; p.Z = z; p.VX = M.Cos(a) * s; p.VY = M.Sin(a) * s * 0.6f; p.VZ = 20 + Rnd.F() * 70;
                p.Life = p.Max = life * (0.5f + Rnd.F() * 0.5f); p.Color = color; p.Color2 = color; p.Kind = PGlow; p.Size = 3; p.Grav = true; p.Drag = 2.5f;
                AddPart(p);
            }
        }

        /// <summary>Opaque droplets that are lit by the scene (blood, ichor, splinters).</summary>
        public void Splat(float x, float y, float z, int color, int count, float dx, float dy, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = M.Atan2(dy, dx) + Rnd.Sym(0.9f), s = speed * (0.3f + Rnd.F() * 0.7f);
                Particle p = new Particle();
                p.X = x + Rnd.Sym(2); p.Y = y + Rnd.Sym(1); p.Z = z + Rnd.F() * 4; p.VX = M.Cos(a) * s; p.VY = M.Sin(a) * s * 0.6f; p.VZ = 30 + Rnd.F() * 60;
                p.Life = p.Max = 0.35f + Rnd.F() * 0.35f; p.Color = color; p.Color2 = Col.Mul(color, 0.6f) & 0xFFFFFF; p.Kind = Rnd.Chance(0.4f) ? PSquare : PPixel; p.Grav = true; p.Drag = 2f; p.Lit = true;
                AddPart(p);
            }
        }

        public void Dust(float x, float y, int count, int color)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = new Particle();
                p.X = x + Rnd.Sym(4); p.Y = y + Rnd.Sym(2); p.Z = Rnd.F() * 2; p.VX = Rnd.Sym(18); p.VY = Rnd.Sym(8); p.VZ = 6 + Rnd.F() * 12;
                p.Life = p.Max = 0.3f + Rnd.F() * 0.3f; p.Color = color; p.Color2 = color; p.Kind = PSmoke; p.Size = 1.5f + Rnd.F() * 1.5f; p.Drag = 3f; p.Lit = true;
                AddPart(p);
            }
        }

        public void Smoke(float x, float y, float z, int count, int color, float size)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = new Particle();
                p.X = x + Rnd.Sym(5); p.Y = y + Rnd.Sym(3); p.Z = z + Rnd.F() * 6; p.VX = Rnd.Sym(14); p.VY = Rnd.Sym(6); p.VZ = 10 + Rnd.F() * 18;
                p.Life = p.Max = 0.6f + Rnd.F() * 0.6f; p.Color = color; p.Color2 = color; p.Kind = PSmoke; p.Size = size * (0.7f + Rnd.F() * 0.6f); p.Drag = 1.5f; p.Lit = true;
                AddPart(p);
            }
        }

        /// <summary>Glowing motes that drift upward (magic, embers, level-up).</summary>
        public void Motes(float x, float y, float z, int color, int color2, int count, float spread, float rise, float life)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = new Particle();
                float a = Rnd.F() * M.TAU, d = Rnd.F() * spread;
                p.X = x + M.Cos(a) * d; p.Y = y + M.Sin(a) * d * 0.6f; p.Z = z + Rnd.F() * 6; p.VX = Rnd.Sym(8); p.VY = Rnd.Sym(4); p.VZ = rise * (0.5f + Rnd.F());
                p.Life = p.Max = life * (0.5f + Rnd.F() * 0.5f); p.Color = color; p.Color2 = color2; p.Kind = PGlow; p.Size = 3; p.Drag = 0.6f;
                AddPart(p);
            }
        }

        /// <summary>A ring of sparks expanding along the ground.</summary>
        public void RingBurst(float x, float y, int color, int count, float speed, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * M.TAU / count + Rnd.Sym(0.1f);
                Particle p = new Particle();
                p.X = x; p.Y = y; p.Z = 2; p.VX = M.Cos(a) * speed; p.VY = M.Sin(a) * speed * 0.55f; p.VZ = Rnd.F() * 10;
                p.Life = p.Max = life * (0.7f + Rnd.F() * 0.3f); p.Color = color; p.Color2 = color; p.Kind = PGlow; p.Size = 3; p.Drag = 2.2f;
                AddPart(p);
            }
        }

        public void Trail(float x, float y, float z, int color, float life)
        {
            Particle p = new Particle();
            p.X = x + Rnd.Sym(1); p.Y = y + Rnd.Sym(1); p.Z = z; p.VX = Rnd.Sym(6); p.VY = Rnd.Sym(4); p.VZ = Rnd.F() * 8;
            p.Life = p.Max = life; p.Color = color; p.Color2 = color; p.Kind = PGlow; p.Size = 2.5f; p.Drag = 2f;
            AddPart(p);
        }

        public void Float(float x, float y, string text, int color, bool big)
        {
            if (Floats.Count > 60) Floats.RemoveAt(0);
            Floater f = new Floater();
            f.X = x + Rnd.Sym(5); f.Y = y; f.Text = text; f.Color = color; f.Big = big; f.VX = Rnd.Sym(10);
            if (big) { f.Dur = 1.1f; f.VY = -34; }
            Floats.Add(f);
        }

        void UpdateParticles(float dt)
        {
            for (int i = 0; i < PartCount; i++)
            {
                Particle p = Parts[i];
                p.Life -= dt;
                if (p.Life <= 0) { Parts[i] = Parts[--PartCount]; i--; continue; }
                p.X += p.VX * dt; p.Y += p.VY * dt; p.Z += p.VZ * dt;
                if (p.Grav) p.VZ -= 320 * dt;
                if (p.Z < 0) { p.Z = 0; p.VZ = -p.VZ * 0.35f; p.VX *= 0.5f; p.VY *= 0.5f; }
                if (p.Drag > 0) { float k = Math.Max(0, 1f - p.Drag * dt); p.VX *= k; p.VY *= k; }
                if (p.Kind == PSmoke) p.Size += dt * 3f;
                Parts[i] = p;
            }
        }

        void UpdateFloaters(float dt)
        {
            for (int i = Floats.Count - 1; i >= 0; i--)
            {
                Floater f = Floats[i];
                f.T += dt; f.X += f.VX * dt; f.Y += f.VY * dt; f.VY += 22 * dt; f.VX *= 1f - 2f * dt;
                if (f.T >= f.Dur) Floats.RemoveAt(i);
            }
        }

        float ambAcc;
        /// <summary>Zone-specific drifting motes: fireflies, ash, dust, embers.</summary>
        void UpdateAmbientFx(float dt)
        {
            ambAcc += dt;
            while (ambAcc > 0.06f)
            {
                ambAcc -= 0.06f;
                if (PartCount > Parts.Length - 300) return;
                float x = CamXi - 20 + Rnd.F() * (W + 40), y = CamYi - 10 + Rnd.F() * (H + 40);
                Particle p = new Particle();
                p.X = x; p.Y = y;
                if (Dawn && !Zone.Indoor)
                {
                    if (!Rnd.Chance(0.5f)) continue;
                    p.Z = 10 + Rnd.F() * 40; p.VX = 5 + Rnd.Sym(4); p.VY = Rnd.Sym(3); p.VZ = Rnd.Sym(4);
                    p.Life = p.Max = 3 + Rnd.F() * 3; p.Color = 0xFFE090; p.Color2 = 0xFFC060; p.Kind = PGlow; p.Size = 2.4f;
                    AddPart(p); continue;
                }
                switch (ZoneId)
                {
                    case Zones.Thornwood:
                    case Zones.Mirefen:
                        if (Rnd.Chance(0.55f))
                        {   // fireflies
                            p.Z = 6 + Rnd.F() * 22; p.VX = Rnd.Sym(10); p.VY = Rnd.Sym(6); p.VZ = Rnd.Sym(5);
                            p.Life = p.Max = 2.5f + Rnd.F() * 3; p.Color = ZoneId == Zones.Mirefen ? 0xA8E04A : 0xDCFA9A; p.Color2 = 0x6FA82E; p.Kind = PGlow; p.Size = 2.6f; p.Drag = 0.2f;
                            AddPart(p);
                        }
                        else if (Rnd.Chance(0.3f) && ZoneId == Zones.Thornwood)
                        {   // falling leaves
                            p.Z = 50 + Rnd.F() * 30; p.VX = -8 + Rnd.Sym(6); p.VY = Rnd.Sym(3); p.VZ = -16 - Rnd.F() * 10;
                            p.Life = p.Max = 3.5f; p.Color = Rnd.Chance(0.3f) ? 0x9E3123 : 0x30644A; p.Color2 = p.Color; p.Kind = PSquare; p.Lit = true;
                            AddPart(p);
                        }
                        break;
                    case Zones.Barrowfield:
                    case Zones.AbbeyRise:
                    case Zones.AshenFields:
                        if (Rnd.Chance(0.6f))
                        {   // drifting ash
                            p.Z = 30 + Rnd.F() * 60; p.VX = -14 + Rnd.Sym(6); p.VY = Rnd.Sym(3); p.VZ = -9 - Rnd.F() * 8;
                            p.Life = p.Max = 4 + Rnd.F() * 3; p.Color = 0xA8A8B8; p.Color2 = 0x8080A0; p.Kind = PPixel; p.Lit = false;
                            AddPart(p);
                        }
                        break;
                    case Zones.Abbey:
                    case Zones.BellCrypt:
                        if (Rnd.Chance(0.35f))
                        {   // dust motes hanging in the torchlight
                            p.Z = 8 + Rnd.F() * 30; p.VX = Rnd.Sym(3); p.VY = Rnd.Sym(2); p.VZ = Rnd.Sym(2);
                            p.Life = p.Max = 3 + Rnd.F() * 3; p.Color = ZoneId == Zones.BellCrypt ? 0x9080D0 : 0xC0A888; p.Color2 = p.Color; p.Kind = PPixel; p.Lit = true;
                            AddPart(p);
                        }
                        break;
                    default:
                        if (Rnd.Chance(0.18f))
                        {
                            p.Z = 6 + Rnd.F() * 20; p.VX = Rnd.Sym(8); p.VY = Rnd.Sym(5); p.VZ = Rnd.Sym(4);
                            p.Life = p.Max = 2.5f + Rnd.F() * 2; p.Color = 0xDCFA9A; p.Color2 = 0x6FA82E; p.Kind = PGlow; p.Size = 2.4f;
                            AddPart(p);
                        }
                        break;
                }
            }
            // embers and smoke from nearby fires
            emberAcc += dt;
            if (emberAcc > 0.09f)
            {
                emberAcc = 0;
                List<Prop> near = scratchProps; near.Clear();
                Cur.Query(CamXi - 20, CamYi - 20, CamXi + W + 20, CamYi + H + 60, near);
                for (int i = 0; i < near.Count; i++)
                {
                    Prop q = near[i];
                    if (!q.Flicker || q.LightR < 60 || q.Hidden || q.OnWall) continue;
                    if (!Rnd.Chance(0.5f)) continue;
                    bool gloam = q.LightColor == WorldGen.GloamLight;
                    Particle e = new Particle();
                    e.X = q.X + Rnd.Sym(3); e.Y = q.Y - 1; e.Z = -q.LightOY + 4; e.VX = Rnd.Sym(7); e.VY = Rnd.Sym(2); e.VZ = 16 + Rnd.F() * 22;
                    e.Life = e.Max = 0.7f + Rnd.F() * 0.8f; e.Color = gloam ? 0xC9D3F5 : 0xFDE999; e.Color2 = gloam ? 0x584688 : 0xCF5327; e.Kind = PGlow; e.Size = 2.2f; e.Drag = 0.4f;
                    AddPart(e);
                    if (q.Kind == PropKind.Campfire && Rnd.Chance(0.5f)) Smoke(q.X, q.Y, 16, 1, 0x3A3A48, 2.5f);
                }
            }
        }
        float emberAcc;
    }
}
