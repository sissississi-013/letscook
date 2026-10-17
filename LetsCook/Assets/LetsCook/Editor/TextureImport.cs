using UnityEditor;

namespace LetsCook.EditorTools
{
    /// Poly Haven textures: *_nor.jpg are OpenGL normal maps; everything tiles.
    public class TextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/LetsCook/Resources/Textures/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            if (assetPath.EndsWith("_nor.jpg")) importer.textureType = TextureImporterType.NormalMap;
        }
    }
}
