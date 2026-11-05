using UnityEngine;
using UnityEngine.UI;

// 一覧では実際に見えたカードの画像だけを読む。UIの枠・名前は先に作れる。
public sealed class CardArtLoader : MonoBehaviour
{
    public enum ArtKind { Cut, Thumb, Full }
    private Image _image;
    private string _id;
    private ArtKind _kind;
    private Vector2 _inner;
    private RectTransform _viewport;
    private int _readyFrame;
    private static int _loadFrame = -1, _loadsThisFrame;
    private const int LoadsPerFrame = 4;
    private readonly Vector3[] _corners = new Vector3[4];

    public static void Attach(Image image, string id, ArtKind kind, Vector2 inner, bool lazy)
    {
        var loader = image.gameObject.AddComponent<CardArtLoader>();
        loader._image = image;
        loader._id = id;
        loader._kind = kind;
        loader._inner = inner;
        image.enabled = false;
        if (!lazy) loader.Load();
        else
        {
            var scroll = image.GetComponentInParent<ScrollRect>();
            if (scroll != null) loader._viewport = scroll.viewport;
        }
    }

    private void OnEnable() { _readyFrame = Time.frameCount + 1; }

    private void LateUpdate()
    {
        if (_image == null) { enabled = false; return; }
        // 表示直後は GridLayoutGroup が配置する前なので1フレーム待つ。
        if (Time.frameCount < _readyFrame) return;
        if (_viewport != null)
        {
            _image.rectTransform.GetWorldCorners(_corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < _corners.Length; i++)
            {
                Vector2 point = _viewport.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            Rect rect = _viewport.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            if (max.x < rect.xMin || min.x > rect.xMax || max.y < rect.yMin || min.y > rect.yMax) return;
        }
        if (_loadFrame != Time.frameCount) { _loadFrame = Time.frameCount; _loadsThisFrame = 0; }
        if (_loadsThisFrame >= LoadsPerFrame) return;
        _loadsThisFrame++;
        Load();
    }

    private void Load()
    {
        Sprite sprite = _kind == ArtKind.Cut ? CharaCatalog.Cut(_id)
            : _kind == ArtKind.Full ? CharaCatalog.Full(_id) : CharaCatalog.Thumb(_id);
        _image.sprite = sprite;
        _image.enabled = sprite != null;
        if (sprite != null && _kind != ArtKind.Cut)
        {
            float aspect = sprite.rect.width / sprite.rect.height;
            float innerAspect = _inner.x / _inner.y;
            Vector2 size = aspect > innerAspect ? new Vector2(_inner.y * aspect, _inner.y)
                : new Vector2(_inner.x, _inner.x / aspect);
            _image.rectTransform.sizeDelta = size;
            _image.rectTransform.anchoredPosition = new Vector2(0f,
                aspect > innerAspect ? 0f : -(size.y - _inner.y) * 0.15f);
        }
        enabled = false;
    }
}
