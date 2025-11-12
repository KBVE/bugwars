Shader "BugWars/PixelArtCharacter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(Pixelation Settings)]
        _PixelSize ("Pixel Size", Range(0.001, 0.1)) = 0.02
        _TexturePixelation ("Texture Pixelation", Range(1, 64)) = 8

        [Header(Outline Settings)]
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.01
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)

        [Header(Vertex Quantization)]
        _VertexQuantization ("Vertex Quantization", Range(0, 1)) = 0.5
        _QuantizationSize ("Quantization Grid Size", Range(0.01, 1.0)) = 0.1

        [Header(Lighting)]
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
        _DiffuseStrength ("Diffuse Strength", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // First pass: Draw outline
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            float _OutlineWidth;
            float4 _OutlineColor;

            v2f vert(appdata v)
            {
                v2f o;

                // Expand vertices along normals for outline
                float3 norm = normalize(v.normal);
                float3 expanded = v.vertex.xyz + norm * _OutlineWidth;

                o.pos = UnityObjectToClipPos(float4(expanded, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // Second pass: Main pixel shader with quantization
        Pass
        {
            Name "BASE"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                SHADOW_COORDS(4)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _PixelSize;
            float _TexturePixelation;
            float _VertexQuantization;
            float _QuantizationSize;
            float _AmbientStrength;
            float _DiffuseStrength;

            // Quantize position to grid
            float3 QuantizePosition(float3 pos, float gridSize)
            {
                return floor(pos / gridSize) * gridSize;
            }

            v2f vert(appdata v)
            {
                v2f o;

                // Apply vertex quantization for blocky look
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 quantizedWorldPos = lerp(
                    worldPos,
                    QuantizePosition(worldPos, _QuantizationSize),
                    _VertexQuantization
                );

                // Transform back to object space
                float3 localPos = mul(unity_WorldToObject, float4(quantizedWorldPos, 1.0)).xyz;

                o.pos = UnityObjectToClipPos(float4(localPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = quantizedWorldPos;
                o.screenPos = ComputeScreenPos(o.pos);

                TRANSFER_SHADOW(o);

                return o;
            }

            // Pixelate UV coordinates
            float2 PixelateUV(float2 uv, float pixelation)
            {
                float2 pixelatedUV = floor(uv * pixelation) / pixelation;
                return pixelatedUV;
            }

            // Simple toon-style lighting with stepped shading
            float ToonShading(float3 normal, float3 lightDir, int steps)
            {
                float NdotL = dot(normal, lightDir);
                float lighting = max(0.0, NdotL);

                // Quantize lighting into discrete steps for pixel art look
                lighting = floor(lighting * steps) / steps;

                return lighting;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Pixelate UVs for texture
                float2 pixelatedUV = PixelateUV(i.uv, _TexturePixelation);

                // Sample texture with pixelated UVs
                fixed4 col = tex2D(_MainTex, pixelatedUV) * _Color;

                // Calculate lighting
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

                // Toon shading with 4 steps for retro look
                float diffuse = ToonShading(normal, lightDir, 4);

                // Apply lighting
                float3 ambient = _AmbientStrength * col.rgb;
                float3 diffuseColor = _DiffuseStrength * diffuse * col.rgb;

                col.rgb = ambient + diffuseColor;

                // Apply shadows
                float shadow = SHADOW_ATTENUATION(i);
                col.rgb *= shadow;

                // Quantize final color for pixel art look (reduce color palette)
                float colorSteps = 16.0;
                col.rgb = floor(col.rgb * colorSteps) / colorSteps;

                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
