namespace Vesperfell
{
    /// <summary>GLSL 3.30 sources for every pass of the 3D renderer.</summary>
    internal static class Shaders
    {
        const string Head = "#version 330 core\n";

        const string Attribs =
@"layout(location=0) in vec3 aPos;
layout(location=1) in vec4 aNrm;
layout(location=2) in vec4 aCol;
layout(location=3) in vec2 aUv;
layout(location=4) in vec4 aExt;
";

        // wind sway, plus grass and reeds bending away from whoever walks through them
        const string Sway =
@"uniform float uTime;
uniform vec4 uPush[4];
vec3 sway(vec3 p, float w) {
    if (w <= 0.0) return p;
    float ph = uTime * 1.7 + p.x * 0.06 + p.z * 0.04;
    vec2 s = vec2(sin(ph) * 1.5, cos(ph * 0.8) * 0.45) * w;
    for (int i = 0; i < 4; i++) {
        vec2 d = p.xz - uPush[i].xz;
        float dl = length(d);
        float r = uPush[i].w;
        if (dl < r) s += d / max(dl, 0.01) * (r - dl) * 0.38 * w;
    }
    p.xz += s;
    return p;
}
";

        public const string WorldVs = Head + Attribs + Sway +
@"uniform mat4 uVP;
uniform mat4 uShadowVP;
out vec3 vW; out vec3 vN; out vec4 vC; out vec2 vUv; flat out float vL; out vec4 vS; out float vOcc;
void main() {
    vec3 p = sway(aPos, aExt.y / 255.0);
    vW = p; vN = aNrm.xyz; vC = aCol; vUv = aUv; vL = aExt.x; vOcc = aExt.z;
    vS = uShadowVP * vec4(p + aNrm.xyz * 0.9, 1.0);
    gl_Position = uVP * vec4(p, 1.0);
}
";

        // lighting shared by the world and the water: ambient from sky and ground, the moon (or the sun), point lights
        const string Lighting =
@"uniform vec3 uAmbSky; uniform vec3 uAmbGround; uniform vec3 uSunDir; uniform vec3 uSunCol;
uniform vec4 uLP[40]; uniform vec4 uLC[40]; uniform int uNL;
uniform sampler2DShadow uShadow; uniform float uShadowTexel; uniform float uShadowOn;
uniform vec3 uFogCol; uniform float uFogD; uniform vec3 uCam; uniform vec2 uFogDist; uniform vec3 uHaze;
uniform sampler2D uNoise;
float shadowAt(vec4 sp) {
    if (uShadowOn < 0.5) return 1.0;
    vec3 s = sp.xyz / sp.w * 0.5 + 0.5;
    if (s.x <= 0.002 || s.x >= 0.998 || s.y <= 0.002 || s.y >= 0.998 || s.z >= 0.999) return 1.0;
    float t = uShadowTexel, sum = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            sum += texture(uShadow, vec3(s.xy + vec2(float(x), float(y)) * t, s.z - 0.0004));
    return sum / 9.0;
}
vec3 lightAt(vec3 P, vec3 N, float sh) {
    float hemi = N.y * 0.5 + 0.5;
    vec3 c = mix(uAmbGround, uAmbSky, hemi);
    c += uSunCol * max(dot(N, -uSunDir), 0.0) * sh;
    for (int i = 0; i < uNL; i++) {
        vec3 L = uLP[i].xyz - P;
        float r = uLP[i].w;
        float d2 = dot(L, L);
        if (d2 >= r * r) continue;
        float d = sqrt(d2);
        float t = 1.0 - d / r; t *= t;
        float nl = dot(N, L / max(d, 0.001));
        c += uLC[i].rgb * t * (0.42 + 0.58 * max(nl, 0.0));
    }
    return c;
}
vec3 atmosphere(vec3 col, vec3 P, vec3 lit) {
    // drifting ground mist, lit by whatever lights the spot (as in the 2D renderer)
    float n1 = texture(uNoise, P.xz / 520.0 + vec2(uTime * 0.0135, uTime * 0.0048)).r;
    float n2 = texture(uNoise, P.xz * vec2(1.0, 1.25) / 330.0 + vec2(-uTime * 0.0075, uTime * 0.0095)).r;
    float f = clamp(n1 * n2 * 2.0 - 0.27, 0.0, 1.0) * uFogD;
    f *= clamp(1.0 - P.y / 46.0, 0.0, 1.0);
    col = col * (1.0 - 0.59 * f) + uFogCol * min(lit, vec3(1.7)) * f;
    float dist = distance(P, uCam);
    float far = clamp((dist - uFogDist.x) / (uFogDist.y - uFogDist.x), 0.0, 1.0);
    return mix(col, uHaze, far * far * (3.0 - 2.0 * far));
}
";

