Shader "Unlit/LeftEyeOnlyGhost"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0,1,0,0.6)
        _Alpha ("Alpha", Range(0,1)) = 0.6
    }

    SubShader
    {
        // Transparent, and crucially: no depth writing (prevents stereo depth artifacts)
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            float _Alpha;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // 0 = Left, 1 = Right
                if (unity_StereoEyeIndex == 1)
                {
                    // Do not render ANY pixels in the right eye
                    clip(-1);
                }

                fixed4 c = _BaseColor;
                c.a *= _Alpha;
                return c;

                return _BaseColor;
            }
            ENDCG
        }
    }
}
