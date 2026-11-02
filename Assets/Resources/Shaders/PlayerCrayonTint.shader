Shader "DrawBody/PlayerCrayonTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(input.texcoord);
                // Change colored pencil pigment to the player's color without
                // tinting neutral graphite outlines and white paper highlights.
                float high = max(source.r, max(source.g, source.b));
                float low = min(source.r, min(source.g, source.b));
                float pigment = smoothstep(0.04, 0.22, high - low);
                float saturation = (high - low) / max(0.001, high);
                fixed3 colored = lerp(fixed3(1,1,1), input.color.rgb, saturation) * high;
                fixed4 output = fixed4(lerp(source.rgb, colored, pigment), source.a * input.color.a);
                output.rgb *= output.a;
                return output;
            }
            ENDCG
        }
    }
}