        public const string WorldFs = Head +
@"in vec3 vW; in vec3 vN; in vec4 vC; in vec2 vUv; flat in float vL; in vec4 vS; in float vOcc;
out vec4 o;
uniform float uTime;
uniform int uMode;
uniform sampler2DArray uMat; uniform sampler2DArray uMatN; uniform float uMatK[24];
uniform sampler2DArray uGnd; uniform sampler2DArray uGndN;
uniform sampler2D uTypes; uniform vec4 uTypeXf; uniform vec3 uGTint[13];
uniform sampler2D uAtlas;
uniform vec4 uCut;
uniform float uWaterY;
uniform vec4 uTint;
" + Lighting +
@"const float bayer[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

// the light (as lightAt) and, through spec, the shine of the moon and of every lamp on a surface with gloss
vec3 lightSpec(vec3 P, vec3 N, vec3 V, float sh, float gloss, out vec3 spec) {
    float hemi = N.y * 0.5 + 0.5;
    vec3 c = mix(uAmbGround, uAmbSky, hemi);
    float pw = exp2(3.0 + gloss * 7.0), ks = gloss * (pw + 2.0) * 0.03;
    vec3 S = -uSunDir;
    float ns = dot(N, S);
    c += uSunCol * max(ns, 0.0) * sh;
    spec = ns > 0.0 ? uSunCol * (pow(max(dot(N, normalize(S + V)), 0.0), pw) * sh) : vec3(0.0);
    for (int i = 0; i < uNL; i++) {
        vec3 L = uLP[i].xyz - P;
        float r = uLP[i].w;
        float d2 = dot(L, L);
        if (d2 >= r * r) continue;
        float d = sqrt(d2);
        float t = 1.0 - d / r; t *= t;
        vec3 Ld = L / max(d, 0.001);
        float nl = dot(N, Ld);
        c += uLC[i].rgb * t * (0.42 + 0.58 * max(nl, 0.0));
        if (nl > 0.0) spec += uLC[i].rgb * (t * pow(max(dot(N, normalize(Ld + V)), 0.0), pw));
    }
    spec *= ks;
    return c;
}

// a normal from a normal map, in the frame the texture coordinates make across the screen
vec3 bump(vec3 N, vec3 tn, vec3 dpx, vec3 dpy, vec2 dux, vec2 duy) {
    vec3 a = cross(dpy, N), b = cross(N, dpx);
    vec3 T = a * dux.x + b * duy.x, B = a * dux.y + b * duy.y;
    float m = max(dot(T, T), dot(B, B));
    if (m < 1e-30) return N;
    float k = inversesqrt(m);
    return normalize(T * (tn.x * k) + B * (tn.y * k) + N * tn.z);
}

int gcode(ivec2 c, ivec2 mx) { return int(texelFetch(uTypes, clamp(c, ivec2(0), mx), 0).r * 255.0 + 0.5); }

// one ground layer at a world spot: colour with height, the normal (x along +x, y along +z) and gloss
void gsample(int code, vec2 xz, vec2 gx, vec2 gy, out vec4 a, out vec3 n, out float gl) {
    float layer = float(code & 15);
    vec2 uv = xz * (1.0 / 64.0);
    gx *= 1.0 / 64.0; gy *= 1.0 / 64.0;
    bool sw = (code & 32) != 0;
    if (sw) { uv = uv.yx; gx = gx.yx; gy = gy.yx; }
    vec2 fl = vec2(1.0);
    if ((code & 15) == 10) {
        // undercroft flags: every 64-unit block mirrored its own way, so the slabs never visibly repeat
        float h = fract(sin(dot(floor(uv), vec2(12.9898, 78.233))) * 43758.5453);
        fl = vec2(h < 0.5 ? -1.0 : 1.0, fract(h * 7.31) < 0.5 ? -1.0 : 1.0);
        uv *= fl; gx *= fl; gy *= fl;
    }
    a = textureGrad(uGnd, vec3(uv, layer), gx, gy);
    vec4 q = textureGrad(uGndN, vec3(uv, layer), gx, gy);
    n = q.xyz * 2.0 - 1.0; gl = q.w;
    if ((code & 128) == 0) {
        // natural ground: a second copy, turned and enlarged, fades in by broad patches so no repeat shows
        float m = smoothstep(0.4, 0.6, textureLod(uNoise, xz / 173.0, 0.0).g);
        if (m > 0.003) {
            const mat2 R = mat2(0.8, 0.6, -0.6, 0.8);
            vec2 uv2 = R * uv * 0.71 + vec2(0.37, 0.61), g2x = R * gx * 0.71, g2y = R * gy * 0.71;
            vec4 a2 = textureGrad(uGnd, vec3(uv2, layer), g2x, g2y);
            vec4 q2 = textureGrad(uGndN, vec3(uv2, layer), g2x, g2y);
            vec3 n2 = q2.xyz * 2.0 - 1.0; n2.xy = transpose(R) * n2.xy;
            a = mix(a, a2, m); n = mix(n, n2, m); gl = mix(gl, q2.w, m);
        }
    }
    n.xy *= fl;
    if (sw) n.xy = n.yx;
}

void main() {
    // screen-space slopes first, while every pixel of the quad still runs
    vec3 dpx = dFdx(vW), dpy = dFdy(vW);
    vec2 dux = dFdx(vUv), duy = dFdy(vUv);
    if (vOcc > 0.5 && uCut.z > 0.0) {
        vec2 d = (gl_FragCoord.xy - uCut.xy) / uCut.z;
        float r = length(d * vec2(1.0, 0.8));
        if (r < 1.0 && distance(vW, uCam) < uCut.w) {
            int ix = int(gl_FragCoord.x) & 3, iy = int(gl_FragCoord.y) & 3;
            float b = (bayer[iy * 4 + ix] + 0.5) / 16.0;
            float keep = smoothstep(0.45, 1.0, r) * 0.9;
            if (b > keep) discard;
        }
    }
    vec3 N = normalize(vN);
    if (!gl_FrontFacing && uMode != 1) N = -N;
    vec3 alb, glow = vec3(0.0);
    float em = vC.a, gloss = 0.06, ao = 1.0;
    if (uMode == 1) {
        // the ground: up to four grounds meet around each spot; each is drawn from its own material and they blend
        // by how high their surfaces stand (grass blades over the dirt, stones over the moss)
        vec2 xz = vW.xz, gx = dpx.xz, gy = dpy.xz;
        vec2 p = xz * uTypeXf.xy + uTypeXf.zw;
        ivec2 mx = textureSize(uTypes, 0) - 1;
        ao = texture(uTypes, p / vec2(mx + 1)).g;
        p -= 0.5;
        vec2 fp = floor(p), f = p - fp;
        ivec2 i0 = ivec2(fp);
        int c[4]; float w[4];
        c[0] = gcode(i0, mx); c[1] = gcode(i0 + ivec2(1, 0), mx); c[2] = gcode(i0 + ivec2(0, 1), mx); c[3] = gcode(i0 + ivec2(1, 1), mx);
        w[0] = (1.0 - f.x) * (1.0 - f.y); w[1] = f.x * (1.0 - f.y); w[2] = (1.0 - f.x) * f.y; w[3] = f.x * f.y;
        for (int i = 1; i < 4; i++)
            for (int j = 0; j < i; j++)
                if (c[i] == c[j] && w[j] >= 0.0) { w[j] += w[i]; w[i] = -1.0; break; }
        vec4 A[4]; vec3 Nn[4]; float G[4]; float hb[4];
        float carpet = 0.0, ritual = 0.0, top = -9.0;
        int ci = -1;
        for (int i = 0; i < 4; i++) {
            hb[i] = -9.0;
            if (w[i] <= 0.0) continue;
            gsample(c[i], xz, gx, gy, A[i], Nn[i], G[i]);
            if ((c[i] & 16) != 0) ritual += w[i];
            if ((c[i] & 15) == 11) { carpet += w[i]; ci = i; continue; }
            hb[i] = A[i].a * 0.7 + w[i];
            top = max(top, hb[i]);
        }
        vec3 cs = vec3(0.0), ns = vec3(0.0);
        float gs = 0.0, sum = 0.0;
        for (int i = 0; i < 4; i++) {
            if (hb[i] < -8.0) continue;
            float b = max(hb[i] - top + 0.2, 0.0);
            cs += A[i].rgb * uGTint[c[i] & 15] * b; ns += Nn[i] * b; gs += G[i] * b; sum += b;
        }
        if (sum > 0.0) { cs /= sum; ns /= sum; gs /= sum; }
        if (ci >= 0) {
            // a runner laid over the flags: a clean edge, a dark band and a thread of gold just inside it
            vec3 cc = A[ci].rgb * uGTint[11];
            float dW = (carpet - 0.5) / uTypeXf.x;
            float aa = max(length(gx), length(gy)) * 1.2 + 0.02;
            float trim = 1.0 - smoothstep(0.2, 0.2 + aa, abs(dW - 1.0));
            cc = mix(cc, cc * 0.5, 1.0 - smoothstep(2.0, 2.0 + aa, dW));
            cc = mix(cc, vec3(0.46, 0.34, 0.15), trim);
            float cov = sum > 0.0 ? smoothstep(-aa * 0.5, aa * 0.5, dW) : 1.0;
            cs = mix(cs, cc, cov); ns = mix(ns, Nn[ci], cov); gs = mix(gs, mix(G[ci], 0.4, trim), cov);
        }
        // broad light and dark across the land
        float mac = textureLod(uNoise, xz / 1300.0, 0.0).g * 0.6 + textureLod(uNoise, xz / 340.0, 0.0).b * 0.4;
        alb = cs * vC.rgb * (0.82 + 0.36 * mac);
        if (ritual > 0.0) {
            // the boss floor: faint sigil lines glowing through the flags
            float rn = textureLod(uNoise, xz / 160.0, 0.0).b;
            float fw = max(length(gx), length(gy)) * 0.006 + 0.002;
            float line = 1.0 - smoothstep(0.012, 0.012 + fw, abs(rn - 0.5));
            glow = vec3(0.30, 0.20, 0.62) * (line * ritual * (0.75 + 0.25 * sin(uTime * 1.4 + rn * 30.0)));
        }
        N = bump(N, normalize(ns), dpx, dpy, gx, gy);
        gloss = gs;
    }
    else if (vL > 254.5) {
        // a sprite card: the pixel art itself, cut out by its alpha; alpha 254 marks pixels that glow
        vec4 t = textureGrad(uAtlas, vUv, dux, duy);
        if (t.a < 0.5) discard;
        alb = vC.rgb * t.rgb;
        if (t.a < 0.999) em = max(em, 0.85);
    }
    else {
        alb = vC.rgb;
        if (vL > 0.5) {
            vec3 tc = vec3(vUv, vL);
            vec4 a = textureGrad(uMat, tc, dux, duy);
            vec4 q = textureGrad(uMatN, tc, dux, duy);
            alb *= a.rgb * uMatK[int(vL + 0.5)];
            vec3 tn = q.xyz * 2.0 - 1.0;
            float tl = length(tn);
            gloss = q.w * min(1.0, tl * tl * 1.1);          // where the relief is finer than a pixel, the shine spreads out
            N = bump(N, tn / max(tl, 0.0001), dpx, dpy, dux, duy);
        }
    }
    alb = mix(alb, uTint.rgb, uTint.a);
    float sh = shadowAt(vS);
    vec3 V = normalize(uCam - vW), spec;
    vec3 lit = min(lightSpec(vW, N, V, sh, gloss, spec), vec3(2.3)) * ao;
    // polished metal shows what it reflects (sky above, ground below) tinted by the metal itself, and its highlights take its colour;
    // anything else glossy, wet ground included, keeps a faint Fresnel sheen of the sky
    float metal = uMode == 1 ? 0.0 : smoothstep(0.55, 0.75, gloss);
    vec3 Rv = reflect(-V, N);
    vec3 env = mix(uAmbGround * 0.55, uAmbSky * 1.3 + uSunCol * (0.25 * sh), smoothstep(-0.15, 0.45, Rv.y));
    float fres = 0.04 + 0.96 * pow(1.0 - max(dot(N, V), 0.0), 5.0);
    vec3 refl = env * mix(vec3(fres * gloss * gloss), alb * 1.1, metal);
    vec3 col = alb * lit * (1.0 - 0.7 * metal) + (refl + min(spec, vec3(3.0)) * mix(vec3(1.0), alb * 1.4 + 0.08, metal)) * ao;
    col = mix(col, alb * 1.9, em) + glow;
    if (uMode == 1 && vW.y < uWaterY) col *= mix(0.5, 1.0, clamp((vW.y - uWaterY + 5.0) / 5.0, 0.0, 1.0));
    o = vec4(atmosphere(col, vW, lit), 1.0);
}
";

