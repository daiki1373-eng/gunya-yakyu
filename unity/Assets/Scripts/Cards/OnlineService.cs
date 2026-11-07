using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================
// OnlineService.cs
// オンライン対戦の通信(Firebase の Realtime Database と匿名ログインを REST で使う。WebGL でも動く)。
//   自分の編成を teams/<6桁の番号> に保存し、相手の番号から相手の編成を読み込む。
//   設定は Resources/online_config.json(apiKey・databaseURL)。空なら「未設定」として何もしない。
// ============================================================
public class OnlineService : MonoBehaviour
{
    [Serializable] private class Config { public string apiKey = ""; public string databaseURL = ""; }
    [Serializable] public class TeamDoc { public string owner = ""; public string name = ""; public string[] lineup; public string pitcher = ""; public long updated; }
    [Serializable] private class SignUpRes { public string idToken; public string refreshToken; public string localId; public string expiresIn; }
    [Serializable] private class RefreshRes { public string id_token; public string refresh_token; public string user_id; public string expires_in; }

    public static OnlineService I { get; private set; }
    private const string CodeKey = "gunya.online.code", TokenKey = "gunya.online.rt";
    private Config _cfg;
    private string _idToken = "", _uid = "";
    private float _tokenUntil;

    public static OnlineService Create(GameObject host)
    {
        var s = host.AddComponent<OnlineService>();
        I = s;
        var ta = Resources.Load<TextAsset>("online_config");
        s._cfg = ta != null ? JsonUtility.FromJson<Config>(ta.text) : new Config();
        if (s._cfg != null && s._cfg.databaseURL != null) s._cfg.databaseURL = s._cfg.databaseURL.TrimEnd('/');
        return s;
    }

    public bool Configured { get { return _cfg != null && !string.IsNullOrEmpty(_cfg.apiKey) && !string.IsNullOrEmpty(_cfg.databaseURL); } }
    public static string MyCode { get { return PlayerPrefs.GetString(CodeKey, ""); } }

    // ---- 匿名ログイン(初回に作り、以後は更新用の鍵で入り直す) ----
    private IEnumerator EnsureAuth(Action<string> fail)
    {
        if (!string.IsNullOrEmpty(_idToken) && Time.realtimeSinceStartup < _tokenUntil) yield break;
        string rt = PlayerPrefs.GetString(TokenKey, "");
        if (!string.IsNullOrEmpty(rt))
        {
            var form = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(rt);
            using (var req = Post("https://securetoken.googleapis.com/v1/token?key=" + _cfg.apiKey, form, "application/x-www-form-urlencoded"))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var r = JsonUtility.FromJson<RefreshRes>(req.downloadHandler.text);
                    _idToken = r.id_token; _uid = r.user_id;
                    _tokenUntil = Time.realtimeSinceStartup + 50f * 60f;
                    PlayerPrefs.SetString(TokenKey, r.refresh_token); PlayerPrefs.Save();
                    yield break;
                }
            }
        }
        using (var req = Post("https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + _cfg.apiKey, "{\"returnSecureToken\":true}", "application/json"))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { _idToken = ""; fail("つながりません"); yield break; }
            var r = JsonUtility.FromJson<SignUpRes>(req.downloadHandler.text);
            _idToken = r.idToken; _uid = r.localId;
            _tokenUntil = Time.realtimeSinceStartup + 50f * 60f;
            PlayerPrefs.SetString(TokenKey, r.refreshToken);
            PlayerPrefs.DeleteKey(CodeKey);   // 入り直した(持ち主が変わった)ので、番号は取り直す
            PlayerPrefs.Save();
        }
    }

    // ---- 自分の編成を保存し、番号を返す ----
    public IEnumerator Publish(CharaRoster.Team team, string name, Action<string> done, Action<string> fail)
    {
        if (!Configured) { fail("未設定"); yield break; }
        string err = null;
        yield return EnsureAuth(e => err = e);
        if (err != null || string.IsNullOrEmpty(_idToken)) { fail(err ?? "つながりません"); yield break; }
        var doc = new TeamDoc { owner = _uid, name = name ?? "", pitcher = team.Pitcher != null ? team.Pitcher.Id : "", updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        doc.lineup = new string[team.Lineup.Count];
        for (int i = 0; i < doc.lineup.Length; i++) doc.lineup[i] = team.Lineup[i].Id;
        string body = JsonUtility.ToJson(doc);

        string code = MyCode;
        if (!string.IsNullOrEmpty(code))
        {
            long st = 0;
            yield return Put(code, body, s => st = s);
            if (st == 200) { done(code); yield break; }
            code = "";   // 書けなかった(持ち主が違う等)ので取り直す
        }
        for (int tries = 0; tries < 8; tries++)
        {
            string c = UnityEngine.Random.Range(100000, 1000000).ToString();
            bool free = false; bool ok = true;
            using (var req = UnityWebRequest.Get(Url(c)))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) ok = false;
                else free = req.downloadHandler.text.Trim() == "null";
            }
            if (!ok) { fail("つながりません"); yield break; }
            if (!free) continue;
            long st = 0;
            yield return Put(c, body, s => st = s);
            if (st == 200)
            {
                PlayerPrefs.SetString(CodeKey, c); PlayerPrefs.Save();
                done(c);
                yield break;
            }
        }
        fail("番号を作れません");
    }

    // ---- 相手の番号から編成を読む ----
    public IEnumerator Fetch(string code, Action<TeamDoc> done, Action<string> fail)
    {
        if (!Configured) { fail("未設定"); yield break; }
        using (var req = UnityWebRequest.Get(Url(code)))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { fail("つながりません"); yield break; }
            string text = req.downloadHandler.text.Trim();
            if (text == "null" || text.Length == 0) { fail("見つかりません"); yield break; }
            TeamDoc doc = null;
            try { doc = JsonUtility.FromJson<TeamDoc>(text); } catch (Exception) { doc = null; }
            if (doc == null || doc.lineup == null || doc.lineup.Length != 9) { fail("読めません"); yield break; }
            done(doc);
        }
    }

    // 読んだ編成を試合用のチームにする(図鑑にない選手がいれば null)
    public static CharaRoster.Team ToTeam(TeamDoc doc)
    {
        var t = new CharaRoster.Team();
        foreach (var id in doc.lineup)
        {
            var d = CharaCatalog.Get(id);
            if (d == null || !CharaRoster.Fields(d)) return null;
            t.Lineup.Add(d);
        }
        var p = CharaCatalog.Get(doc.pitcher);
        t.Pitcher = p != null && CharaRoster.Role(p) == CharaRole.Pitcher ? p : CharaCatalog.Get("sleepyhead");
        return t;
    }

    private string Url(string code) { return _cfg.databaseURL + "/teams/" + code + ".json"; }

    private IEnumerator Put(string code, string body, Action<long> status)
    {
        using (var req = new UnityWebRequest(Url(code) + "?auth=" + _idToken, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            status(req.responseCode);
        }
    }

    private static UnityWebRequest Post(string url, string body, string type)
    {
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", type);
        return req;
    }
}
