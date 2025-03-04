Shader "MaskEffect/Complement/MinuendObj02"
{
    Properties
    {
        [HideInInspector] _ZTest("ZTest", Float) = 4.0
        [HideInInspector]_ZWrite("ZWrite", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 viewDir : TEXCOORD1;
                float3 wpos : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                float4 wPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, wPos);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - wPos.xyz;
                o.wpos = wPos.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.viewDir = normalize(i.viewDir);
                i.normal = normalize(i.normal);
                float NDotV = 0.5 + 0.5 * dot(i.viewDir, i.normal);
                half3 col = 0.5 + 0.5 * cos(_Time.y * 1.14514 + i.wpos * 0.1 + float3(1, 3, 5));
                col *= NDotV;
                return half4(col, 1);
            }
            ENDCG
        }
    }
}