        public const string ShadowVs = Head + Attribs + Sway +
@"uniform mat4 uVP;
out vec2 vUv; flat out float vL;
void main() { vUv = aUv; vL = aExt.x; gl_Position = uVP * vec4(sway(aPos, aExt.y / 255.0), 1.0); }
";

        public const string ShadowFs = Head +
@"in vec2 vUv; flat in float vL;
uniform sampler2D uAtlas;
out vec4 o;
void main() {
    if (vL > 254.5 && texture(uAtlas, vUv).a < 0.5) discard;
    o = vec4(1.0);
}
";

        // water
        public const string WaterVs = Head + Attribs +
@"uniform mat4 uVP; uniform mat4 uShadowVP; uniform float uTime;
out vec3 vW; out vec4 vS; out float vEdge;
void main() {
    vec3 p = aPos;
    p.y += sin(uTime * 1.3 + p.x * 0.05 + p.z * 0.07) * 0.35;
    vW = p; vEdge = aCol.a;
    vS = uShadowVP * vec4(p, 1.0);
    gl_Position = uVP * vec4(p, 1.0);
}
";

        public const string WaterFs = Head +
@"in vec3 vW; in vec4 vS; in float vEdge;
out vec4 o;
uniform float uTime; uniform vec3 uDeep; uniform vec3 uSky;
" + Lighting +
@"void main() {
    vec2 p = vW.xz;
    float t = uTime;
    float a1 = p.x * 0.11 + p.y * 0.05 + t * 1.25, a2 = p.y * 0.13 - p.x * 0.04 - t * 0.9, a3 = (p.x + p.y) * 0.21 + t * 1.9;
    float dx = cos(a1) * 0.11 - sin(a2) * 0.04 * 0.0 + cos(a3) * 0.08;
    float dz = cos(a2) * 0.13 + cos(a3) * 0.08 + sin(a1) * 0.02;
    float n = texture(uNoise, p / 96.0 + vec2(t * 0.02, -t * 0.013)).r - 0.5;
    vec3 N = normalize(vec3(dx + n * 0.25, 1.0, dz - n * 0.2));
    vec3 V = normalize(uCam - vW);
    float sh = shadowAt(vS);
    vec3 lit = lightAt(vW, vec3(0.0, 1.0, 0.0), sh);
    float fres = 0.18 + 0.62 * pow(1.0 - max(dot(N, V), 0.0), 3.0);
    vec3 col = mix(uDeep * lit, uSky * lit, fres);
    vec3 spec = vec3(0.0);
    vec3 S = normalize(-uSunDir);
    spec += uSunCol * pow(max(dot(N, normalize(S + V)), 0.0), 90.0) * 1.6 * sh;
    for (int i = 0; i < uNL; i++) {
        vec3 L = uLP[i].xyz - vW;
        float r = uLP[i].w * 1.25;
        float d = length(L);
        if (d >= r) continue;
        float att = 1.0 - d / r;
        spec += uLC[i].rgb * pow(max(dot(N, normalize(L / d + V)), 0.0), 70.0) * att * 2.2;
    }
    float glint = smoothstep(0.92, 1.0, texture(uNoise, p / 40.0 + vec2(t * 0.05, t * 0.03)).r) * 0.25;
    col += spec + glint * lit;
    float alpha = clamp(0.62 + fres * 0.3, 0.0, 0.95) * vEdge;
    o = vec4(atmosphere(col, vW, lit), alpha);
}
";

