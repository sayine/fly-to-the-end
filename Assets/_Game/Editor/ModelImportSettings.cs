using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Import settings for Meshy models in Assets/_Game/Art/Models (no Inspector tweaking per file):
    /// static geometry only (no materials, animation, cameras, lights), and base-color textures capped for mobile.
    /// Materials are made by <see cref="PropModels"/> from Textures/&lt;Name&gt;_base_color.png.
    /// </summary>
    internal sealed class ModelImportSettings : AssetPostprocessor
    {
        private const int MaxTextureSize = 512;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(PropModels.Folder + "/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.addCollider = false; // greybox primitives stay the colliders
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.bakeAxisConversion = true;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PropModels.TexturesFolder + "/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = MaxTextureSize;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.sRGBTexture = true;
        }
    }
}
