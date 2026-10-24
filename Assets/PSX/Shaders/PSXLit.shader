// PS1-style lit surface: vertex snapping, affine texture warp, per-vertex (Gouraud) lighting, per-vertex fog.
// Needs the Forward renderer (not Forward+): per-object light lists are read in the vertex shader.
Shader "PSX/Lit"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Tiling ("Tiling", Vector) = (1,1,0,0)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _SnapResolution ("Vertex snap grid (rows)", Float) = 120
        _Affine ("Affine warp", Range(0,1)) = 0.5
        _Ambient ("Ambient boost", Float) = 1
        // Pulls flat stuck-on things (puddles, grates, paper, signs) a hair toward the camera, so the vertex snapping
        // can't make them fight the surface they lie on.
        _DepthOffset ("Depth offset (decals)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Offset [_DepthOffset], [_DepthOffset]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _Tiling;
                half4 _EmissionColor;
                float _SnapResolution;
                float _Affine;
                float _Ambient;
                float _DepthOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uvw : TEXCOORD0;
                half3 light : TEXCOORD1;
                half fog : TEXCOORD2;
                half4 color : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                float4 cs = TransformWorldToHClip(posWS);

                // Snap to a coarse screen grid: the PS1 had no sub-pixel precision.
                float2 grid = float2(_SnapResolution * _ScreenParams.x / _ScreenParams.y, _SnapResolution);
                cs.xy = round(cs.xy / cs.w * grid) / grid * cs.w;
                o.positionCS = cs;

                // Interpolating uv*w and dividing back undoes perspective correction (affine mapping).
                float w = lerp(1.0, cs.w, _Affine);
                o.uvw = float3(v.uv * _Tiling.xy * w, w);

                half3 light = SampleSH(nWS) * _Ambient;
                Light mainLight = GetMainLight();
                light += mainLight.color * saturate(dot(nWS, mainLight.direction));

                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, posWS);
                    light += l.color * l.distanceAttenuation * saturate(dot(nWS, l.direction) * 0.8 + 0.2);
                }
                o.light = light;
                o.fog = ComputeFogFactor(cs.z);
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uvw.xy / i.uvw.z;
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 col = tex.rgb * _BaseColor.rgb * i.color.rgb * i.light + _EmissionColor.rgb;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Offset [_DepthOffset], [_DepthOffset]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _Tiling;
                half4 _EmissionColor;
                float _SnapResolution;
                float _Affine;
                float _Ambient;
                float _DepthOffset;
            CBUFFER_END

            // Same snap as the lit pass, or depth and colour disagree.
            float4 vert (float4 positionOS : POSITION) : SV_POSITION
            {
                float4 cs = TransformObjectToHClip(positionOS.xyz);
                float2 grid = float2(_SnapResolution * _ScreenParams.x / _ScreenParams.y, _SnapResolution);
                cs.xy = round(cs.xy / cs.w * grid) / grid * cs.w;
                return cs;
            }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
