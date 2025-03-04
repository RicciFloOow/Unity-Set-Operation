Shader "MaskEffect/Complement/SubtrahendObj01"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        [HideInInspector]_ZTest("ZTest", Float) = 4.0//[Enum(UnityEngine.Rendering.CompareFunction)]
        [HideInInspector]_CullMode("CullMode", Float) = 2.0//[Enum(UnityEngine.Rendering.CullMode)]
        [HideInInspector]_ZWrite("ZWrite", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull[_CullMode]
        ZTest[_ZTest]
        ZWrite[_ZWrite]

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            half4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float mod(float x, float y)
            {
                return x - y * floor(x / y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.uv = floor(i.uv * 32);
                float baseColor = lerp(0.5, 0.75, mod((i.uv.x + i.uv.y), 2.0));
                fixed4 col = baseColor * _Color;
                return col;
            }
            ENDCG
        }
    }
}
