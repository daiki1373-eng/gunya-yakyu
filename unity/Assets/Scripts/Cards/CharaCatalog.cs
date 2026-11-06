using System.Collections.Generic;
using UnityEngine;

public enum Rarity { R1 = 1, R2 = 2, R3 = 3, R4 = 4, R5 = 5, Secret = 6 }

public class CharaDef
{
    public string Id;          // 画像ファイル名(拡張子なし)。保存データ・画像のキー
    public string Code;        // ch001 など(roster-101.csv の id)
    public string Name;
    public Rarity Rarity;
    public int Index;
    public string Intro = "";
    public string Position = "";
    public string[] Tags = new string[0];
    public int StackLimit = 1;
    public int ExchangePoints = 1;
    public string ImageFile = "";
    // 小さい索引だけを読む。相手チームの編成で全員の画像をロードしない。
    public bool HasImage { get { return CharaCatalog.HasImage(Id); } }

}

// ============================================================
// CharaCatalog.cs
// キャラ一覧の正本は Resources/Chara/roster-101.csv(id,name,rarity,intro,position,tags,stack_limit,exchange_points,image_file)。
// 101体すべてを読み込み、図鑑・ガチャ・試合に使う。画像がないキャラはプレースホルダで表示する。
// 画像は Resources/Chara/Full(拡大用) と Thumb(一覧用 2:3)、試合用に Cut(立ち姿) と Head(顔)。
// ============================================================
public static class CharaCatalog
{
    public static readonly List<CharaDef> All = new List<CharaDef>();
    private static readonly Dictionary<string, CharaDef> ById = new Dictionary<string, CharaDef>();

