//TODO:区分API
Shader "MaskEffect/Complement/SubtrahendDepth"
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
            float4 sspos : TEXCOORD0;
        };

        struct Output
        {
            float mask : SV_Target;
            float depth : SV_Depth;
        };

        Texture2D_float _CameraDepthInnerGRT;
        Texture2D_float _CameraDepthInnerLRT;
        Texture2D_float _CameraDepthOuterGRT;
        Texture2D_float _CameraDepthOuterLRT;

        SamplerState sampler_PointClamp;

        ENDCG

        Pass
        {
            Cull Front
            ZWrite On
            ZTest Always//我们需要在PS里写入深度，因此自然要跳过early z

            Name "Subtrahend Depth Sphere Inner Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.sspos = o.vertex;
                return o;
            }

            Output frag (v2f i)
            {
                Output o = (Output)o;
                float2 uv = i.sspos.xy / i.sspos.w * 0.5 + 0.5;//
                float depth = i.sspos.z / i.sspos.w;
                float mask = 1;
#if UNITY_UV_STARTS_AT_TOP
                uv.y = 1 - uv.y;
#endif
                float innerGDepth = _CameraDepthInnerGRT.SampleLevel(sampler_PointClamp, uv, 0).x;
                float outerGDepth = _CameraDepthOuterGRT.SampleLevel(sampler_PointClamp, uv, 0).x;
                float innerLDepth = _CameraDepthInnerLRT.SampleLevel(sampler_PointClamp, uv, 0).x;
                float outerLDepth = _CameraDepthOuterLRT.SampleLevel(sampler_PointClamp, uv, 0).x;
                //
                if (depth > outerGDepth)//比(被减集)GEqual的外表面近
                {
                    if (depth > outerLDepth)
                    {
                        discard;
                    }
                    if (depth < innerLDepth)
                    {
                        if (outerGDepth < outerLDepth)//如果是需要被剔除的，但存在遮挡
                        {
                            depth = 0;
                            mask = 16383 / 65535.0;
                            o.depth = depth;
                            o.mask = mask;
                            return o;
                        }
                        else
                        {
                            discard;
                        }
                    }
                }
                if (depth < innerGDepth)
                {
                    depth = 0;//比(被减集)GEqual的内表面远的需要裁去：因为这部分在减集的内部，是我们不需要的部分
                    mask = 0;
                }
                o.depth = depth;
                o.mask = mask;
                return o;
            }
            ENDCG
        }

        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.sspos = o.vertex;
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                float2 uv = i.sspos.xy / i.sspos.w * 0.5 + 0.5;//
                float depth = i.sspos.z / i.sspos.w;
                float mask = 32767 / 65535.0;
#if UNITY_UV_STARTS_AT_TOP
                uv.y = 1 - uv.y;
#endif
                float outerLDepth = _CameraDepthOuterLRT.SampleLevel(sampler_PointClamp, uv, 0).x;
                if (depth >= outerLDepth)
                {
                    discard;
                }
                return mask;
            }
            ENDCG
        }
    }
}
