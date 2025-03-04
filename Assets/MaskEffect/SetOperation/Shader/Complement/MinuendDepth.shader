Shader "MaskEffect/Complement/MinuendDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGINCLUDE
        #include "UnityCG.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            return o;
        }

        float frag (v2f i) : SV_Target
        {
            return 0;
        }
        ENDCG

        Pass
        {
            Cull Front
            ZWrite On
            ZTest GEqual

            ColorMask 0

            Name "Minuend Depth GEqual Back Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }

        Pass
        {
            Cull Front
            ZWrite On
            ZTest LEqual

            ColorMask 0

            Name "Minuend Depth LEqual Back Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }

        Pass
        {
            Cull Back
            ZWrite On
            ZTest GEqual

            ColorMask 0

            Name "Minuend Depth GEqual Front Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            Name "Minuend Depth LEqual Front Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragMask

            float fragMask (v2f i) : SV_Target
            {
                return 32767 / 65535.0;
            }
            ENDCG
        }
    }
}
