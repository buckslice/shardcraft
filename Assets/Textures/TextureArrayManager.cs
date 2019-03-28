using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureArrayManager : MonoBehaviour {

    public Texture2D[] textures;

    public Material mat;

    void Awake() {
        Texture2D t = textures[0];
        Texture2DArray textureArray = new Texture2DArray(
            t.width, t.height, textures.Length, t.format, t.mipmapCount > 1);

        textureArray.anisoLevel = t.anisoLevel;
        textureArray.filterMode = t.filterMode;
        textureArray.wrapMode = t.wrapMode;

        for (int i = 0; i < textures.Length; i++) {
            if(textures[i].format != TextureFormat.RGBA32) {
                Debug.Assert(false); // auto importer doesnt work so manually need to set them to this format!
            }

            for (int m = 0; m < t.mipmapCount; m++) {
                Graphics.CopyTexture(textures[i], 0, m, textureArray, i, m);
            }
        }

        textureArray.Apply();

        mat.SetTexture(ShaderParams.MainTex, textureArray);

    }


}
