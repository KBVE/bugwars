Shader "BugWars/BlankPlayerAnimatedSprite_URP3D_Billboard"
{
    Properties
    {
        _BaseMap("Sprite Sheet", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _FrameUVMin("Frame UV Min", Vector) = (0,0,0,0)
        _FrameUVMax("Frame UV Max", Vector) = (1,1,0,0)
        _FlipX("Flip Horizontal", Float) = 0
        _FlipY("Flip Vertical", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.1
        _BillboardMode("Billboard Mode (0=None,1=Cyl,2=Sph)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "SpriteUnlitForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float  fogCoord   : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float4 _FrameUVMin;
                float4 _FrameUVMax;
                float  _FlipX;
                float  _FlipY;
                half   _Cutoff;
                float  _BillboardMode;
            CBUFFER_END

            // Build billboarded world position from sprite local quad offsets
            float3 BillboardPositionWS(float2 localXY, float3 centerWS)
            {
                // Camera basis in world space
                float3 camRightWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(1,0,0)));
                float3 camUpWS    = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(0,1,0)));

                if (_BillboardMode >= 1.5) // spherical
                {
                    return centerWS + camRightWS * localXY.x + camUpWS * localXY.y;
                }
                else if (_BillboardMode >= 0.5) // cylindrical (Y-locked)
                {
                    float3 toCam = _WorldSpaceCameraPos - centerWS;
                    toCam.y = 0;
                    toCam = normalize(toCam + 1e-6);
                    float3 upWS = float3(0,1,0);
                    float3 rightWS = normalize(cross(upWS, toCam));
                    return centerWS + rightWS * localXY.x + upWS * localXY.y;
                }
                else // no billboard
                {
                    float3 posOS = float3(localXY.x, localXY.y, 0);
                    return TransformObjectToWorld(posOS);
                }
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 centerWS = TransformObjectToWorld(float3(0,0,0));
                float2 quadLocal = IN.positionOS.xy;

                float3 posWS = BillboardPositionWS(quadLocal, centerWS);
                OUT.positionCS = TransformWorldToHClip(posWS);

                // Flip UV and remap to frame window
                float2 uv = IN.uv;
                if (_FlipX > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                OUT.uv = lerp(_FrameUVMin.xy, _FrameUVMax.xy, uv);

                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 col = tex * _BaseColor * IN.color;
                clip(col.a - _Cutoff);

                col.rgb = MixFog(col.rgb, IN.fogCoord);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
