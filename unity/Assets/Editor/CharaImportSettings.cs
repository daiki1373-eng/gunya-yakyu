using UnityEditor;

// ============================================================
// CharaImportSettings.cs (Editor専用)
// Resources/Chara 以下のキャラ画像を、取り込み時に自動でスプライト設定にする。
// Full=拡大表示用(最大2048)、Thumb=一覧用(最大512)。どちらも圧縮して容量を抑える。
// ============================================================
public class CharaImportSettings : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Resources/Chara/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.crunchedCompression = true;
        ti.compressionQuality = 60;
        ti.maxTextureSize = p.Contains("/Thumb/") ? 512 : 2048;
        ti.spritePixelsPerUnit = 100f;
    }
}
