using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRSeasons
{
    // A small dark glass pill in the top-left corner (under SR HUD's FPS counter): "● Printemps · jour 3/7".
    // Follows the vanilla HUD's visibility (hidden in menus). Text rebuilt only when the day changes.
    internal class SeasonPill : MonoBehaviour
    {
        private HudUI hud;
        private CanvasGroup group;
        private TextMeshProUGUI label;
        private Image dot;
        private int shownDay = int.MinValue;
        private SeasonId shownSeason;

        internal void Init(HudUI vanilla)
        {
            hud = vanilla;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var pill = Child("Pill").gameObject.AddComponent<Image>();
            pill.sprite = Rounded();
            pill.type = Image.Type.Sliced;
            pill.color = new Color(0.055f, 0.063f, 0.082f, 0.62f);
            pill.raycastTarget = false;
            var prt = pill.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(24f, -56f);
            prt.sizeDelta = new Vector2(190f, 30f);

            dot = Child("Dot", prt).gameObject.AddComponent<Image>();
            dot.sprite = Rounded();
            dot.type = Image.Type.Sliced;
            dot.pixelsPerUnitMultiplier = 4f;
            dot.raycastTarget = false;
            var drt = dot.rectTransform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0f, 0.5f);
            drt.anchoredPosition = new Vector2(12f, 0f);
            drt.sizeDelta = new Vector2(10f, 10f);

            label = Child("Text", prt).gameObject.AddComponent<TextMeshProUGUI>();
            if (vanilla.dayText != null) label.font = vanilla.dayText.font as TMP_FontAsset;
            label.fontSize = 15f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, 1f, 1f, 0.95f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(30f, 0f);
            lrt.offsetMax = new Vector2(-10f, 0f);
        }

        private RectTransform Child(string name, Transform parent = null)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent != null ? parent : transform, false);
            return rt;
        }

        private void Update()
        {
            if (hud == null) { Destroy(gameObject); return; }
            Map map = SRSingleton<Map>.Instance;
            bool mapOpen = map != null && map.mapUI != null && map.mapUI.gameObject.activeInHierarchy; // it would cover the map title
            group.alpha = hud.uiContainer != null && hud.uiContainer.activeInHierarchy && !mapOpen ? 1f : 0f;

            int day = SRSingleton<SceneContext>.Instance.TimeDirector.CurrDay();
            SeasonId s = Season.Of(day);
            if (day == shownDay && s == shownSeason) return; // also refreshes when the season command forces one
            shownDay = day;
            shownSeason = s;
            dot.color = Season.Accent(s);
            label.text = Season.Name(s) + "  <alpha=#99>jour " + Season.DayInSeason(day) + "/" + Season.DaysPerSeason;
            var prt = (RectTransform)label.transform.parent;
            prt.sizeDelta = new Vector2(label.GetPreferredValues(label.text).x + 44f, 30f);
        }

        private static Sprite rounded;

        private static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }
    }
}
