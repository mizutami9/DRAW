Shader "DrawBody/CrayonBackground"
{
    Properties
    {
        _PencilStrength ("Pencil Strength", Range(0, 1)) = 0.50
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "False"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed _PencilStrength;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float PencilHatch(
                float2 position,
                float spacing,
                float slope,
                float lineWidth,
                float segmentLength,
                float seed)
            {
                float rowCoordinate = (position.y - position.x * slope) / spacing + seed;
                float row = floor(rowCoordinate);
                float distanceToLine = abs(frac(rowCoordinate) - 0.5);
                float antialiasing = max(fwidth(rowCoordinate) * 0.55, 0.005);
                float hatchLine = 1.0 - smoothstep(lineWidth, lineWidth + antialiasing, distanceToLine);

                float alongStroke = (position.x + position.y * slope) / segmentLength;
                float segment = floor(alongStroke);
                float segmentPosition = frac(alongStroke);
                float endFade = smoothstep(0.02, 0.10, segmentPosition)
                    * (1.0 - smoothstep(0.88, 0.98, segmentPosition));
                float pressure = lerp(0.52, 1.0, Hash21(float2(row + seed * 31.0, segment)));
                float missingStroke = step(0.10, Hash21(float2(segment + seed * 17.0, row * 0.73)));
                return hatchLine * endFade * pressure * missingStroke;
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xy;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 position = input.worldPosition;
                float hatchA = PencilHatch(position, 0.31, 0.28, 0.040, 1.55, 0.17);
                float hatchB = PencilHatch(position + float2(0.09, 0.04), 0.41, 0.32, 0.035, 1.25, 2.31);
                float hatchC = PencilHatch(position + float2(-0.05, 0.08), 0.56, 0.24, 0.030, 1.90, 4.73);
                float pencilCoverage = saturate(hatchA * 0.48 + hatchB * 0.31 + hatchC * 0.12);

                fixed4 selectedColor = input.color;
                fixed opacity = saturate(selectedColor.a);
                // Alpha values above this still belong to the saved color, but
                // must not keep darkening the pencil pigment. Stage 1-1 uses
                // 16% while 1-3 uses 100%; an uncapped linear mapping made the
                // latter roughly 3.5 times darker.
                fixed visualOpacity = min(opacity, 0.35);
                fixed3 paperColor = fixed3(0.985, 0.975, 0.93);
                fixed3 paperTint = lerp(paperColor, selectedColor.rgb, visualOpacity * 0.36);

                // Draw with the selected background hue itself. Very pale colors
                // only receive a small same-hue darkening so the strokes remain
                // readable; they are never mixed toward neutral black.
                fixed selectedLuminance = dot(selectedColor.rgb, fixed3(0.2126, 0.7152, 0.0722));
                fixed paleColorFactor = lerp(0.76, 0.94, saturate((0.86 - selectedLuminance) * 2.0));
                fixed3 pencilColor = selectedColor.rgb * paleColorFactor;
                fixed pigmentStrength = lerp(0.15, 1.0, visualOpacity);
                fixed amount = saturate(pencilCoverage * _PencilStrength * pigmentStrength);

                fixed4 outputColor;
                outputColor.rgb = lerp(paperTint, pencilColor, amount);
                outputColor.a = 1.0;
                return outputColor;
            }
            ENDCG
        }
    }
}
