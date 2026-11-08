using UnityEditor;
using UnityEngine;

// ============================================================
// CharaImportSettings.cs (Editor専用)
// Resources/Chara 以下のキャラ画像を、取り込み時に自動でスプライト設定にする。
// Full=拡大表示用(最大2048)、Thumb=一覧用(最大512)。どちらも圧縮して容量を抑える。
// ============================================================
public class CharaImportSettings : AssetPostprocessor
{
    // ブラウザ版(WebGL、主に iPhone の Safari)は ASTC で圧縮して容量とメモリを抑える。
    // 寸法を変えると試合用の絵(体・決めポーズ・切り抜き)の画素座標がずれるので、縮めるのはカード絵(Full)と一覧(Thumb)だけ。
    private static void WebGL(TextureImporter ti, int maxSize, TextureImporterFormat fmt)
    {
        var s = ti.GetPlatformTextureSettings("WebGL");
        s.overridden = true;
        s.maxTextureSize = maxSize;
        s.format = fmt;
        s.compressionQuality = 50;
        ti.SetPlatformTextureSettings(s);
    }

    private void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        // 演出素材(タイトル背景・大型ビジョン・ガチャマシン・アイコン)
        if (p.Contains("/Resources/Presentation/"))
        {
            var pi = (TextureImporter)assetImporter;
            pi.textureType = TextureImporterType.Sprite;
            pi.spriteImportMode = SpriteImportMode.Single;
            pi.alphaIsTransparency = true;
            pi.mipmapEnabled = p.Contains("/vision_");      // 球場の中で遠くに映るものだけミップマップ
            pi.npotScale = TextureImporterNPOTScale.None;
            pi.textureCompression = TextureImporterCompression.Compressed;
            pi.crunchedCompression = true;
            pi.compressionQuality = 70;
            pi.maxTextureSize = 2048;
            pi.spritePixelsPerUnit = 100f;
            WebGL(pi, 2048, TextureImporterFormat.ASTC_6x6);
            return;
        }
        if (!p.Contains("/Resources/Chara/")) return;
        var ti = (TextureImporter)assetImporter;
        // Aポーズの体の部品の塗り分け(BodyLabel)。値をそのまま読むので、圧縮・ミップマップ・補間なし、CPUから読める
        if (p.Contains("/BodyLabel/"))
        {
            ti.textureType = TextureImporterType.SingleChannel;
            var ls = new TextureImporterSettings();
            ti.ReadTextureSettings(ls);
            ls.singleChannelComponent = TextureImporterSingleChannelComponent.Alpha;
            ti.SetTextureSettings(ls);
            ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 1024;
            return;
        }
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
        if (p.Contains("/Full/")) WebGL(ti, 1024, TextureImporterFormat.ASTC_6x6);
        else if (p.Contains("/Thumb/")) WebGL(ti, 512, TextureImporterFormat.ASTC_6x6);
        else if (p.Contains("/Pose/")) WebGL(ti, 1024, TextureImporterFormat.ASTC_8x8);
        else WebGL(ti, 2048, TextureImporterFormat.ASTC_6x6);   // 体・持ち物・切り抜き・頭(寸法はそのまま)
        // 試合で動かす用(Body=Aポーズの立ち絵 / Bat=持ち物 / Glove)。寸法は4の倍数で作ってあるのでそのまま圧縮する
        bool body = p.Contains("/Body/"), prop = p.Contains("/Bat/") || p.Contains("/Glove/");
        if (body || prop)
        {
            ti.alphaIsTransparency = true;
            // 体(512×768)と持ち物(横長・寸法まちまち)は2の累乗でない。2の累乗でない絵にミップマップを付けると、
            // ブラウザ版では ASTC に圧縮されず無圧縮(1枚2MB)で入ってしまう(ダウンロードが約150MBになっていた原因)。
            // 遠くの選手はビルボード絵に切り替えるので、ここではミップマップを付けない。
            ti.mipmapEnabled = false;
            ti.maxTextureSize = body ? 1024 : 512;
            var bs = new TextureImporterSettings();
            ti.ReadTextureSettings(bs);
            bs.spriteAlignment = body ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
            bs.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(bs);
            return;
        }
        // 試合で使う切り抜き(Cut=立ち姿 / Head=頭)。透明あり・遠くでもちらつかないようミップマップあり
        bool cut = p.Contains("/Cut/"), head = p.Contains("/Head/");
        if (cut || head)
        {
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = cut ? 1024 : 256;
            // 512・256 の正方形で書き出しているので、2の累乗への拡大は不要(スプライトでは設定できない)
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteAlignment = cut ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
        }
    }
}
