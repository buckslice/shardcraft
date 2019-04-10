using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class TextureArrayManager : MonoBehaviour {

    public Texture2D[] textures;

    public Texture2D[] tileTextures;

    public Material mat;

    void Awake() {

        mat.SetTexture(ShaderParams.MainTex, GetTexArray(textures));
        mat.SetTexture(ShaderParams.TileTex, GetTexArray(tileTextures));

    }

    Texture2DArray GetTexArray(Texture2D[] tex2Ds) {
        Texture2D t = tex2Ds[0];
        Texture2DArray textureArray = new Texture2DArray(
            t.width, t.height, tex2Ds.Length, t.format, t.mipmapCount > 1);

        textureArray.anisoLevel = t.anisoLevel;
        textureArray.filterMode = t.filterMode;
        textureArray.wrapMode = t.wrapMode;

        for (int i = 0; i < tex2Ds.Length; i++) {
            if (tex2Ds[i].format != t.format) {
                Assert.IsTrue(false); // auto importer doesnt work so manually need to set everyone to same format!!
            }

            for (int m = 0; m < t.mipmapCount; m++) {
                Graphics.CopyTexture(tex2Ds[i], 0, m, textureArray, i, m);
            }
        }

        textureArray.Apply();

        return textureArray;
    }


}
