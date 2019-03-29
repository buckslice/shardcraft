Shader "Custom/TileShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("TerrainTextureArray", 2DArray) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types

        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow vertex:vert
        //#pragma surface surf Unlit noforwardadd alphatest:_Cutoff vertex:vert

        #pragma target 3.5

        UNITY_DECLARE_TEX2DARRAY(_MainTex);

        struct Input
        {
            float3 blockUVs;
            float4 color : COLOR;
        }; 

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        void vert(inout appdata_full v, out Input OUT) {
            UNITY_INITIALIZE_OUTPUT(Input, OUT);
            OUT.blockUVs = v.texcoord.xyz;
            OUT.color = v.color;
        }

        //fixed4 LightingUnlit(SurfaceOutput s, fixed3 lightDir, fixed atten) {
        //    fixed4 c;
        //    c.rgb = s.Albedo;
        //    c.a = s.Alpha;
        //    return c;
        //}

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_MainTex, IN.blockUVs);

            o.Albedo = c.rgb * IN.color.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
