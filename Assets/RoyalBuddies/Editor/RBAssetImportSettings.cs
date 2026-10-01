using UnityEditor;
using UnityEngine;

namespace RoyalBuddies.EditorTools
{
    /// <summary>
    /// Réglages d'import des textures d'interface (atlas HUD / cartes) : pas de mipmaps,
    /// taille max 4096 (les atlas font 1024-1536 px, on ne veut aucune réduction), sRGB, clamp.
    /// Les Sprites sont créés à l'exécution (Sprite.Create) à partir des mêmes coordonnées que Godot.
    /// </summary>
    public class RBAssetImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/RoyalBuddies/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 4096;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.isReadable = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/RoyalBuddies/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;     // musiques longues : streaming pour économiser la RAM mobile
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            ai.defaultSampleSettings = s;
        }
    }
}
