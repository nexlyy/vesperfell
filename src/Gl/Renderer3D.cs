using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Vesperfell
{
    /// <summary>
    /// The 3D view of the game world. The simulation is unchanged (it still lives on the 2D map plane); this class
    /// turns it into terrain, models, lights and effects seen through a perspective camera, and lays the 640x360
    /// interface over the result.
    /// </summary>
    public sealed unsafe partial class Renderer3D
    {
        readonly Game g;
        internal GlContext Ctx;
        public string Info = "";

        // target
        public int VW = 1280, VH = 720;          // framebuffer the frame is drawn into
        public int UiX, UiY, UiS = 2;            // where the 640x360 interface sits inside it
        bool offscreen;

        // resources
        Prog pWorld, pShadow, pWater, pFx, pSky, pBright, pBlur, pFinal, pUi, pBlit;
        GpuMesh quad, dynGpu, fxAddGpu, fxAlphaGpu, decalGpu, cardGpu;
        uint matTex, matNTex, gndTex, gndNTex, noiseTex, atlasTex, uiTex;
        int uiTexW = Game.W, uiTexH = Game.H; bool uiTexLinear;
        uint uiPbo;
        UiGpu uiGpu;
        /// <summary>Where the interface is written down for the card (null when this card cannot draw it that way).</summary>
        public UiRec UiRecorder { get { return uiGpu != null ? uiGpu.Rec : null; } }
        /// <summary>How the interface reaches the card: 0 changed bands, 1 whole layer, 2 changed bands through a pixel buffer.</summary>
        public static int UiUploadMode = ReadUploadMode();
        static int ReadUploadMode() { int m; return int.TryParse(Environment.GetEnvironmentVariable("VF_UIUP"), out m) ? m : 0; }
        Fbo msaa, scene, half1, half2, quart1, quart2, shadow, shot;
        int samples = 4, fbW, fbH;
        bool atlasReady;
        public bool Shadows = true, Bloom = true;
        const int ShadowSize = 2048;

        // camera
        public readonly float[] View = M4.New(), Proj = M4.New(), VP = M4.New(), InvVP = M4.New();
        readonly float[] shadowVP = M4.New(), shadowBiasVP = M4.New(), tmp = M4.New(), tmp2 = M4.New();
        readonly float[] frustum = new float[24];
        public V3 Eye, Look;
        public float Pitch = 52f * M.PI / 180f, Dist = 470f, FovY = 31f * M.PI / 180f;
        public float Zoom = 1f, ZoomTarget = 1f;
        public bool TitleMode;
        public V3 SunDir = new V3(0.42f, -1f, 0.34f).Norm;

        // first person: the game hands over the eye each tick
        public bool FirstPerson;
        public V3 FpEye, ViewDir = new V3(0, 0, -1);
        public float FpYaw = -M.PI / 2, FpPitch, FpRoll, FpFovKick;
        public float FpFov = 64f * M.PI / 180f;
        /// <summary>This frame is seen through the hero's eyes (gameplay in first person; menus keep their vistas).</summary>
        bool Fp { get { return FirstPerson && !TitleMode; } }
        bool frameIndoor, frameDay;

        // per frame
        readonly MeshB dyn = new MeshB(), fxAdd = new MeshB(), fxAlpha = new MeshB(), decal = new MeshB(), cards = new MeshB();
        const int MaxLights = 40;
        readonly float[] lp = new float[MaxLights * 4], lc = new float[MaxLights * 4];
        int nl;
        readonly List<LightSrc> lights = new List<LightSrc>(256);
        struct LightSrc { public float X, Y, Z, R, I, D; public int C; }
        readonly float[] push = new float[16];
        V3 cutHero; bool cutOn;
        float ambR, ambG, ambB, fogD; int fogCol;
        V3 haze;

        internal Renderer3D(Game game, GlContext ctx)
        {
            g = game; Ctx = ctx;
            Info = ctx.Renderer + " / OpenGL " + ctx.Version;
            samples = Math.Min(4, ctx.MaxSamples);
            if (samples < 2) samples = 0;
            pWorld = new Prog("world", Shaders.WorldVs, Shaders.WorldFs);
            pShadow = new Prog("shadow", Shaders.ShadowVs, Shaders.ShadowFs);
            pWater = new Prog("water", Shaders.WaterVs, Shaders.WaterFs);
            pFx = new Prog("fx", Shaders.FxVs, Shaders.FxFs);
            pSky = new Prog("sky", Shaders.FullVs, Shaders.SkyFs);
            pBright = new Prog("bright", Shaders.FullVs, Shaders.BrightFs);
            pBlur = new Prog("blur", Shaders.FullVs, Shaders.BlurFs);
            pFinal = new Prog("final", Shaders.FullVs, Shaders.FinalFs);
            pUi = new Prog("ui", Shaders.UiVs, Shaders.UiFs);
            pBlit = new Prog("blit", Shaders.FullVs, Shaders.BlitFs);

            MeshB q = new MeshB();
            q.Quad(new V3(-1, -1, 0), new V3(1, -1, 0), new V3(1, 1, 0), new V3(-1, 1, 0));
            quad = new GpuMesh(); quad.Upload(q, false);
            dynGpu = new GpuMesh(); fxAddGpu = new GpuMesh(); fxAlphaGpu = new GpuMesh(); decalGpu = new GpuMesh(); cardGpu = new GpuMesh();

            Mat3.BuildArrays(out matTex, out matNTex, out gndTex, out gndNTex);
            noiseTex = Mat3.BuildNoise();
            uiTex = Tex.Empty(Game.W, Game.H, GL.RGBA8, GL.BGRA, GL.UNSIGNED_BYTE, false);
            shadow = Fbo.Create(ShadowSize, ShadowSize, 0, 2, 0, false);

            // samplers stay on fixed units: 0 materials, 2 shadow, 3 noise, 4 atlas, 5 material normals, 6 and 7 the ground and its
            // normals, 8 the ground types of the map
            pWorld.Use(); pWorld.I("uMat", 0); pWorld.I("uShadow", 2); pWorld.I("uNoise", 3); pWorld.I("uAtlas", 4);
            pWorld.I("uMatN", 5); pWorld.I("uGnd", 6); pWorld.I("uGndN", 7); pWorld.I("uTypes", 8);
            pWorld.F1v("uMatK", MatGen.ModelK, MatGen.ModelK.Length);
            pWater.Use(); pWater.I("uShadow", 2); pWater.I("uNoise", 3);
            pShadow.Use(); pShadow.I("uAtlas", 4);
            pFx.Use(); pFx.I("uAtlas", 4);
            pBright.Use(); pBright.I("uSrc", 0);
            pBlur.Use(); pBlur.I("uSrc", 0);
            pFinal.Use(); pFinal.I("uSrc", 0); pFinal.I("uBloom", 1); pFinal.I("uBloom2", 2);
            pUi.Use(); pUi.I("uUi", 0);
            pBlit.Use(); pBlit.I("uSrc", 0);
            GL.UseProgram(0);
            Check("init");
            try
            {
                uiGpu = new UiGpu();
                uint e = GL.GetError();
                if (e != GL.NO_ERROR) throw new Exception("OpenGL error 0x" + e.ToString("X"));
            }
            catch (Exception ex)
            {
                Log.Line("interface drawn in software: " + ex.Message);
                if (uiGpu != null) { try { uiGpu.Dispose(); } catch { } uiGpu = null; }
                while (GL.GetError() != GL.NO_ERROR) { }
            }
        }

        static void Check(string where)
        {
            uint e = GL.GetError();
            if (e != GL.NO_ERROR) throw new Exception("OpenGL error 0x" + e.ToString("X") + " in " + where);
        }

        // targets
        /// <summary>Sets the size of the framebuffer the frame goes to (the window's client area, or an offscreen image).</summary>
        public void SetTarget(int w, int h, bool toImage)
        {
            w = Math.Max(64, w); h = Math.Max(64, h);
            offscreen = toImage;
            VW = w; VH = h;
            UiS = Math.Max(1, Math.Min(w / Game.W, h / Game.H));
            UiX = (w - Game.W * UiS) / 2; UiY = (h - Game.H * UiS) / 2;
            if (fbW != w || fbH != h || scene == null)
            {
                fbW = w; fbH = h;
                if (msaa != null) msaa.Dispose(); if (scene != null) scene.Dispose();
                if (half1 != null) { half1.Dispose(); half2.Dispose(); quart1.Dispose(); quart2.Dispose(); }
                msaa = samples > 1 ? Fbo.Create(w, h, GL.RGBA16F, 1, samples, false) : null;
                scene = Fbo.Create(w, h, GL.RGBA16F, samples > 1 ? 0 : 1, 0, true);
                half1 = Fbo.Create(Math.Max(1, w / 2), Math.Max(1, h / 2), GL.RGBA16F, 0, 0, true);
                half2 = Fbo.Create(Math.Max(1, w / 2), Math.Max(1, h / 2), GL.RGBA16F, 0, 0, true);
                quart1 = Fbo.Create(Math.Max(1, w / 8), Math.Max(1, h / 8), GL.RGBA16F, 0, 0, true);
                quart2 = Fbo.Create(Math.Max(1, w / 8), Math.Max(1, h / 8), GL.RGBA16F, 0, 0, true);
            }
            if (toImage && (shot == null || shot.W != w || shot.H != h))
            {
                if (shot != null) shot.Dispose();
                shot = Fbo.Create(w, h, GL.RGBA8, 0, 0, false);
            }
        }

        void BindTarget()
        {
            if (offscreen) shot.Bind();
            else { GL.BindFramebuffer(GL.FRAMEBUFFER, 0); GL.Viewport(0, 0, VW, VH); }
        }

        // camera
        /// <summary>
        /// Places the camera from the 2D camera state: it looks at the middle of what the flat view would show, from the
        /// south and above. Called every tick (aiming needs it) and every frame.
        /// </summary>
        public void UpdateCamera(float dt)
        {
            if (Fp) { FpCamera(); return; }
            Zoom += (ZoomTarget - Zoom) * Math.Min(1f, dt * 8f);
            float shx = g.CamXi - g.CamX, shy = g.CamYi - g.CamY;
            if (Math.Abs(shx) > 6) shx = 0; if (Math.Abs(shy) > 6) shy = 0;
            float tx = g.CamX + Game.W / 2f + shx, tz = g.CamY + Game.H / 2f + 12 + shy;
            float ground = HeightAt(tx, tz);
            float pitch = Pitch, dist = Dist * Zoom, yawOff = 0;
            if (TitleMode)
            {
                pitch = 24f * M.PI / 180f; dist = 560f;
                yawOff = M.Sin(g.UiT * 0.05f) * 0.18f;
                tz -= 60;
            }
            Look = new V3(tx, ground + 12, tz);
            Eye = new V3(tx + M.Sin(yawOff) * dist * M.Cos(pitch), ground + 12 + dist * M.Sin(pitch), tz + M.Cos(yawOff) * dist * M.Cos(pitch));
            M4.LookAt(View, Eye, Look, new V3(0, 1, 0));
            float aspect = VW / (float)VH;
            M4.Perspective(Proj, FovY, aspect, 24f, TitleMode ? 4200f : 2200f);
            M4.Mul(Proj, View, VP);
            M4.Invert(VP, InvVP);
            Frustum();
        }

        /// <summary>The camera in the hero's head: eye, heading, pitch and roll from the game; wide lens, close near plane.</summary>
        void FpCamera()
        {
            float cp = M.Cos(FpPitch);
            V3 dir = new V3(M.Cos(FpYaw) * cp, M.Sin(FpPitch), M.Sin(FpYaw) * cp);
            Eye = FpEye; ViewDir = dir;
            V3 right = V3.Cross(dir, new V3(0, 1, 0)).Norm;
            V3 up = V3.Cross(right, dir);
            if (FpRoll != 0) up = (up * M.Cos(FpRoll) + right * M.Sin(FpRoll)).Norm;
            float fx = M.Cos(FpYaw), fz = M.Sin(FpYaw);
            Look = new V3(Eye.X + fx * 140, HeightAt(Eye.X + fx * 140, Eye.Z + fz * 140) + 12, Eye.Z + fz * 140);
            M4.LookAt(View, Eye, Eye + dir, up);
            float aspect = VW / (float)VH;
            M4.Perspective(Proj, FpFov + FpFovKick, aspect, 1.2f, FogEnd() + 260f);
            M4.Mul(Proj, View, VP);
            M4.Invert(VP, InvVP);
            Frustum();
        }

        /// <summary>Whole chunks past the fog are not drawn in first person (they would come out as plain haze anyway).</summary>
        bool PastFog(float x0, float z0, float x1, float z1)
        {
            if (!Fp) return false;
            float dx = Math.Max(0, Math.Max(x0 - Eye.X, Eye.X - x1)), dz = Math.Max(0, Math.Max(z0 - Eye.Z, Eye.Z - z1));
            float f = FogEnd() + 40f;
            return dx * dx + dz * dz > f * f;
        }

        void Frustum()
        {
            float[] m = VP;
            for (int i = 0; i < 6; i++)
            {
                int row = i / 2; float sgn = (i & 1) == 0 ? 1 : -1;
                float a = m[3] + sgn * m[row], b = m[7] + sgn * m[4 + row], c = m[11] + sgn * m[8 + row], d = m[15] + sgn * m[12 + row];
                float l = (float)Math.Sqrt(a * a + b * b + c * c);
                frustum[i * 4] = a / l; frustum[i * 4 + 1] = b / l; frustum[i * 4 + 2] = c / l; frustum[i * 4 + 3] = d / l;
            }
        }

        public bool BoxVisible(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = frustum[i * 4], b = frustum[i * 4 + 1], c = frustum[i * 4 + 2], d = frustum[i * 4 + 3];
                float px = a > 0 ? x1 : x0, py = b > 0 ? y1 : y0, pz = c > 0 ? z1 : z0;
                if (a * px + b * py + c * pz + d < 0) return false;
            }
            return true;
        }

        /// <summary>World point (map x, map y, height above the ground) to interface pixels. False when behind the camera.</summary>
        public bool ToUi(float x, float y, float h, out float ux, out float uy)
        {
            float cx, cy, cz, cw;
            M4.Transform(VP, x, HeightAt(x, y) + h, y, out cx, out cy, out cz, out cw);
            if (cw < 1f) { ux = uy = -9999; return false; }
            float sx = (cx / cw * 0.5f + 0.5f) * VW, sy = (0.5f - cy / cw * 0.5f) * VH;
            ux = (sx - UiX) / UiS; uy = (sy - UiY) / UiS;
            return true;
        }

        /// <summary>Where the ray under an interface pixel meets the horizontal plane at height planeY.</summary>
        public bool Pick(float ux, float uy, float planeY, out float wx, out float wz)
        {
            float sx = UiX + ux * UiS, sy = UiY + uy * UiS;
            float nx = sx / VW * 2 - 1, ny = 1 - sy / VH * 2;
            float ax, ay, az, aw, bx, by, bz, bw;
            M4.Transform(InvVP, nx, ny, -1, out ax, out ay, out az, out aw);
            M4.Transform(InvVP, nx, ny, 1, out bx, out by, out bz, out bw);
            ax /= aw; ay /= aw; az /= aw; bx /= bw; by /= bw; bz /= bw;
            float dy = by - ay;
            wx = ax; wz = az;
            if (Math.Abs(dy) < 1e-5f) return false;
            float t = (planeY - ay) / dy;
            if (t < 0) t = 0;
            wx = ax + (bx - ax) * t; wz = az + (bz - az) * t;
            return true;
        }

        // frame
        public void Blank()
        {
            BindTarget();
            GL.ClearColor(0.03f, 0.03f, 0.05f, 1); GL.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);
        }

        /// <summary>Draws the world (play = gameplay elements as well, not just the scenery) into the target.</summary>
        static readonly System.Diagnostics.Stopwatch frameClock = System.Diagnostics.Stopwatch.StartNew();
        static double FT() { return frameClock.Elapsed.TotalMilliseconds; }

        public void Frame(bool play)
        {
            double f0 = FT();
            Map m = g.Cur;
            if (m == null) { Blank(); return; }
            if (!atlasReady && Assets.Built) { atlasTex = Mat3.BuildAtlas(); atlasReady = true; }
            MapGeo geo = Geo(m);
            Surface.Squash = 1f;
            UpdateCamera(0);
            ambR = g.AmbR; ambG = g.AmbG; ambB = g.AmbB; fogD = g.FogD; fogCol = g.FogCol;
            bool indoor = g.Zone.Indoor && !TitleMode;
            bool day = g.Dawn && !indoor;
            frameIndoor = indoor; frameDay = day;
            UpdateCamera(0);

            // gather what this frame shows
            double fa = FT();
            geo.Refresh();
            double f1 = FT();
            CollectLights(m, play, day);
            double f2 = FT();
            dyn.Clear(); fxAdd.Clear(); fxAlpha.Clear(); decal.Clear(); cards.Clear(); selfShadow.Clear(); vm.Clear(); vmFx.Clear();
            BuildActors(play);
            double f3 = FT();
            BuildEffects(play, geo);
            double f4 = FT();
            bool viewmodel = Fp && play && BuildViewmodel();
            double f5 = FT();
            dynGpu.Upload(dyn, true); fxAddGpu.Upload(fxAdd, true); fxAlphaGpu.Upload(fxAlpha, true); decalGpu.Upload(decal, true); cardGpu.Upload(cards, true);
            selfGpu.Upload(selfShadow, true);
            if (viewmodel) { vmGpu.Upload(vm, true); vmFxGpu.Upload(vmFx, true); }

            // shadow map (the moon, or the dawn sun)
            double f6 = FT();
            bool useShadow = Shadows && !indoor;
            if (useShadow) RenderShadow(geo);
            double f7 = FT();

            // scene
            Fbo target = msaa ?? scene;
            target.Bind();
            haze = new V3(fogCol >> 16 & 255, fogCol >> 8 & 255, fogCol & 255) * (1f / 255f);
            bool sky = TitleMode || (Fp && !indoor);
            if (Fp && !indoor) haze = SkyHorizon(day);       // distant land melts into the sky behind it
            else
            {
                float hk = indoor ? 0.07f : (day ? 0.55f : 0.16f);
                haze = new V3(haze.X * hk + 0.01f, haze.Y * hk + 0.01f, haze.Z * hk + 0.02f);
            }
            GL.ClearColor(haze.X, haze.Y, haze.Z, 1);
            GL.DepthMask(1);
            GL.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);
            GL.Enable(GL.DEPTH_TEST); GL.DepthFunc(GL.LEQUAL);
            GL.Disable(GL.CULL_FACE); GL.Disable(GL.BLEND);
            if (sky) DrawSky(day);

            BindWorldTextures(geo, useShadow);
            pWorld.Use();
            WorldUniforms(pWorld, geo, useShadow, indoor, day, play);
            // terrain
            pWorld.I("uMode", 1);
            for (int i = 0; i < geo.Chunks.Length; i++)
            {
                Chunk c = geo.Chunks[i];
                if (c.Terrain == null || !BoxVisible(c.X0, c.MinY, c.Z0, c.X1, c.MaxY, c.Z1) || PastFog(c.X0, c.Z0, c.X1, c.Z1)) continue;
                c.Terrain.Draw();
            }
            // models
            pWorld.I("uMode", 0);
            for (int i = 0; i < geo.Chunks.Length; i++)
            {
                Chunk c = geo.Chunks[i];
                if (c.Props == null || c.Props.Count == 0) continue;
                if (!BoxVisible(c.Props.MinX, c.Props.MinY, c.Props.MinZ, c.Props.MaxX, c.Props.MaxY, c.Props.MaxZ)) continue;
                if (PastFog(c.Props.MinX, c.Props.MinZ, c.Props.MaxX, c.Props.MaxZ)) continue;
                c.Props.Draw();
            }
            dynGpu.Draw();
            cardGpu.Draw();
            pWorld.F4("uTint", 0, 0, 0, 0);

            // water
            if (geo.Water != null && geo.Water.Count > 0)
            {
                GL.Enable(GL.BLEND); GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);
                GL.DepthMask(0);
                pWater.Use();
                WorldUniforms(pWater, geo, useShadow, indoor, day, play);
                pWater.F3("uDeep", 0.07f, 0.16f, 0.2f);
                pWater.F3("uSky", 0.24f + ambR * 0.1f, 0.3f + ambG * 0.1f, 0.42f + ambB * 0.12f);
                geo.Water.Draw();
                GL.DepthMask(1);
                GL.Disable(GL.BLEND);
            }

            // ground telegraphs and effects
            pFx.Use();
            pFx.Mat("uVP", VP); pFx.F3("uCam", Eye); pFx.F2("uFogDist", FogStart(), FogEnd());
            pFx.F3("uLitAmb", Math.Min(1.4f, ambR * 1.5f), Math.Min(1.4f, ambG * 1.5f), Math.Min(1.4f, ambB * 1.5f));
            GL.ActiveTexture(GL.TEXTURE0 + 4); GL.BindTexture(GL.TEXTURE_2D, atlasTex);
            GL.Enable(GL.BLEND); GL.DepthMask(0);
            GL.Enable(GL.POLYGON_OFFSET_FILL); GL.PolygonOffset(-2f, -8f);
            GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            pFx.F("uAdd", 1); pFx.F("uBoost", 1.25f);
            decalGpu.Draw();
            GL.Disable(GL.POLYGON_OFFSET_FILL);
            pFx.F("uAdd", 0); pFx.F("uBoost", 1f);
            GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);
            fxAlphaGpu.Draw();
            GL.BlendFunc(GL.ONE, GL.ONE);
            pFx.F("uAdd", 1); pFx.F("uBoost", 1.6f);
            fxAddGpu.Draw();
            GL.DepthMask(1); GL.Disable(GL.BLEND);
            GL.BindVertexArray(0);

            // the hero's own hands and weapon, drawn last over a cleared depth so they never sink into walls
            double f8 = FT();
            if (viewmodel) DrawViewmodel(geo, indoor, day, play);
            double f9 = FT();

            // post
            if (msaa != null)
            {
                GL.BindFramebuffer(GL.READ_FRAMEBUFFER, msaa.Id);
                GL.BindFramebuffer(GL.DRAW_FRAMEBUFFER, scene.Id);
                GL.BlitFramebuffer(0, 0, fbW, fbH, 0, 0, fbW, fbH, GL.COLOR_BUFFER_BIT, GL.NEAREST_U);
            }
            GL.Disable(GL.DEPTH_TEST);
            if (Bloom)
            {
                half1.Bind(); pBright.Use(); pBright.F2("uTexel", 1f / fbW, 1f / fbH);
                GL.ActiveTexture(GL.TEXTURE0); GL.BindTexture(GL.TEXTURE_2D, scene.ColorTex); quad.Draw();
                Blur(half1, half2);
                quart1.Bind(); pBlit.Use(); GL.ActiveTexture(GL.TEXTURE0); GL.BindTexture(GL.TEXTURE_2D, half1.ColorTex); quad.Draw();
                Blur(quart1, quart2); Blur(quart1, quart2);
            }
            BindTarget();
            pFinal.Use();
            pFinal.F("uBloomK", Bloom ? (day ? 0.6f : 0.95f) : 0);
            pFinal.F("uVig", 1f);
            pFinal.F("uSat", day ? 1.05f : 1.0f);
            pFinal.F3("uGrade", 1.02f, 1.0f, 1.02f);
            GL.ActiveTexture(GL.TEXTURE0); GL.BindTexture(GL.TEXTURE_2D, scene.ColorTex);
            GL.ActiveTexture(GL.TEXTURE0 + 1); GL.BindTexture(GL.TEXTURE_2D, half1.ColorTex);
            GL.ActiveTexture(GL.TEXTURE0 + 2); GL.BindTexture(GL.TEXTURE_2D, quart1.ColorTex);
            quad.Draw();
            GL.ActiveTexture(GL.TEXTURE0);
            GL.BindVertexArray(0);
            double f10 = FT();
            if (f10 - f0 > 80)
                Log.Line("slow 3D frame " + (f10 - f0).ToString("0") + " ms: setup " + (fa - f0).ToString("0") + ", refresh " + (f1 - fa).ToString("0") + ", lights " + (f2 - f1).ToString("0")
                    + ", actors " + (f3 - f2).ToString("0") + ", effects " + (f4 - f3).ToString("0") + ", viewmodel " + (f5 - f4).ToString("0") + ", upload " + (f6 - f5).ToString("0")
                    + ", shadow " + (f7 - f6).ToString("0") + ", scene " + (f8 - f7).ToString("0") + ", hands " + (f9 - f8).ToString("0") + ", post " + (f10 - f9).ToString("0"));
        }

        void Blur(Fbo a, Fbo b)
        {
            pBlur.Use();
            b.Bind(); pBlur.F2("uDir", 1f / a.W, 0); GL.ActiveTexture(GL.TEXTURE0); GL.BindTexture(GL.TEXTURE_2D, a.ColorTex); quad.Draw();
            a.Bind(); pBlur.F2("uDir", 0, 1f / a.H); GL.BindTexture(GL.TEXTURE_2D, b.ColorTex); quad.Draw();
        }

        float FogStart()
        {
            if (Fp) return frameIndoor ? 30f : (frameDay ? 520f : 210f);
            return TitleMode ? 700f : Dist * Zoom + 120f;
        }

        float FogEnd()
        {
            if (Fp) return frameIndoor ? 640f : (frameDay ? 2900f : 1700f);
            return TitleMode ? 2600f : Dist * Zoom + 720f;
        }

        /// <summary>The sky's colour at the horizon (the haze far land fades into).</summary>
        V3 SkyHorizon(bool day)
        {
            if (day) return new V3(0.86f, 0.74f, 0.6f);
            V3 hz = new V3((fogCol >> 16 & 255) / 255f, (fogCol >> 8 & 255) / 255f, (fogCol & 255) / 255f);
            return new V3(hz.X * 0.25f + 0.012f, hz.Y * 0.26f + 0.014f, hz.Z * 0.32f + 0.03f);
        }

        void BindWorldTextures(MapGeo geo, bool useShadow)
        {
            GL.ActiveTexture(GL.TEXTURE0); GL.BindTexture(GL.TEXTURE_2D_ARRAY, matTex);
            GL.ActiveTexture(GL.TEXTURE0 + 5); GL.BindTexture(GL.TEXTURE_2D_ARRAY, matNTex);
            GL.ActiveTexture(GL.TEXTURE0 + 6); GL.BindTexture(GL.TEXTURE_2D_ARRAY, gndTex);
            GL.ActiveTexture(GL.TEXTURE0 + 7); GL.BindTexture(GL.TEXTURE_2D_ARRAY, gndNTex);
            GL.ActiveTexture(GL.TEXTURE0 + 8); GL.BindTexture(GL.TEXTURE_2D, geo.TypeTex);
            GL.ActiveTexture(GL.TEXTURE0 + 2); GL.BindTexture(GL.TEXTURE_2D, shadow.DepthTex);
            GL.ActiveTexture(GL.TEXTURE0 + 3); GL.BindTexture(GL.TEXTURE_2D, noiseTex);
            GL.ActiveTexture(GL.TEXTURE0 + 4); GL.BindTexture(GL.TEXTURE_2D, atlasTex);
            GL.ActiveTexture(GL.TEXTURE0);
        }

        void WorldUniforms(Prog p, MapGeo geo, bool useShadow, bool indoor, bool day, bool play)
        {
            p.Mat("uVP", VP);
            p.Mat("uShadowVP", shadowVP);
            p.F("uTime", g.UiT);
            p.F4v("uPush", push, 4);
            p.F4("uTypeXf", geo.TypeScale, geo.TypeScale, geo.TypeOffX, geo.TypeOffZ);
            p.F3v("uGTint", geo.GTint, MatGen.GCount);
            p.F("uWaterY", MapGeo.WaterY);
            float a = indoor ? 1.0f : 0.9f;
            p.F3("uAmbSky", ambR * a, ambG * a, ambB * a);
            p.F3("uAmbGround", ambR * 0.52f, ambG * 0.5f, ambB * 0.55f);
            if (indoor) { p.F3("uSunCol", 0, 0, 0); }
            else if (day) p.F3("uSunCol", 0.62f, 0.5f, 0.36f);
            else p.F3("uSunCol", 0.16f + ambR * 0.12f, 0.18f + ambG * 0.12f, 0.27f + ambB * 0.15f);
            p.F3("uSunDir", day ? new V3(0.55f, -0.8f, 0.25f).Norm : SunDir);
            p.F("uShadowOn", useShadow ? 1 : 0);
            p.F("uShadowTexel", 1f / ShadowSize);
            p.I("uNL", nl);
            p.F4v("uLP", lp, nl); p.F4v("uLC", lc, nl);
            p.Rgb("uFogCol", fogCol, 1f);
            p.F("uFogD", fogD);
            p.F3("uCam", Eye);
            p.F2("uFogDist", FogStart(), FogEnd());
            p.F3("uHaze", haze);
            p.F4("uTint", 0, 0, 0, 0);
            if (cutOn && play)
            {
                float cx, cy, cz, cw;
                M4.Transform(VP, cutHero.X, cutHero.Y, cutHero.Z, out cx, out cy, out cz, out cw);
                float sx = (cx / cw * 0.5f + 0.5f) * fbW, sy = (cy / cw * 0.5f + 0.5f) * fbH;
                float rad = 64f * UiS * (fbH / (float)Math.Max(1, VH));
                p.F4("uCut", sx, sy, rad, (cutHero - Eye).Len - 8f);
            }
            else p.F4("uCut", 0, 0, 0, 0);
        }

        void DrawSky(bool day)
        {
            GL.DepthMask(0);
            pSky.Use();
            pSky.Mat("uInvVP", InvVP);
            V3 h = SkyHorizon(day);
            pSky.F3("uHorizon", h.X, h.Y, h.Z);
            if (day) { pSky.F3("uZenith", 0.38f, 0.5f, 0.72f); pSky.F("uStars", 0); }
            else { pSky.F3("uZenith", 0.02f, 0.025f, 0.06f); pSky.F("uStars", 1); }
            pSky.F3("uMoonDir", -SunDir.X, -SunDir.Y * 0.35f, -SunDir.Z - 0.9f);
            pSky.F("uTime", g.UiT);
            quad.Draw();
            GL.DepthMask(1);
        }

        void RenderShadow(MapGeo geo)
        {
            // an orthographic box around the view, snapped to whole texels so edges do not crawl
            V3 dir = g.Dawn ? new V3(0.55f, -0.8f, 0.25f).Norm : SunDir;
            float ext = TitleMode ? 900f : 470f * Math.Max(1f, Zoom);
            V3 c = new V3(Look.X, Look.Y, Look.Z - (TitleMode ? 300 : 90));
            if (Fp)
            {
                // the box reaches ahead of the eye, where nearly everything shadowed in view stands
                float fx = M.Cos(FpYaw), fz = M.Sin(FpYaw);
                ext = 620f;
                c = new V3(Eye.X + fx * 330, HeightAt(Eye.X + fx * 330, Eye.Z + fz * 330), Eye.Z + fz * 330);
            }
            V3 eye = c - dir * 900f;
            V3 up = Math.Abs(dir.Y) > 0.95f ? new V3(0, 0, -1) : new V3(0, 1, 0);
            M4.LookAt(tmp, eye, c, up);
            float texel = ext * 2 / ShadowSize;
            tmp[12] = (float)Math.Floor(tmp[12] / texel) * texel; tmp[13] = (float)Math.Floor(tmp[13] / texel) * texel;
            M4.Ortho(tmp2, -ext, ext, -ext, ext, 100f, 1900f);
            M4.Mul(tmp2, tmp, shadowVP);
            shadow.Bind();
            GL.Clear(GL.DEPTH_BUFFER_BIT);
            GL.ActiveTexture(GL.TEXTURE0 + 4); GL.BindTexture(GL.TEXTURE_2D, atlasTex); GL.ActiveTexture(GL.TEXTURE0);
            GL.Enable(GL.DEPTH_TEST); GL.DepthFunc(GL.LEQUAL);
            GL.Enable(GL.POLYGON_OFFSET_FILL); GL.PolygonOffset(1.6f, 3f);
            pShadow.Use();
            pShadow.Mat("uVP", shadowVP);
            pShadow.F("uTime", g.UiT);
            pShadow.F4v("uPush", push, 4);
            float x0 = c.X - ext * 1.6f, x1 = c.X + ext * 1.6f, z0 = c.Z - ext * 1.6f, z1 = c.Z + ext * 1.6f;
            for (int i = 0; i < geo.Chunks.Length; i++)
            {
                Chunk ch = geo.Chunks[i];
                if (ch.Props == null || ch.Props.Count == 0) continue;
                if (ch.Props.MaxX < x0 || ch.Props.MinX > x1 || ch.Props.MaxZ < z0 || ch.Props.MinZ > z1) continue;
                ch.Props.Draw();
            }
            dynGpu.Draw();
            selfGpu.Draw();
            GL.Disable(GL.POLYGON_OFFSET_FILL);
        }

        // interface layer
        /// <summary>Lays the 640x360 interface (premultiplied alpha) over the frame, scaled by a whole number.</summary>
        public void Composite(Surface ui)
        {
            BindTarget();
            if (ui.Rec != null && uiGpu != null && ui.Rec == uiGpu.Rec)
            {
                uiGpu.Draw(UiX, UiY, Game.W * UiS, Game.H * UiS, VW, VH);
                return;
            }
            GL.Disable(GL.DEPTH_TEST); GL.Disable(GL.CULL_FACE);
            GL.Enable(GL.BLEND); GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            bool full = ui.Bands == 0;
            if (uiTexW != ui.PW || uiTexH != ui.PH || uiTexLinear != (ui.PW != Game.W * UiS))
            {
                // the interface is drawn at the window's scale: one texel per screen pixel (filtered if they ever differ)
                Tex.Delete(ref uiTex);
                uiTexLinear = ui.PW != Game.W * UiS;
                uiTex = Tex.Empty(ui.PW, ui.PH, GL.RGBA8, GL.BGRA, GL.UNSIGNED_BYTE, uiTexLinear);
                uiTexW = ui.PW; uiTexH = ui.PH;
                full = true;
            }
            GL.ActiveTexture(GL.TEXTURE0); GL.BindTexture(GL.TEXTURE_2D, uiTex);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            if (UiUploadMode == 1) full = true;
            bool viaPbo = UiUploadMode == 2;
            if (viaPbo)
            {
                // a pixel buffer, orphaned each frame, lets the card copy into the texture on its own time
                if (uiPbo == 0) { uint id; GL.GenBuffers(1, &id); uiPbo = id; }
                GL.BindBuffer(GL.PIXEL_UNPACK_BUFFER, uiPbo);
                GL.BufferData(GL.PIXEL_UNPACK_BUFFER, (IntPtr)(ui.PW * ui.PH * 4), IntPtr.Zero, GL.STREAM_DRAW);
            }
            GL.PixelStorei(GL.UNPACK_ROW_LENGTH, ui.PW);
            int bh = full ? ui.PH : ui.BandH, nb = full ? 1 : ui.Bands;
            fixed (int* p = ui.Px)
            {
                // only the bands drawn on (this frame or the last) change; neighbouring bands go up together
                int b = 0;
                while (b < nb)
                {
                    int ux0 = 0, ux1 = ui.PW;
                    if (!full) ui.Changed(b, out ux0, out ux1);
                    if (ux1 <= ux0) { b++; continue; }
                    int b1 = b + 1;
                    while (b1 < nb)
                    {
                        int nx0, nx1;
                        ui.Changed(b1, out nx0, out nx1);
                        if (nx1 <= nx0) break;
                        ux0 = Math.Min(ux0, nx0); ux1 = Math.Max(ux1, nx1); b1++;
                    }
                    int uy0 = b * bh, uy1 = Math.Min(ui.PH, b1 * bh);
                    if (viaPbo)
                    {
                        GL.BufferSubData(GL.PIXEL_UNPACK_BUFFER, (IntPtr)(uy0 * ui.PW * 4), (IntPtr)((uy1 - uy0) * ui.PW * 4), (IntPtr)(p + uy0 * ui.PW));
                        GL.TexSubImage2D(GL.TEXTURE_2D, 0, ux0, uy0, ux1 - ux0, uy1 - uy0, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)((uy0 * ui.PW + ux0) * 4));
                    }
                    else GL.TexSubImage2D(GL.TEXTURE_2D, 0, ux0, uy0, ux1 - ux0, uy1 - uy0, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)(p + uy0 * ui.PW + ux0));
                    b = b1;
                }
            }
            GL.PixelStorei(GL.UNPACK_ROW_LENGTH, 0);
            if (viaPbo) GL.BindBuffer(GL.PIXEL_UNPACK_BUFFER, 0);
            ui.Uploaded();
            pUi.Use();
            float x0 = UiX / (float)VW * 2 - 1, y1 = 1 - UiY / (float)VH * 2;
            float w = Game.W * UiS / (float)VW * 2, h = Game.H * UiS / (float)VH * 2;
            pUi.F4("uRect", x0, y1 - h, w, h);
            quad.Draw();
            GL.Disable(GL.BLEND);
            GL.BindVertexArray(0);
        }

        /// <summary>Reads the finished offscreen frame back (tests and screenshots).</summary>
        public Bitmap ReadImage()
        {
            int w = VW, h = VH;
            byte[] buf = new byte[w * h * 4];
            GL.BindFramebuffer(GL.FRAMEBUFFER, offscreen ? shot.Id : 0);
            if (!offscreen) GL.ReadBuffer(GL.BACK_LEFT);
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            fixed (byte* p = buf) GL.ReadPixels(0, 0, w, h, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)p);
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            for (int y = 0; y < h; y++)
                System.Runtime.InteropServices.Marshal.Copy(buf, (h - 1 - y) * w * 4, bd.Scan0 + y * bd.Stride, w * 4);
            bmp.UnlockBits(bd);
            return bmp;
        }

        public void SavePng(string path)
        {
            using (Bitmap b = ReadImage()) b.Save(path, ImageFormat.Png);
        }

        // lights
        void AddLight(float x, float y, float height, float r, int color, float intensity)
        {
            if (r <= 1 || intensity <= 0.01f) return;
            LightSrc s; s.X = x; s.Z = y; s.Y = HeightAt(x, y) + height; s.R = r * 1.18f; s.I = intensity * 1.22f; s.C = color;
            if (Fp)
            {
                // nearest first, but a light well behind the eye only matters for what it spills forward
                float ex = x - Eye.X, ez = y - Eye.Z;
                float ahead = ex * ViewDir.X + ez * ViewDir.Z;
                s.D = ex * ex + ez * ez - r * r * 0.5f + (ahead < -r ? ahead * ahead * 2f : 0);
            }
            else
            {
                float dx = x - Look.X, dz = y - Look.Z;
                s.D = dx * dx + dz * dz * 0.6f - r * r * 0.25f;
            }
            lights.Add(s);
        }

        static readonly Comparison<LightSrc> byDist = delegate(LightSrc a, LightSrc b) { return a.D.CompareTo(b.D); };
        readonly List<Prop> lightProps = new List<Prop>(512);

        void CollectLights(Map m, bool play, bool day)
        {
            lights.Clear();
            float propScale = day ? 0.3f : 1f;
            float rx = TitleMode ? 900 : 560, rzN = TitleMode ? 1100 : 560, rzS = 260;
            lightProps.Clear();
            if (Fp) m.Query(Eye.X - 760, Eye.Z - 760, Eye.X + 760, Eye.Z + 760, lightProps);
            else m.Query(Look.X - rx, Look.Z - rzN, Look.X + rx, Look.Z + rzS, lightProps);
            for (int i = 0; i < lightProps.Count; i++)
            {
                Prop p = lightProps[i];
                if (p.LightR <= 0) continue;
                float fl = 1f;
                if (p.Flicker) fl = 0.93f + 0.05f * M.Sin(g.UiT * 9f + p.X * 0.31f) + 0.04f * M.Sin(g.UiT * 23f + p.Y * 0.17f);
                float hgt = Math.Max(6f, -p.LightOY + 4f);
                if (p.OnWall) hgt += 10;
                float lampY = PropModels.LightY(p);
                if (lampY > 0) hgt = lampY;
                AddLight(p.X, p.Y + (p.OnWall ? -6 : 0), hgt, p.LightR * fl, p.LightColor, p.LightI * fl * propScale);
            }
            if (play) CollectActorLights(day);
            lights.Sort(byDist);
            nl = Math.Min(MaxLights, lights.Count);
            for (int i = 0; i < nl; i++)
            {
                LightSrc s = lights[i];
                lp[i * 4] = s.X; lp[i * 4 + 1] = s.Y; lp[i * 4 + 2] = s.Z; lp[i * 4 + 3] = s.R;
                lc[i * 4] = ((s.C >> 16) & 255) / 255f * s.I; lc[i * 4 + 1] = ((s.C >> 8) & 255) / 255f * s.I; lc[i * 4 + 2] = (s.C & 255) / 255f * s.I; lc[i * 4 + 3] = 0;
            }
        }

        void CollectActorLights(bool day)
        {
            Game G = g;
            bool indoor = G.Zone.Indoor;
            for (int k = 0; k < G.Party.Count; k++)
            {
                Player p = G.Party[k];
                if (p.Dead) continue;
                float li = (indoor ? 0.62f : 0.4f) * (p.StealthT > 0 ? 0.45f : 1f) * (day ? 0.2f : 1f);
                AddLight(p.X, p.Y, 26, indoor ? 112 : 96, 0xFFD9B0, li);
                if (p.CastT > 0 && p.Class == ClassId.Mage) AddLight(p.X, p.Y, 16, 60, 0xFFB060, 0.5f);
                if (p.OathT > 0) AddLight(p.X, p.Y, 14, 74, 0xFFD070, 0.55f);
                if (p == G.P && G.LevelUpT < 1f) AddLight(p.X, p.Y, 20, 150, 0xFFE8A0, 1.3f * (1f - G.LevelUpT));
            }
            for (int i = 0; i < G.Shots.Count; i++)
            {
                Projectile s = G.Shots[i];
                if (s.LightR > 0) AddLight(s.X, s.Y, 8 + s.Z, s.LightR, s.LightColor, 0.9f);
            }
            for (int i = 0; i < G.Areas.Count; i++)
            {
                Area a = G.Areas[i];
                float t = a.Dur > 0 ? M.Clamp01(a.T / a.Dur) : 0;
                switch (a.Kind)
                {
                    case Area.Sigil: AddLight(a.X, a.Y, 8, a.R * 2.4f, 0xA890FF, a.Armed ? 1.3f : 0.3f + 0.5f * M.Clamp01(a.T / a.Delay)); break;
                    case Area.Burst: AddLight(a.X, a.Y, 10, a.R * 3f, 0xFFA850, 1.6f * (1 - t)); break;
                    case Area.Bolt: AddLight(a.X2, a.Y2, 16, 90, 0xC8E8FF, 1.4f * (1 - t)); break;
                    case Area.Ring: AddLight(a.X, a.Y, 8, a.R * 2.2f, a.Color, 0.9f * (1 - t)); break;
                    case Area.Pool: AddLight(a.X, a.Y, 6, a.R * 2f, 0x80FF60, 0.35f); break;
                }
            }
            for (int i = 0; i < G.Foes.Count; i++)
            {
                Enemy e = G.Foes[i];
                if (e.Dead) continue;
                if (e.Kind == EnemyKind.Wisp) AddLight(e.X, e.Y, 14 + e.Z, 58, 0xB0A8FF, 0.75f * e.FadeIn);
                else if (e.Kind == EnemyKind.Prior) AddLight(e.X, e.Y, 30, 84, 0x9080E0, 0.75f);
                else if (e.Kind == EnemyKind.Knight) AddLight(e.X, e.Y, 22, 46, 0x80C0FF, 0.4f);
                else if (e.Kind == EnemyKind.Acolyte && e.State == Enemy.Windup) AddLight(e.X, e.Y, 18, 54, 0xA890FF, 0.7f * e.StateT / Math.Max(0.01f, e.StateDur));
                if (e.BurnT > 0) AddLight(e.X, e.Y, 10, 38, 0xFFA050, 0.5f);
            }
            for (int i = 0; i < G.Drops.Count; i++)
            {
                Pickup d = G.Drops[i];
                if (d.Kind == Pickup.Equip && d.It != null && d.It.Rar >= Rarity.Fine) AddLight(d.X, d.Y, 8, 26 + (int)d.It.Rar * 8, Items.RarityColor[(int)d.It.Rar], 0.55f);
                else if (d.Kind == Pickup.Quest) AddLight(d.X, d.Y, 8, 46, 0xC8D8FF, 0.9f);
            }
        }

        // maps
        readonly Dictionary<Map, MapGeo> geos = new Dictionary<Map, MapGeo>();

        /// <summary>The world is built for walking through at eye height (first person) rather than for looking down on.</summary>
        public bool FpWorld { get { return g.Cfg.Camera != "top"; } }

        internal MapGeo Geo(Map m)
        {
            MapGeo geo;
            if (geos.TryGetValue(m, out geo))
            {
                if (geo.TallWalls == (m.Dungeon && FpWorld)) return geo;
                geo.Dispose(); geos.Remove(m);          // switched between first person and overhead: the walls change height
            }
            // a rebuilt map replaces the old one with the same id
            List<Map> stale = new List<Map>();
            foreach (KeyValuePair<Map, MapGeo> kv in geos) if (kv.Key.Id == m.Id) stale.Add(kv.Key);
            foreach (Map s in stale) { geos[s].Dispose(); geos.Remove(s); }
            geo = new MapGeo(this, m);
            geos[m] = geo;
            return geo;
        }

        /// <summary>Builds the 3D geometry for a map ahead of time (during the loading screen).</summary>
        public void Prepare(Map m)
        {
            if (m == null) return;
            if (!atlasReady && Assets.Built) { atlasTex = Mat3.BuildAtlas(); atlasReady = true; }
            Geo(m);
        }

        public float HeightAt(float x, float y)
        {
            Map m = g.Cur;
            if (m == null) return 0;
            MapGeo geo;
            if (!geos.TryGetValue(m, out geo)) return 0;
            return geo.HeightAt(x, y);
        }

        public void Dispose()
        {
            foreach (MapGeo m in geos.Values) m.Dispose();
            geos.Clear();
            if (uiPbo != 0) { uint id = uiPbo; GL.DeleteBuffers(1, &id); uiPbo = 0; }
            if (uiGpu != null) { uiGpu.Dispose(); uiGpu = null; }
        }
    }
}