        // effects: particles, ribbons, decals, sprite cards
        public const string FxVs = Head + Attribs +
@"uniform mat4 uVP;
out vec4 vC; out vec2 vUv; flat out float vShape; out vec3 vW;
void main() {
    vC = aCol; vUv = aUv; vShape = aExt.x; vW = aPos;
    gl_Position = uVP * vec4(aPos, 1.0);
}
";

        public const string FxFs = Head +
@"in vec4 vC; in vec2 vUv; flat in float vShape; in vec3 vW;
out vec4 o;
uniform sampler2D uAtlas; uniform float uBoost; uniform vec3 uCam; uniform vec2 uFogDist; uniform float uAdd;
uniform vec3 uLitAmb;
void main() {
    vec4 c = vC;
    float a = c.a;
    if (vShape < 0.5) { float r = length(vUv); a *= clamp(1.0 - r, 0.0, 1.0); a *= a; }
    else if (vShape < 1.5) { }
    else if (vShape < 2.5) { a *= 1.0 - vUv.y * vUv.y; }
    else if (vShape < 3.5) {
        vec4 t = texture(uAtlas, vUv);
        if (t.a < 0.5) discard;
        c.rgb *= t.rgb * (t.a < 0.999 ? 1.9 : 1.0);
    }
    else if (vShape < 4.5) { float r = length(vUv); a *= smoothstep(1.0, 0.35, r); }
    else { vec4 t = texture(uAtlas, vUv); if (t.a < 0.5) discard; c.rgb *= t.rgb * uLitAmb; }
    float dist = distance(vW, uCam);
    float far = clamp((dist - uFogDist.x) / (uFogDist.y - uFogDist.x), 0.0, 1.0);
    a *= 1.0 - far * 0.85;
    if (uAdd > 0.5) o = vec4(c.rgb * a * uBoost, a);
    else o = vec4(c.rgb * uBoost, a);
}
";

