#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

// 画像の有無の判定用。実行時に50枚を一括ロードせず、IDの索引を読む。
// Thumb の追加・削除・移動時に更新。Texture/Sprite 自体はロードしない。
public sealed class CharaImageIndex : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Chara/Thumb";
    private const string Output = "Assets/Resources/Chara/image-index.txt";

    [InitializeOnLoadMethod]
    private static void Initialize() { EditorApplication.delayCall += Rebuild; }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (TouchesImages(imported) || TouchesImages(deleted) || TouchesImages(moved) || TouchesImages(movedFrom))
            EditorApplication.delayCall += Rebuild;
    }

    private static bool TouchesImages(string[] paths)
    {
        foreach (string path in paths)
            if (path.StartsWith(Folder + "/", StringComparison.Ordinal)) return true;
        return false;
    }

    [MenuItem("Gunya/Rebuild Character Image Index")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        var ids = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.Sprite)
                ids.Add(Path.GetFileNameWithoutExtension(path));
        }
        ids.Sort(StringComparer.Ordinal);
        string text = string.Join("\n", ids.ToArray()) + "\n";
        if (File.Exists(Output) && File.ReadAllText(Output) == text) return;
        File.WriteAllText(Output, text, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(Output);
    }
}
#endif
