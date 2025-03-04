Shader "MaskEffect/Complement/ComplementDepth"
{
    SubShader
    {
        CGINCLUDE
        #include "UnityCG.cginc"

        //ref: URP
        float2 GetFullScreenTriangleTexCoord(uint vertexID)
        {
#if UNITY_UV_STARTS_AT_TOP
            return float2((vertexID << 1) & 2, 1.0 - (vertexID & 2));
#else
            return float2((vertexID << 1) & 2, vertexID & 2);
#endif
        }

        float4 GetFullScreenTriangleVertexPosition(uint vertexID, float z = 1)//given api near clip value
        {
            // note: the triangle vertex position coordinates are x2 so the returned UV coordinates are in range -1, 1 on the screen.
            float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
            float4 pos = float4(uv * 2.0 - 1.0, z, 1.0);
            return pos;
        }

        struct v2f
        {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        v2f vert(uint vertexID : SV_VertexID)
        {
            v2f o;
            o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
            o.uv = GetFullScreenTriangleTexCoord(vertexID);
            return o;
        }
        ENDCG

        Pass
        {
            Cull Off ZWrite On ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Texture2D_float _CameraDepthRT;
            Texture2D_float _CameraDepthOuterLRT;
            Texture2D_float _CameraDepthOuterGRT;
            Texture2D_float _CameraDepthSubInnerRT;
            Texture2D _CameraMaskRT;

            SamplerState sampler_PointClamp;

            float frag (v2f i) : SV_Depth
            {
                float mask = _CameraMaskRT.SampleLevel(sampler_PointClamp, i.uv, 0).x;
                float depth = _CameraDepthRT.SampleLevel(sampler_PointClamp, i.uv, 0).x;
                if (mask == 32767 / 65535.0)
                {
                    depth = _CameraDepthOuterLRT.SampleLevel(sampler_PointClamp, i.uv, 0).x;
                }
                if (mask == 16383 / 65535.0)
                {
                    depth = _CameraDepthOuterGRT.SampleLevel(sampler_PointClamp, i.uv, 0).x;
                }
                else if (mask == 1)
                {
                    depth = _CameraDepthSubInnerRT.SampleLevel(sampler_PointClamp, i.uv, 0).x;
                }
                return depth;
            }
            ENDCG
        }

        Pass
        {
            Cull Off ZWrite On ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Texture2D_float _CameraDepthRT;
            SamplerState sampler_PointClamp;

            float frag(v2f i) : SV_Depth
            {
                return _CameraDepthRT.SampleLevel(sampler_PointClamp, i.uv, 0).x;
            }
            ENDCG
        }
    }
}
