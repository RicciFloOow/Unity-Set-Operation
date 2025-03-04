Shader "MaskEffect/Complement/MinuendObj01"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        [HideInInspector]_ZTest("ZTest", Float) = 4.0
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
            };

            sampler2D _MainTex;
            half4 _Color;

            v2f vert (appdata v)
            {
                float4 wPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
                float4 vert = mul(UNITY_MATRIX_VP, wPos);
                v2f o;
                o.vertex = vert;
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - wPos.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.viewDir = normalize(i.viewDir);
                i.normal = normalize(i.normal);
                float NDotV = 0.5 + 0.5 * dot(i.viewDir, i.normal);
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.xyz *= NDotV;
                return col;
            }
            ENDCG
        }
    }
}
