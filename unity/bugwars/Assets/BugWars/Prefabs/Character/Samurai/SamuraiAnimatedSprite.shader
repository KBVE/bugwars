Shader "BugWars/SamuraiAnimatedSprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Sprite Sheet", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)

        // Frame UV coordinates (set from script)
        _FrameUVMin("Frame UV Min", Vector) = (0,0,0,0)
        _FrameUVMax("Frame UV Max", Vector) = (1,1,0,0)

        // Billboard & Sprite Flipping (set from script via MaterialPropertyBlock)
        _FlipX("Flip Horizontal", Float) = 0  // 0 = normal, 1 = flipped
        _FlipY("Flip Vertical", Float) = 0     // 0 = normal, 1 = flipped

        // Billboard Effects
        _BillboardStretch("Billboard Stretch", Vector) = (1,1,1,0) // x=width, y=height, z=depth stretch
        _PreserveSize("Preserve Size with Distance", Range(0,1)) = 0 // 0=perspective, 1=constant size

        // Additional properties
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        // Lighting
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1.0

        // Rendering
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10 // OneMinusSrcAlpha
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0 // Off for billboards
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SpriteForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 color        : COLOR;
                float fogFactor     : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _FrameUVMin;
                float4 _FrameUVMax;
                float _FlipX;
                float _FlipY;
                float4 _BillboardStretch;
                float _PreserveSize;
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // Apply billboard stretch to vertex position
                float3 vertexOS = input.positionOS.xyz;
                vertexOS.x *= _BillboardStretch.x;
                vertexOS.y *= _BillboardStretch.y;
                vertexOS.z *= _BillboardStretch.z;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(vertexOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                // Optional: Preserve sprite size with distance
                float3 positionWS = vertexInput.positionWS;
                if (_PreserveSize > 0.0)
                {
                    float3 toCamera = _WorldSpaceCameraPos - TransformObjectToWorld(float3(0,0,0));
                    float distance = length(toCamera);
                    float scaleFactor = lerp(1.0, distance * 0.1, _PreserveSize);

                    float3 localOffset = input.positionOS.xyz * scaleFactor;
                    positionWS = TransformObjectToWorld(float3(0,0,0)) + localOffset;
                    vertexInput.positionCS = TransformWorldToHClip(positionWS);
                }

                output.positionCS = vertexInput.positionCS;
                output.positionWS = positionWS;
                output.normalWS = normalInput.normalWS;

                // Apply sprite flipping to UV
                float2 uv = input.uv;

                // Flip X (horizontal)
                if (_FlipX > 0.5)
                {
                    uv.x = 1.0 - uv.x;
                }

                // Flip Y (vertical)
                if (_FlipY > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                // Remap UV from (0,1) to frame UV bounds
                output.uv = lerp(_FrameUVMin.xy, _FrameUVMax.xy, uv);

                output.color = input.color;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample the sprite sheet at the remapped UV
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Apply tint and vertex color
                half4 color = texColor * _BaseColor * input.color;

                // Alpha cutoff
                clip(color.a - _Cutoff);

                // Lighting
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = normalize(input.normalWS);
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                // Simple lit calculation
                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(lightingInput.shadowCoord, lightingInput.positionWS, shadowMask);

                half3 lighting = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                lighting = max(lighting, half3(0.3, 0.3, 0.3)); // Ambient minimum

                color.rgb *= lighting;

                // Fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _FrameUVMin;
                float4 _FrameUVMax;
                float _FlipX;
                float _FlipY;
                float4 _BillboardStretch;
                float _Cutoff;
            CBUFFER_END

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;

                // Apply billboard stretch
                float3 vertexOS = input.positionOS.xyz;
                vertexOS.x *= _BillboardStretch.x;
                vertexOS.y *= _BillboardStretch.y;
                vertexOS.z *= _BillboardStretch.z;

                float3 positionWS = TransformObjectToWorld(vertexOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                // Apply sprite flipping
                float2 uv = input.uv;
                if (_FlipX > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;

                output.uv = lerp(_FrameUVMin.xy, _FrameUVMax.xy, uv);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Sprites/Default"
}
