using UnityEditor;
using UnityEngine;

// ============================================================
// AudioImportSettings.cs (Editor専用)
// Resources/Audio/BGM の曲を取り込むときの設定。
// 長い曲(ループ用)はストリーミング、短いジングルはメモリ上で圧縮のまま。
// ============================================================
public class AudioImportSettings : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Resources/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        ai.forceToMono = false;
        ai.loadInBackground = true;
        var s = ai.defaultSampleSettings;
        bool loop = p.Contains("bgm-home") || p.Contains("bgm-match") || p.EndsWith("bgm-gacha.ogg")
                    || p.Contains("bgm-team-screen") || p.Contains("bgm-shop") || p.Contains("bgm-event")
                    || p.Contains("bgm-training") || p.Contains("bgm-tournament-entry") || p.Contains("bgm-champion");
        s.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.5f;
        s.preloadAudioData = !loop;
        ai.defaultSampleSettings = s;
    }
}