        // sky (title vistas look toward the horizon)
        public const string FullVs = Head +
@"layout(location=0) in vec3 aPos;
out vec2 vUv;
void main() { vUv = aPos.xy * 0.5 + 0.5; gl_Position = vec4(aPos.xy, 0.0, 1.0); }
";

        public const string SkyFs = Head +
@"in vec2 vUv; out vec4 o;
uniform mat4 uInvVP; uniform vec3 uHorizon; uniform vec3 uZenith; uniform vec3 uMoonDir; uniform float uTime; uniform float uStars;
float hash(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
void main() {
    vec4 a = uInvVP * vec4(vUv * 2.0 - 1.0, -1.0, 1.0), b = uInvVP * vec4(vUv * 2.0 - 1.0, 1.0, 1.0);
    vec3 d = normalize(b.xyz / b.w - a.xyz / a.w);
    float e = clamp(d.y, -0.2, 1.0);
    vec3 col = mix(uHorizon, uZenith, pow(clamp(e * 1.6, 0.0, 1.0), 0.65));
    vec3 g = floor(d * 420.0);
    float s = hash(g);
    if (s > 0.9975 && d.y > 0.04) col += vec3(0.75, 0.78, 0.95) * (s - 0.9975) * 400.0 * uStars * (0.6 + 0.4 * sin(uTime * 2.0 + s * 90.0));
    float m = dot(d, normalize(uMoonDir));
    col += vec3(0.95, 0.93, 0.85) * smoothstep(0.9993, 0.9996, m) * 1.6;
    col += vec3(0.45, 0.5, 0.75) * pow(max(m, 0.0), 220.0) * 0.6;
    o = vec4(col, 1.0);
}
";

        // post: bloom, grade, vignette
        public const string BrightFs = Head +
@"in vec2 vUv; out vec4 o; uniform sampler2D uSrc; uniform vec2 uTexel;
void main() {
    vec3 c = vec3(0.0);
    c += texture(uSrc, vUv + uTexel * vec2(-0.5, -0.5)).rgb;
    c += texture(uSrc, vUv + uTexel * vec2(0.5, -0.5)).rgb;
    c += texture(uSrc, vUv + uTexel * vec2(-0.5, 0.5)).rgb;
    c += texture(uSrc, vUv + uTexel * vec2(0.5, 0.5)).rgb;
    c *= 0.25;
    float l = max(c.r, max(c.g, c.b));
    o = vec4(c * smoothstep(0.92, 1.9, l), 1.0);
}
";

        public const string BlurFs = Head +
@"in vec2 vUv; out vec4 o; uniform sampler2D uSrc; uniform vec2 uDir;
void main() {
    vec3 c = texture(uSrc, vUv).rgb * 0.227;
    c += (texture(uSrc, vUv + uDir * 1.385).rgb + texture(uSrc, vUv - uDir * 1.385).rgb) * 0.316;
    c += (texture(uSrc, vUv + uDir * 3.231).rgb + texture(uSrc, vUv - uDir * 3.231).rgb) * 0.070;
    o = vec4(c, 1.0);
}
";

        public const string FinalFs = Head +
@"in vec2 vUv; out vec4 o;
uniform sampler2D uSrc; uniform sampler2D uBloom; uniform sampler2D uBloom2; uniform float uBloomK; uniform float uVig; uniform vec2 uAspect;
uniform vec3 uGrade; uniform float uSat;
vec3 shoulder(vec3 c) {
    vec3 k = vec3(0.78);
    return mix(c, k + (1.0 - k) * (1.0 - exp(-(c - k) / (1.0 - k))), step(k, c));
}
void main() {
    vec3 c = texture(uSrc, vUv).rgb;
    c += (texture(uBloom, vUv).rgb * 0.75 + texture(uBloom2, vUv).rgb * 0.55) * uBloomK;
    float l = dot(c, vec3(0.299, 0.587, 0.114));
    c = mix(vec3(l), c, uSat) * uGrade;
    vec2 d = (vUv - 0.5) * 2.0;
    float q = d.x * d.x * 0.85 + d.y * d.y * 0.95;
    float v = 1.0 - smoothstep(0.0, 1.0, (q - 0.35) / 1.25) * 0.62 * uVig;
    c *= v;
    o = vec4(clamp(shoulder(max(c, vec3(0.0))), 0.0, 1.0), 1.0);
}
";

        // the 640x360 interface laid over the 3D frame
        public const string UiVs = Head +
@"layout(location=0) in vec3 aPos;
uniform vec4 uRect;
out vec2 vUv;
void main() {
    vUv = vec2(aPos.x * 0.5 + 0.5, 0.5 - aPos.y * 0.5);
    vec2 p = uRect.xy + (aPos.xy * 0.5 + 0.5) * uRect.zw;
    gl_Position = vec4(p, 0.0, 1.0);
}
";

        public const string UiFs = Head +
@"in vec2 vUv; out vec4 o; uniform sampler2D uUi;
void main() { o = texture(uUi, vUv); }
";

        // the interface drawn as quads (see UiRec)
        public const string UiQuadVs = Head +
@"layout(location=0) in vec2 inP;
layout(location=1) in vec2 inT;
layout(location=2) in vec4 inC;
layout(location=3) in float inM;
layout(location=4) in vec4 inA;
layout(location=5) in vec4 inB;
uniform vec4 uRect;      // interface area in clip space: left, top, width, height
uniform vec2 uSize;      // interface size in its own pixels
out vec2 vP; out vec2 vT; out vec4 vC;
flat out int vM; flat out vec4 vA; flat out vec4 vB;
void main() {
    vP = inP; vT = inT; vC = inC; vM = int(inM + 0.5); vA = inA; vB = inB;
    vec2 t = inP / uSize;
    gl_Position = vec4(uRect.x + t.x * uRect.z, uRect.y - t.y * uRect.w, 0.0, 1.0);
}
";

        public const string UiQuadFs = Head +
@"in vec2 vP; in vec2 vT; in vec4 vC;
flat in int vM; flat in vec4 vA; flat in vec4 vB;
uniform sampler2D uAtlas; uniform sampler2D uImg;
out vec4 o;
void main() {
    if (vM == 0) { o = texture(uAtlas, vT) * vC; return; }
    if (vM == 1) {
        vec4 t = texture(uAtlas, vT);
        o = vec4(mix(t.rgb, vA.rgb * t.a, vA.a), t.a) * vC;
        return;
    }
    if (vM == 8) { o = texture(uImg, vT) * vC; return; }
    if (vM == 9) {
        vec4 t = texture(uImg, vT);
        float m = smoothstep(0.3, 0.7, t.a);
        o = vec4(t.rgb * m, m) * vC;
        return;
    }
    vec2 p = vP;
    float cov = 0.0;
    if (vM == 2) {                       // line: ends A.xy A.zw, half width B.x
        vec2 d = vA.zw - vA.xy, q = p - vA.xy;
        float l2 = dot(d, d);
        float t = l2 > 1e-6 ? clamp(dot(q, d) / l2, 0.0, 1.0) : 0.0;
        cov = clamp(vB.x + 0.5 - length(q - d * t), 0.0, 1.0);
    } else if (vM == 3) {                // ellipse: centre A.xy, radii A.zw (row spans, as drawn in software)
        float dy = (p.y - vA.y) / vA.w, t = 1.0 - dy * dy;
        if (t <= 0.0) discard;
        float hw = vA.z * sqrt(t);
        cov = clamp(min(p.x + 0.5, vA.x + hw) - max(p.x - 0.5, vA.x - hw), 0.0, 1.0);
    } else if (vM == 4) {                // ring on the ground: centre A.xy, radii A.zw, squash B.x
        vec2 d = vec2(p.x - vA.x, (p.y - vA.y) * vB.x);
        float q = length(d);
        cov = min(1.0, vA.w + 0.5 - q);
        if (vA.z > 0.0) cov = min(cov, q - vA.z + 0.5);
    } else if (vM == 5) {                // strip: ends A.xy A.zw, half width B.x, squash B.y
        vec2 d = vec2(vA.z - vA.x, (vA.w - vA.y) * vB.y), q = vec2(p.x - vA.x, (p.y - vA.y) * vB.y);
        float t = dot(q, d) / max(dot(d, d), 1e-6);
        if (t < 0.0 || t > 1.0) discard;
        cov = min(1.0, vB.x + 0.5 - length(q - d * t));
    } else if (vM == 6) {                // sector: centre A.xy, radii A.zw, direction B.xy, cos of half the arc B.z, squash B.w
        vec2 d = vec2(p.x - vA.x, (p.y - vA.y) * vB.w);
        float q = length(d);
        cov = min(1.0, min(vA.w + 0.5 - q, vA.z > 0.0 ? q - vA.z + 0.5 : 1.0));
        if (q > 0.001 && dot(d, vB.xy) / q < vB.z) discard;
    } else if (vM == 7) {                // soft glow: centre A.xy, radius A.z
        vec2 d = p - vA.xy;
        float t = 1.0 - dot(d, d) / (vA.z * vA.z);
        if (t <= 0.0) discard;
        cov = t * t;
    }
    if (cov <= 0.0) discard;
    o = vC * min(cov, 1.0);
}
";

        public const string BlitFs = Head +
@"in vec2 vUv; out vec4 o; uniform sampler2D uSrc;
void main() { o = vec4(texture(uSrc, vUv).rgb, 1.0); }
";
    }
}
