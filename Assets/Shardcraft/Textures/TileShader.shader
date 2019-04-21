Shader "Custom/TileShader"
{
    Properties
    {
        _MainTex("BlockTextureArray", 2DArray) = "white" {}
    _TileTex("BlockTiledTextureArray", 2DArray) = "white" {}
    _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
    }
        SubShader
    {
        Tags{ "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types

        //#include UnityPBSLighting.cginc

    #pragma surface surf Standard alphatest:_Cutoff addshadow vertex:vert
        //#pragma surface surf Standard alphatest:_Cutoff vertex:vert

        //#pragma surface surf Unlit noforwardadd alphatest:_Cutoff vertex:vert

    #pragma target 3.5

        UNITY_DECLARE_TEX2DARRAY(_MainTex);
    UNITY_DECLARE_TEX2DARRAY(_TileTex);

    struct Input {
        float3 blockUVs;
        float3 torch;
        float sunlight;
        float ao;
        float textureType;
    };

    half _Glossiness;
    half _Metallic;

    void vert(inout appdata_full v, out Input OUT) {
        UNITY_INITIALIZE_OUTPUT(Input, OUT);
        OUT.blockUVs = v.texcoord.xyz;
        OUT.torch = v.color.rgb;
        OUT.sunlight = v.color.a;
        OUT.ao = v.texcoord2.y;
        OUT.textureType = v.texcoord2.x;
    }

    // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
    // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
    // #pragma instancing_options assumeuniformscaling
    UNITY_INSTANCING_BUFFER_START(Props)
        // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf(Input IN, inout SurfaceOutputStandard o) {
        // Albedo comes from a texture tinted by color
        fixed4 texCol;

        if (IN.textureType > 1.0) {
            texCol = UNITY_SAMPLE_TEX2DARRAY(_TileTex, IN.blockUVs);
        } else {
            texCol = UNITY_SAMPLE_TEX2DARRAY(_MainTex, IN.blockUVs);
        }

        half3 light = GammaToLinearSpace(IN.torch);

        // makes non perfect attenuators (like orange) look weird
        //light = saturate(light*5.0); // kinda like bright mode in minecraft

        o.Albedo = texCol.rgb * lerp(1.0,2.0,light);
        o.Albedo *= IN.ao;
        o.Alpha = texCol.a; // set from transparent textures

        o.Emission = o.Albedo*lerp(0.005, 1.0, light);

        o.Smoothness = _Glossiness;
        o.Metallic = _Metallic;
        //o.Emission = IN.color.rgb;
    }
    ENDCG
    }
        FallBack "Diffuse"
}
