// Final PSX screen pass: shows the low-res frame with colour depth reduced and 4x4 Bayer dithering.
// Used by PSXScreen on a full-screen RawImage, so it is written as a UI shader.
// Menus and the story play on an old TV set drawn round the picture (see _PsxCrt); the set can be switched on and
// off and shows snow between channels.
Shader "PSX/Screen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Frame", 2D) = "black" {}
        _ColorLevels ("Colour levels per channel", Float) = 32
        _DitherStrength ("Dither strength", Range(0,1)) = 0.6
        _Contrast ("Contrast", Float) = 1.05
        _Saturation ("Saturation", Float) = 0.92
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _ColorLevels;
            float _DitherStrength;
            float _Contrast;
            float _Saturation;
            // Globals, so the game can drive them without touching the material asset:
            float _PsxCrt;      // 0 = plain PSX frame, 1 = shown on the TV set (the set zooms in/out in between)
            float _PsxTvOff;    // 0 = tube on, 1 = switched off (the picture squeezes to a line, then a dot)
            float _PsxStatic;   // 0..1 snow over the picture
            float4 _PsxWindow;  // window size in pixels
            float4 _PsxKnobs;   // channel knob angle, volume knob angle, control under the mouse (1-2 knobs, 3-5 buttons), button held down

            // Where the glass sits in the window and how much it bulges. Mirrored in PSXScreen.WindowToFrame.
            static const float2 TvCenter = float2(0.5, 0.548);
            static const float2 TvHalf = float2(0.405, 0.392);
            static const float TvBulge = 0.085;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float RoundBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Knob seen from the front: ribbed rim, domed cap, a white pointer, a shadow below it.
            float3 Knob(float3 col, float2 v, float2 c, float r, float angle, float hover)
            {
                float2 d = v - c;
                float len = length(d);
                if (len < r * 1.3 && d.y < 0.0) col *= 0.55 + 0.45 * saturate((len - r) / (r * 0.3));
                if (len > r) return col;
                float a = atan2(d.y, d.x);
                float ribs = step(0.5, frac(a * 18.0 / 6.2831853));
                float3 k = float3(0.11, 0.1, 0.095) * (0.75 + 0.5 * saturate(d.y / r + 0.5));
                if (len > r * 0.78) k *= 0.75 + 0.35 * ribs;
                else k += 0.05 * (1.0 - len / r);
                float2 dir = float2(cos(angle), sin(angle));
                float along = dot(d, dir), across = abs(d.x * dir.y - d.y * dir.x);
                if (along > r * 0.2 && along < r * 0.7 && across < r * 0.09) k = float3(0.7, 0.68, 0.62);
                return k * (1.0 + hover * 0.6);
            }

            // The TV cabinet round the glass. w: window point in window heights from the centre. sd: distance from
            // the glass edge in glass half-heights (0 at the edge). p: point on the glass (for the recess shading).
            float3 Cabinet(float2 uv, float2 w, float aspectW, float sd, float2 p, float tvOn)
            {
                float2 px = floor(uv * _MainTex_TexelSize.zw);
                float n = Hash(px) - 0.5;

                // Moulded dark plastic, lit softly from above, with a fine texture.
                float3 col = float3(0.085, 0.073, 0.064) * (0.78 + 0.4 * (w.y + 0.5)) + n * 0.012;

                // Recessed lip round the glass: shadowed under the top edge, catching light along the bottom.
                float facing = -p.y / max(length(p), 1e-3);
                float diag = dot(normalize(p + 1e-4), normalize(float2(-1.0, 1.0)));
                if (sd < 0.075)
                {
                    float k = saturate(sd / 0.075);
                    float3 lip = float3(0.025, 0.022, 0.02) + max(facing, 0.0) * float3(0.09, 0.08, 0.07);
                    col = lerp(lip, col, smoothstep(0.55, 1.0, k));
                }
                // A thin chrome trim ring round the tube, bright where it faces the light (top left).
                else if (sd < 0.1)
                {
                    float k = (sd - 0.075) / 0.025;
                    float shine = 0.32 + 0.3 * diag + 0.25 * sin(k * 3.14159);
                    col = float3(0.55, 0.54, 0.5) * shine + n * 0.02;
                    if (k < 0.12 || k > 0.88) col *= 0.45;
                }

                // Ventilation slots along the top.
                if (abs(w.x) < 0.36 && w.y > 0.462 && w.y < 0.476 && frac(w.x / 0.024) < 0.55) col *= 0.35;

                // Control strip under the glass, laid out for 16:9 and squeezed for narrower windows.
                float2 v = float2(w.x / (aspectW / 1.7778), w.y);
                // Speaker grille.
                float grille = RoundBox(v - float2(-0.4, -0.42), float2(0.22, 0.036), 0.012);
                if (grille < 0.0) col = frac((v.y + 0.5) / 0.013) < 0.5 ? float3(0.018, 0.016, 0.015) : col * 1.35;
                // Brushed metal badge with three engraved bars.
                if (abs(v.x + 0.02) < 0.075 && abs(v.y + 0.42) < 0.014)
                {
                    col = float3(0.42, 0.41, 0.38) * (0.85 + 0.15 * sin(v.x * 600.0)) * (1.1 - (v.y + 0.42) * 8.0);
                    if (abs(v.y + 0.42) < 0.004 && frac((v.x + 0.02) / 0.035) < 0.6) col *= 0.45;
                }
                // Channel and volume knobs.
                col = Knob(col, v, float2(0.2, -0.42), 0.04, _PsxKnobs.x, step(abs(_PsxKnobs.z - 1.0), 0.1));
                col = Knob(col, v, float2(0.33, -0.42), 0.04, _PsxKnobs.y, step(abs(_PsxKnobs.z - 2.0), 0.1));
                // Push buttons.
                for (int b = 0; b < 3; b++)
                {
                    float2 bc = float2(0.55 + b * 0.055, -0.42);
                    float bd = RoundBox(v - bc, float2(0.02, 0.013), 0.004);
                    float held = step(abs(_PsxKnobs.w - (b + 3.0)), 0.1);
                    float lit = step(abs(_PsxKnobs.z - (b + 3.0)), 0.1);
                    // Held down it sinks into the panel: darker, no highlight on its top edge.
                    if (bd < 0.0) col = float3(0.15, 0.14, 0.13) * (1.0 + saturate((v.y - bc.y) / 0.013) * 0.4 * (1.0 - held)) * (1.0 - held * 0.4) * (1.0 + lit * 0.5);
                    else if (bd < 0.006 && v.y < bc.y) col *= 0.6 + held * 0.3;
                }
                // Power light: red while the set is on, a dull ember when off.
                float led = length(v - float2(0.45, -0.42));
                float3 ledCol = lerp(float3(0.12, 0.02, 0.02), float3(1.0, 0.16, 0.08), tvOn);
                if (led < 0.009) col = ledCol;
                col += ledCol * 0.35 * tvOn * exp(-led * 55.0);

                // Four little screws holding the front panel, near its corners.
                float2 sq = abs(w) - float2(aspectW * 0.5 - 0.085, 0.5 - 0.075);
                if (length(sq) < 0.009)
                {
                    col = float3(0.32, 0.31, 0.29) * (0.8 + 0.4 * saturate(-sq.y / 0.009 + 0.5));
                    if (abs(sq.x - sq.y) < 0.0025) col *= 0.35;
                }

                // The set fills the whole window: a wood veneer surround right out to the edges, with a bevelled inner
                // edge where it meets the plastic front.
                float band = -RoundBox(w, float2(aspectW * 0.5, 0.5), 0.004);
                const float woodWidth = 0.05;
                if (band < woodWidth)
                {
                    float grain = sin(w.y * 150.0 + sin(w.x * 7.0 + w.y * 3.0) * 4.0 + Hash(float2(px.y, 3.0)) * 0.8)
                                + 0.5 * sin(w.x * 60.0 + w.y * 20.0);
                    float3 wood = float3(0.25, 0.135, 0.065) * (0.8 + 0.1 * grain + n * 0.12);
                    // Rounded outer edge: dark at the very rim, a soft sheen, then the flat face.
                    float roll = saturate(band / 0.014);
                    wood *= lerp(0.35, 1.0, roll) + 0.35 * sin(roll * 3.14159) * saturate(0.6 + w.y);
                    // Lit from above: top brighter, bottom in shadow.
                    wood *= 0.75 + 0.5 * (w.y + 0.5);
                    // Inner bevel: a groove, then a lip catching the light before the plastic front starts.
                    if (band > woodWidth - 0.008 && band < woodWidth - 0.004) wood *= 0.4;
                    else if (band >= woodWidth - 0.004) wood *= 1.35;
                    col = wood;
                }
                else if (band < woodWidth + 0.01)
                {
                    col *= 0.55 + 0.45 * saturate((band - woodWidth) / 0.01);   // shadow the wood casts on the front
                }
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float crt = saturate(_PsxCrt);
                float aspectF = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float aspectW = _PsxWindow.y > 0.0 ? _PsxWindow.x / _PsxWindow.y : aspectF;

                // Window -> glass (-1..1 across the tube, bulged) -> frame. Zooming between the plain view and the set
                // is the glass growing to fill the window.
                float2 center = lerp(float2(0.5, 0.5), TvCenter, crt);
                float2 halfSize = lerp(float2(0.5, 0.5), TvHalf, crt);
                float2 p = (i.uv - center) / halfSize;
                p += p * (p.yx * p.yx) * TvBulge * crt;
                float glassAspect = halfSize.x / halfSize.y * aspectW;
                float2 crop = glassAspect < aspectF ? float2(glassAspect / aspectF, 1.0) : float2(1.0, aspectF / glassAspect);

                // Switching off: the picture squeezes into a bright line, the line into a dot, the dot fades.
                float off = saturate(_PsxTvOff);
                float2 squeeze = max(float2(saturate(1.0 - (off - 0.45) * 2.4), saturate(1.0 - off * 2.2)), float2(0.004, 0.012));
                float2 pp = p / squeeze;
                float lit = step(abs(pp.x), 1.0) * step(abs(pp.y), 1.0) * saturate((1.0 - off) * 5.0);
                float2 uv = pp * crop * 0.5 + 0.5;

                // A hint of colour fringing toward the edges.
                float2 shift = p * 0.0025 * crt;
                float3 c;
                c.r = tex2D(_MainTex, uv + shift).r;
                c.g = tex2D(_MainTex, uv).g;
                c.b = tex2D(_MainTex, uv - shift).b;

                c = (c - 0.5) * _Contrast + 0.5;
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, _Saturation);
                c *= lit * (1.0 + (1.0 - squeeze.y) * 2.5);

                // Snow: frame-sized grains, a fresh pattern every frame of the tube.
                float2 grain = floor(uv * _MainTex_TexelSize.zw * 0.5);
                float snow = Hash(grain + floor(_Time.y * 30.0) * float2(17.0, 31.0));
                c = lerp(c, snow.xxx * 0.85 * lit, saturate(_PsxStatic));

                if (crt > 0.001)
                {
                    // Scanlines on the frame's own rows, a slow roll bar, a soft vignette and a slight tube flicker.
                    float rows = _MainTex_TexelSize.w;
                    float scan = 0.5 + 0.5 * cos(uv.y * rows * 6.2831853);
                    c *= 1.0 - crt * 0.28 * scan;
                    c *= 1.0 - crt * 0.06 * smoothstep(0.0, 0.1, frac(uv.y * 0.6 - _Time.y * 0.08)) * (1.0 - smoothstep(0.1, 0.25, frac(uv.y * 0.6 - _Time.y * 0.08)));
                    c *= lerp(1.0, 1.15 - 0.55 * dot(p, p) * 0.5, crt);
                    c *= 1.0 - crt * 0.025 * sin(_Time.y * 47.0);

                    // Aperture grille on the window's own pixel columns.
                    float column = fmod(floor(i.uv.x * max(_PsxWindow.x, 1.0)), 3.0);
                    float3 mask = column < 1.0 ? float3(1.0, 0.82, 0.82) : column < 2.0 ? float3(0.82, 1.0, 0.82) : float3(0.82, 0.82, 1.0);
                    c *= lerp(float3(1, 1, 1), mask, crt * 0.5);

                    // The glass itself: dark green-grey when nothing is lit, with a soft glare top left.
                    c += float3(0.03, 0.036, 0.034) * crt;
                    float2 g = (p - float2(-0.5, 0.58)) * float2(0.9, 1.7);
                    c += crt * 0.05 * exp(-dot(g, g) * 3.0);

                    // Rounded tube corners; past them, the set. The set is drawn on the game's own low-res pixel grid,
                    // so it has the same chunky PS1 look as everything else.
                    float2 uvq = (floor(i.uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
                    float2 pq = (uvq - center) / halfSize;
                    pq += pq * (pq.yx * pq.yx) * TvBulge * crt;
                    float2 e = float2(glassAspect, 1.0);
                    float sd = RoundBox(pq * e, e, 0.16 * crt + 1e-4);
                    float inside = sd < 0.0 ? 1.0 : 0.0;
                    inside = lerp(1.0, inside, saturate(crt * 4.0));
                    float2 w = float2((uvq.x - 0.5) * aspectW, uvq.y - 0.5);
                    float3 cab = Cabinet(uvq, w, aspectW, sd, pq, 1.0 - off);
                    // The glass sits a little in shadow under the top of the bezel.
                    c *= 1.0 - 0.35 * crt * smoothstep(-0.12, 0.0, sd) * saturate(p.y + 0.2);
                    c = lerp(cab, c, inside);
                }

                // Dither on the low-res pixel grid, not the window's.
                int2 dp = int2(floor(i.uv * _MainTex_TexelSize.zw)) & 3;
                float threshold = (bayer[dp.y * 4 + dp.x] + 0.5) / 16.0 - 0.5;
                c += threshold * _DitherStrength / _ColorLevels;
                c = floor(saturate(c) * _ColorLevels + 0.5) / _ColorLevels;
                // The look (levels, dither, contrast) is tuned as display values. In a linear project the backbuffer
                // re-encodes whatever we return, which would wash it out; hand it the linear value instead.
                #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
                #endif
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
