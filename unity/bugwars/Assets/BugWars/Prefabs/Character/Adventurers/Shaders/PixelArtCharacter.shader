Shader "BugWars/PixelArtCharacter"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _TexturePixelation("Texture Pixelation", Range(1, 64)) = 16
        _OutlineStrength("Outline Strength", Range(0, 1)) = 0.3
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // Main Lit Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TexturePixelation;
                float _OutlineStrength;
                half4 _OutlineColor;
            CBUFFER_END

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.positionCS = vertexInput.positionCS;

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                // Pixelate UVs for retro texture look
                float2 pixelatedUV = floor(input.uv * _TexturePixelation) / _TexturePixelation;

                // Sample texture with pixelated UVs
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, pixelatedUV);
                albedo *= _BaseColor;

                // Get main light
                Light mainLight = GetMainLight();

                // Calculate simple diffuse lighting
                float3 normalWS = normalize(input.normalWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction));

                // Add strong ambient for good visibility
                float lighting = max(0.6, NdotL);

                // Quantize to 4 steps for toon/pixel art look
                lighting = floor(lighting * 4.0) / 4.0;

                // Apply lighting
                half3 color = albedo.rgb * lighting * mainLight.color;

                // Subtle edge detection using Fresnel effect (similar to Three.js normalEdgeStrength)
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));

                // Sharpen the edge detection (power controls thickness)
                fresnel = pow(fresnel, 3.0);

                // Apply outline only at edges based on strength parameter
                float edgeFactor = step(1.0 - _OutlineStrength, fresnel);

                // Blend outline color with lit color
                color = lerp(color, _OutlineColor.rgb, edgeFactor * _OutlineStrength);

                return half4(color, albedo.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