    static CharaCatalog()
    {
        var ta = Resources.Load<TextAsset>("Chara/roster-101");
        if (ta == null) { Debug.LogError("roster-101.csv が見つかりません"); return; }
        var rows = ParseCsv(ta.text);
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Count < 9 || string.IsNullOrEmpty(r[0])) continue;
            string file = r[8].Trim();
            string id = System.IO.Path.GetFileNameWithoutExtension(file);
            var d = new CharaDef
            {
                Id = id, Code = r[0].Trim(), Name = r[1].Trim(), Rarity = ParseRarity(r[2].Trim()), Index = All.Count,
                Intro = r[3].Trim(), Position = r[4].Trim(), Tags = r[5].Trim().Split('|'),
                StackLimit = ParseInt(r[6], 1), ExchangePoints = ParseInt(r[7], 1), ImageFile = file
            };
            All.Add(d);
            ById[id] = d;
        }
        // 表示順: レア度の高い順、同じレア度はCSVの順
        All.Sort((x, y) => x.Rarity != y.Rarity ? ((int)y.Rarity).CompareTo((int)x.Rarity) : x.Code.CompareTo(y.Code));
        for (int i = 0; i < All.Count; i++) All[i].Index = i;
    }

    private static int ParseInt(string s, int def)
    {
        int v;
        return int.TryParse(s.Trim(), out v) ? v : def;
    }

    public static Rarity ParseRarity(string s)
    {
        if (s.StartsWith("SECRET")) return Rarity.Secret;
        // 「★5」(星+数字) と「★★★★★」(星の数) のどちらでも読めるようにする
        int n;
        if (!int.TryParse(s.Replace("★", "").Trim(), out n))
        {
            n = 0;
            foreach (char c in s) if (c == '★') n++;
        }
        return (Rarity)Mathf.Clamp(n, 1, 5);
    }

    // ダブルクォート対応の簡易CSV
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool q = false;
        text = text.Replace("\r\n", "\n").TrimStart('\uFEFF');
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                else sb.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == ',') { row.Add(sb.ToString()); sb.Length = 0; }
            else if (c == '\n') { row.Add(sb.ToString()); sb.Length = 0; rows.Add(row); row = new List<string>(); }
            else if (c != '\r') sb.Append(c);
        }
        if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }
        return rows;
    }

    public static CharaDef Get(string id)
    {
        CharaDef d;
        return id != null && ById.TryGetValue(id, out d) ? d : null;
    }

    public static CharaDef ByCode(string code)
    {
        foreach (var d in All) if (d.Code == code) return d;
        return null;
    }

    public static List<CharaDef> OfRarity(Rarity r)
    {
        var list = new List<CharaDef>();
        foreach (var d in All) if (d.Rarity == r) list.Add(d);
        return list;
    }

    // 重ね上限(CSVの stack_limit が正。これはCSVにない場合の既定値: ★1=10 … ★5=6 / SECRET=5)
    public static int StackMax(CharaDef d) { return d.StackLimit > 0 ? d.StackLimit : StackMax(d.Rarity); }

    public static int StackMax(Rarity r)
    {
        switch (r)
        {
            case Rarity.R1: return 10;
            case Rarity.R2: return 9;
            case Rarity.R3: return 8;
            case Rarity.R4: return 7;
            case Rarity.R5: return 6;
            default: return 5;
        }
    }

    public static string Label(Rarity r)
    {
        if (r == Rarity.Secret) return "SECRET";
        return new string('★', (int)r);
    }

    // 枠の色(金×赤の路線。低レアは落ち着いた色、上ほど金と赤が強い)
    public static Color FrameColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.R1: return new Color(0.62f, 0.64f, 0.68f);
            case Rarity.R2: return new Color(0.72f, 0.47f, 0.28f);
            case Rarity.R3: return new Color(0.28f, 0.52f, 0.92f);
            case Rarity.R4: return new Color(0.91f, 0.73f, 0.14f);
            case Rarity.R5: return new Color(0.90f, 0.16f, 0.16f);
            default: return new Color(0.98f, 0.90f, 0.55f);
        }
    }

    // 画像追加時は Editor/CharaImageIndex が自動更新する。
    private static HashSet<string> _imageIds;
    public static bool HasImage(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (_imageIds == null)
        {
            _imageIds = new HashSet<string>(System.StringComparer.Ordinal);
            var index = Resources.Load<TextAsset>("Chara/image-index");
            if (index != null)
                foreach (string line in index.text.Split('\n'))
                {
                    string key = line.Trim();
                    if (key.Length > 0) _imageIds.Add(key);
                }
        }
        return _imageIds.Contains(id);
    }

    // ---- 画像の読み込み(拡大用は必要なときだけ読む) ----
    private static readonly Dictionary<string, Sprite> Thumbs = new Dictionary<string, Sprite>();

    public static Sprite Thumb(string id) { return Load(Thumbs, "Thumb", id); }

    // 試合用: 背景を抜いた立ち姿(足元が原点) / 頭(円形に切り抜き)
    private static readonly Dictionary<string, Sprite> Cuts = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> Heads = new Dictionary<string, Sprite>();

    public static Sprite Cut(string id) { return Load(Cuts, "Cut", id); }
    public static Sprite Head(string id) { return Load(Heads, "Head", id); }
    public static Sprite Full(string id) { return Load(Fulls, "Full", id); }

    // 試合で動かす用: Aポーズの立ち絵(Body) / 持ち物(Bat。握る側が左) / グローブ(Glove)。Tools/chara-rig/prep_body.py で作る
    private static readonly Dictionary<string, Sprite> Bodies = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> Bats = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> Gloves = new Dictionary<string, Sprite>();
    public static Sprite Body(string id) { return Load(Bodies, "Body", id); }
    public static Sprite BatArt(string id) { return Load(Bats, "Bat", id); }
    public static Sprite GloveArt(string id) { return Load(Gloves, "Glove", id); }

    // ---- 画像のメモリ管理 ----
    // 読んだ画像は最後に使った時刻を覚えておく。種類ごとの上限を超えたら、画面で使われていない古いものから
    // 手放し(辞書から外す)、Resources.UnloadUnusedAssets でどこからも参照されていない画像だけをメモリから消す。
    // 使用中(表示中の UI・スプライト・選手の体)の画像は消さない。必要になればまた読み込む。
    private static readonly Dictionary<string, Sprite> Fulls = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, float> LastUse = new Dictionary<string, float>();
    public static int MaxThumbs = 40, MaxCuts = 32, MaxHeads = 32, MaxFulls = 4, MaxBodies = 24, MaxProps = 24;   // Cut・Head は両チームの打順(18人)が収まる数
    public static int Loads { get; private set; }
    public static int Released { get; private set; }

    private static Sprite Load(Dictionary<string, Sprite> dict, string kind, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        LastUse[kind + "/" + id] = Time.realtimeSinceStartup;
        Sprite s;
        if (dict.TryGetValue(id, out s) && s != null) return s;
        s = Resources.Load<Sprite>("Chara/" + kind + "/" + id);
        dict[id] = s;
        if (s != null) Loads++;
        return s;
    }

    public static int Cached { get { return Thumbs.Count + Cuts.Count + Heads.Count + Fulls.Count + Bodies.Count + Bats.Count + Gloves.Count; } }

    public static bool OverBudget
    {
        get
        {
            return Thumbs.Count > MaxThumbs || Cuts.Count > MaxCuts || Heads.Count > MaxHeads || Fulls.Count > MaxFulls
                || Bodies.Count > MaxBodies || Bats.Count > MaxProps || Gloves.Count > MaxProps;
        }
    }

    // 上限を超えた分を手放す。unload=true なら参照の切れた画像をメモリから消す(画面の切り替え時など)。
    public static int TrimArt(bool unload)
    {
        int n = 0;
        if (OverBudget)
        {
            var used = UsedTextures();
            n += TrimKind(Thumbs, "Thumb", MaxThumbs, used);
            n += TrimKind(Cuts, "Cut", MaxCuts, used);
            n += TrimKind(Heads, "Head", MaxHeads, used);
            n += TrimKind(Fulls, "Full", MaxFulls, used);
            n += TrimKind(Bodies, "Body", MaxBodies, used);
            n += TrimKind(Bats, "Bat", MaxProps, used);
            n += TrimKind(Gloves, "Glove", MaxProps, used);
            Released += n;
        }
        if (unload && n > 0) Resources.UnloadUnusedAssets();
        return n;
    }

    private static HashSet<Texture> UsedTextures()
    {
        var set = new HashSet<Texture>();
        foreach (var im in Object.FindObjectsOfType<UnityEngine.UI.Image>()) if (im.sprite != null) set.Add(im.sprite.texture);
        foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>()) if (sr.sprite != null) set.Add(sr.sprite.texture);
        foreach (var mr in Object.FindObjectsOfType<MeshRenderer>())
        {
            var m = mr.sharedMaterial;
            if (m != null && m.HasProperty("_MainTex") && m.mainTexture != null) set.Add(m.mainTexture);
        }
        return set;
    }

    private static readonly List<string> _trimKeys = new List<string>();
    private static int TrimKind(Dictionary<string, Sprite> dict, string kind, int max, HashSet<Texture> used)
    {
        if (dict.Count <= max) return 0;
        _trimKeys.Clear();
        _trimKeys.AddRange(dict.Keys);
        _trimKeys.Sort((a, b) => Last(kind, a).CompareTo(Last(kind, b)));
        int removed = 0;
        for (int i = 0; i < _trimKeys.Count && dict.Count > max; i++)
        {
            var s = dict[_trimKeys[i]];
            if (s != null && used.Contains(s.texture)) continue;   // 表示中は守る
            dict.Remove(_trimKeys[i]);
            LastUse.Remove(kind + "/" + _trimKeys[i]);
            removed++;
        }
        return removed;
    }

    private static float Last(string kind, string id)
    {
        float t;
        return LastUse.TryGetValue(kind + "/" + id, out t) ? t : 0f;
    }
}
