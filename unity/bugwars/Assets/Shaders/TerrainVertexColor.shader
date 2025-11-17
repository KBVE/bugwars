Shader "BugWars/TerrainVertexColor"
{
    Properties
    {
        _ColorMultiplier ("Color Multiplier", Range(0.5, 2.0)) = 1.0
        _Smoothness      ("Smoothness", Range(0, 1))           = 0.2
        _AmbientStrength ("Ambient Strength", Range(0, 1))     = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"    = "Opaque"
            "RenderPipeline"= "UniversalPipeline"
            "Queue"         = "Geometry"
        }

        // =========================================================
        // Forward Lit Pass
        // =========================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP lighting variants
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color      : COLOR;
                float4 shadowCoord: TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _ColorMultiplier;
                float _Smoothness;
                float _AmbientStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS  = vertexInput.positionCS;
                output.positionWS  = vertexInput.positionWS;
                output.normalWS    = normalInput.normalWS;
                output.color       = input.color * _ColorMultiplier;
                output.shadowCoord = GetShadowCoord(vertexInput);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Simple unlit approach - just use vertex color directly
                // This eliminates ALL lighting-based seams between chunks
                half3 baseColor = input.color.rgb;

                // Apply a constant brightness multiplier for visibility
                half3 finalColor = baseColor * _ColorMultiplier;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // =========================================================
        // ShadowCaster Pass
        // (Fixed: use _MainLightPosition instead of custom _LightDirection)
        // =========================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                // Get main light direction (URP style)
                float3 lightDirWS;
                // Directional light: _MainLightPosition.w == 0, xyz is direction
                if (_MainLightPosition.w == 0.0)
                {
                    lightDirWS = normalize(_MainLightPosition.xyz);
                }
                else
                {
                    // Point/spot: xyz is position
                    lightDirWS = normalize(_MainLightPosition.xyz - positionWS);
                }

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirWS)
                );

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // =========================================================
        // DepthOnly Pass (for depth prepass & SSAO, etc.)
        // =========================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
