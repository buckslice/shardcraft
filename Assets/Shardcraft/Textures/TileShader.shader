Shader "Custom/TileShader"
{
    Properties
    {
        _MainTex ("BlockTextureArray", 2DArray) = "white" {}
        _TileTex("BlockTiledTextureArray", 2DArray) = "white" {}
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

        //#include UnityPBSLighting.cginc

        #pragma surface surf Standard alphatest:_Cutoff addshadow vertex:vert
        //#pragma surface surf Standard alphatest:_Cutoff vertex:vert

        //#pragma surface surf Unlit noforwardadd alphatest:_Cutoff vertex:vert

        #pragma target 3.5

        UNITY_DECLARE_TEX2DARRAY(_MainTex);
        UNITY_DECLARE_TEX2DARRAY(_TileTex);

        struct Input
        {
            float3 blockUVs;
            float textureType;
            float4 color : COLOR;
        }; 

        half _Glossiness;
        half _Metallic;

        void vert(inout appdata_full v, out Input OUT) {
            UNITY_INITIALIZE_OUTPUT(Input, OUT);
            OUT.blockUVs = v.texcoord.xyz;
            OUT.textureType = v.texcoord2.x;
            OUT.color = v.color;
        }

        //fixed4 LightingUnlit(SurfaceOutput s, fixed3 lightDir, fixed atten) {
        //    fixed4 c;
        //    c.rgb = s.Albedo;
        //    c.a = s.Alpha;
        //    return c;
        //}


        //half4 LightingBlonk_Deferred(SurfaceOutputStandard s, half3 viewDir, UnityGI gi, out half4 outGBuffer0, out half4 outGBuffer1, out half4 outGBuffer2) {
        //    UnityStandardData data;
        //    data.diffuseColor = s.Albedo;
        //    data.occlusion = 1;
        //    // PI factor come from StandardBDRF (UnityStandardBRDF.cginc:351 for explanation)
        //    data.specularColor = _SpecColor.rgb * s.Gloss * (1 / UNITY_PI);
        //    data.smoothness = s.Specular;
        //    data.normalWorld = s.Normal;

        //    UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

        //    half4 emission = half4(s.Emission, 1);

        //#ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
        //    emission.rgb += s.Albedo * gi.indirect.diffuse;
        //#endif

        //    return emission;
        //}

        //void LightingBlonk_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi) {
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
            fixed4 texCol;

            if (IN.textureType > 1.0) {
                texCol = UNITY_SAMPLE_TEX2DARRAY(_TileTex, IN.blockUVs);
            } else {
                texCol = UNITY_SAMPLE_TEX2DARRAY(_MainTex, IN.blockUVs);
            }

            half3 light = GammaToLinearSpace(IN.color.rgb);

            // makes non perfect attenuators (like orange) look weird
            //light = saturate(light*5.0); // kinda like bright mode in minecraft

            o.Albedo = texCol.rgb;
            //o.Albedo = float3(1, 1, 1);

            //o.Albedo = texCol.rgb *lerp(1.0,2.0,light);
            //o.Albedo = col.rgb;
            o.Albedo *= IN.color.a; // multiply by ambient occlusion stored in alpha channel of color
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
