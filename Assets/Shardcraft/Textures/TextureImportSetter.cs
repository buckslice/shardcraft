using UnityEngine;
using UnityEditor;

public class TextureImportSetter : AssetPostprocessor {

    // auto imports level textures correctly
    void OnPreprocessTexture() {
        if (assetPath.Contains("Textures")) {
            TextureImporter ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Repeat;

            ti.textureCompression = TextureImporterCompression.Uncompressed;
            //ti.textureFormat = TextureImporterFormat.RGBA32; // this doesnt work with the importer anymore apparently

            ti.mipmapEnabled = true;
        }
    }


}