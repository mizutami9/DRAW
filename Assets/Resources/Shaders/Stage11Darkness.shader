Shader "DrawBody/Stage11Darkness"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _PointLights[16];
            int _PointLightCount;
            float4 _ConeOriginDirection;
            float4 _ConeShape;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xy;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float revealed = 0.0;
                [unroll]
                for (int index = 0; index < 16; index++)
                {
                    if (index >= _PointLightCount) break;
                    float distanceToLight = distance(input.worldPosition, _PointLights[index].xy);
                    float radius = _PointLights[index].z;
                    float feather = max(0.05, _PointLights[index].w);
                    revealed = max(revealed, 1.0 - smoothstep(radius - feather, radius + feather, distanceToLight));
                }

                float2 coneOrigin = _ConeOriginDirection.xy;
                float2 coneDirection = normalize(_ConeOriginDirection.zw + float2(0.0001, 0.0));
                float2 relative = input.worldPosition - coneOrigin;
                float along = dot(relative, coneDirection);
                float side = abs(relative.x * coneDirection.y - relative.y * coneDirection.x);
                float coneLength = max(0.01, _ConeShape.x);
                float coneHalfWidth = lerp(0.35, _ConeShape.y, saturate(along / coneLength));
                float coneFeather = max(0.05, _ConeShape.z);
                float withinLength = smoothstep(-0.2, 0.5, along)
                    * (1.0 - smoothstep(coneLength - coneFeather, coneLength + coneFeather, along));
                float withinWidth = 1.0 - smoothstep(coneHalfWidth - coneFeather, coneHalfWidth + coneFeather, side);
                revealed = max(revealed, saturate(withinLength * withinWidth * _ConeShape.w));

                fixed textureAlpha = tex2D(_MainTex, input.uv).a;
                // Even the centre retains a thin pencil-dark veil.  It reads as
                // a real night scene instead of a perfectly cut-out daylight cone.
                return fixed4(0, 0, 0, textureAlpha * _Color.a * (1.0 - saturate(revealed * 0.88)));
            }
            ENDCG
        }
    }
}
