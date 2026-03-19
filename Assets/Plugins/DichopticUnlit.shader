Shader "Unlit/DichopticUnlit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ContrastLeft ("Contrast Left", Range(0,2)) = 1
        _ContrastRight ("Contrast Right", Range(0,2)) = 0.5
        _BrightnessLeft ("Brightness Left", Range(0,2)) = 1
        _BrightnessRight ("Brightness Right", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            ZWrite On
            Cull Back
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            float _ContrastLeft;
            float _ContrastRight;
            float _BrightnessLeft;
            float _BrightnessRight;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // unity_StereoEyeIndex: 0 = left, 1 = right
                bool isRight = (unity_StereoEyeIndex == 1);

                float contrast   = isRight ? _ContrastRight   : _ContrastLeft;
                float brightness = isRight ? _BrightnessRight : _BrightnessLeft;

                fixed3 col = _BaseColor.rgb;

                // simple contrast around mid-grey, then brightness
                col = (col - 0.5) * contrast + 0.5;
                col *= brightness;

                return fixed4(saturate(col), _BaseColor.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
