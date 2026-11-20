using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Dark glass theme for every menu, in two layers:
    //
    // 1. The style sheet. Most menu elements carry a *Styler (ButtonStyler, PanelStyler, TextStyler...) that pulls
    //    its look from UIStyleDirector when it is enabled. Rewriting the style definitions in memory once restyles
    //    every menu that uses them, with no change to any menu's logic: 9-sliced backgrounds become the rounded
    //    dark surface (keeping a hint of their hue), buttons switch to color tints for hover/press, dark text
    //    becomes light.
    // 2. Leftovers. MenuThemer, attached to each menu (BaseUI subclasses, main menu, pause menu), applies the same
    //    rules to elements that have no Styler. Icons and illustrations (non-sliced images) are never touched.
    internal static class MenuTheme
    {
        private static readonly string[] BackgroundSprites = { "bgSprite", "menuBgSprite", "itemBgSprite" };
        private static readonly string[] SwapSprites = { "highlightedSprite", "pressedSprite", "disabledSprite" };

        private static bool styleSheetDone;

        internal static void Apply(Harmony harmony)
        {
            Postfix(harmony, typeof(BaseUI), "Awake");
            Postfix(harmony, typeof(MainMenuUI), "Awake");
            Postfix(harmony, typeof(PauseMenu), "Awake");
        }

        private static void Postfix(Harmony harmony, Type type, string method)
        {
            MethodInfo target = AccessTools.Method(type, method);
            if (target == null)
            {
                Main.Log(type.Name + "." + method + " not found, that menu keeps its original look");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MenuTheme), "AttachThemer"));
        }

        private static void AttachThemer(MonoBehaviour __instance)
        {
            if (__instance == null || __instance.GetComponent<MenuThemer>() != null) return;
            __instance.gameObject.AddComponent<MenuThemer>();
        }

        // ---- Layer 1: style sheet ----------------------------------------------------------------------------

        // Called every frame until UIStyleDirector exists; rewrites it once.
        internal static void TickStyleSheet()
        {
            if (styleSheetDone) return;
            UIStyleDirector dir;
            try
            {
                dir = UIStyleDirector.Instance;
            }
            catch (Exception)
            {
                return; // not loadable yet
            }
            if (dir == null) return;
            styleSheetDone = true;

            int styles = 0;
            foreach (FieldInfo f in typeof(UIStyleDirector).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Array list = f.GetValue(dir) as Array;
                if (list == null) continue;
                foreach (object style in list)
                {
                    if (style == null) continue;
                    try
                    {
                        Retheme(style);
                        styles++;
                    }
                    catch (Exception e)
                    {
                        Main.Log("could not retheme a " + style.GetType().Name + ": " + e.Message);
                    }
                }
            }
            Main.Log("menu style sheet themed: " + styles + " styles");
            RefreshExisting();
        }

        // Menus already on screen when the sheet was rewritten (the main menu, at launch) applied the old styles
        // and woke up before the Awake hooks existed: re-apply every active Styler and attach the themer to them.
        private static void RefreshExisting()
        {
            int restyled = 0, menus = 0;
            foreach (Type t in typeof(ButtonStyler).Assembly.GetTypes())
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(t) || !t.Name.EndsWith("Styler", StringComparison.Ordinal)) continue;
                MethodInfo apply;
                try
                {
                    apply = AccessTools.Method(t, "ApplyStyle", Type.EmptyTypes);
                }
                catch (Exception)
                {
                    continue;
                }
                if (apply == null) continue;
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(t))
                {
                    try
                    {
                        apply.Invoke(o, null);
                        restyled++;
                    }
                    catch (Exception)
                    {
                        // a styler that cannot re-apply now keeps its look until it is next enabled
                    }
                }
            }
            foreach (Type t in new[] { typeof(BaseUI), typeof(MainMenuUI), typeof(PauseMenu) })
            {
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(t))
                {
                    AttachThemer((MonoBehaviour)o);
                    menus++;
                }
            }
            Main.Log("re-applied " + restyled + " styled elements, themer on " + menus + " open menus");
        }

        private static void Retheme(object style)
        {
            Type t = style.GetType();
            // Styles whose background IS the clickable element (tinted on hover). Scrollbars and sliders also have
            // tints, but for their handle: their background is a plain track.
            bool isSelectable = t.GetField("normalTint") != null
                && !(style is UIStyleDirector.ScrollbarStyle) && !(style is UIStyleDirector.SliderStyle);
            bool replacedBg = false;

            foreach (string name in BackgroundSprites)
            {
                var sprite = Get<UIStyleDirector.SpriteSetting>(style, name);
                if (sprite != null && sprite.apply && IsFrame(sprite.value))
                {
                    sprite.value = Theme.Rounded;
                    replacedBg = true;
                    // Matching color: white under tints (the tint carries the color), dark surface otherwise.
                    var color = Get<UIStyleDirector.ColorSetting>(style, name.Replace("Sprite", "Color"));
                    if (color != null)
                    {
                        color.value = isSelectable && name == "bgSprite" ? Color.white : Theme.DarkSurface(color.apply ? color.value : Color.black);
                        color.apply = true;
                    }
                }
            }

            if (isSelectable && replacedBg)
            {
                SetColor(style, "normalTint", Theme.ButtonIdle);
                SetColor(style, "highlightedTint", Theme.ButtonHover);
                SetColor(style, "pressedTint", Theme.ButtonPressed);
                SetColor(style, "disabledTint", Theme.ButtonDisabled);
                // Sprite swaps would bring the old art back on hover: tint instead.
                foreach (string name in SwapSprites)
                {
                    var swap = Get<UIStyleDirector.SpriteSetting>(style, name);
                    if (swap != null) swap.apply = false;
                }
                var transition = Get<UIStyleDirector.TransitionSetting>(style, "transition");
                if (transition != null)
                {
                    transition.apply = true;
                    transition.value = Selectable.Transition.ColorTint;
                }
            }

            // Selected state of tabs and toggle buttons (vanilla: a gold overlay, which turned olive on dark).
            var selectedSprite = Get<UIStyleDirector.SpriteSetting>(style, "selectedSprite");
            if (selectedSprite != null && selectedSprite.apply && IsFrame(selectedSprite.value))
            {
                selectedSprite.value = Theme.Rounded;
            }
            SetColor(style, "selectedColor", Theme.Selected);

            // Checkmarks: designed dark for light boxes, invisible on dark ones.
            SetColor(style, "markColor", Theme.Mark);

            var text = Get<UIStyleDirector.ColorSetting>(style, "textColor");
            if (text != null && text.apply) text.value = Theme.LightText(text.value);

            // Light outlines were drawn around dark text; on light text they become a soft dark edge.
            var outline = Get<UIStyleDirector.ColorSetting>(style, "outlineColor");
            if (outline != null && outline.apply && Theme.Luminance(outline.value) > 0.6f)
            {
                outline.value = new Color(0f, 0f, 0f, outline.value.a * 0.6f);
            }
        }

        // Sets a color setting if the style has that field (no-op otherwise).
        private static void SetColor(object style, string field, Color value)
        {
            var c = Get<UIStyleDirector.ColorSetting>(style, field);
            if (c == null) return;
            c.apply = true;
            c.value = value;
        }

        private static T Get<T>(object style, string field) where T : class
        {
            FieldInfo f = style.GetType().GetField(field);
            return f != null ? f.GetValue(style) as T : null;
        }

        // ---- Shared rules ------------------------------------------------------------------------------------

        // A frame/panel sprite is 9-sliced (has borders); icons and illustrations are not.
        internal static bool IsFrame(Sprite sprite)
        {
            return sprite != null && sprite.border != Vector4.zero && sprite != Theme.Rounded;
        }
    }

    // Per-menu pass for elements without a Styler. Runs when the menu appears, then every second while it is
    // open to catch rows the menu adds later (lists, shops). Uses reusable lists: no per-pass garbage.
    internal class MenuThemer : MonoBehaviour
    {
        private const float RescanSeconds = 1f;

        private readonly HashSet<int> done = new HashSet<int>();
        private readonly List<Image> images = new List<Image>();
        private readonly List<Selectable> selectables = new List<Selectable>();
        private readonly List<TMPro.TMP_Text> tmpTexts = new List<TMPro.TMP_Text>();
        private readonly List<Text> texts = new List<Text>();
        private float nextScan;
        private bool logged;

        private void OnEnable()
        {
            nextScan = 0f;
        }

        private void Update()
        {
            if (!Main.Config.ThemeMenus || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + RescanSeconds;
            try
            {
                int changed = Pass();
                if (!logged && changed > 0)
                {
                    logged = true;
                    Main.Log("themed menu " + name + " (" + changed + " elements without a style)");
                }
            }
            catch (Exception e)
            {
                enabled = false;
                Main.Log("theming " + name + " failed, left as is: " + e.Message);
            }
        }

        private int Pass()
        {
            int changed = 0;

            GetComponentsInChildren(true, selectables);
            foreach (Selectable s in selectables)
            {
                if (!done.Add(s.GetInstanceID()) || HasStyler(s.gameObject)) continue;
                Image img = s.targetGraphic as Image;
                if (img == null || !MenuTheme.IsFrame(img.sprite)) continue;
                img.sprite = Theme.Rounded;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                ColorBlock cb = s.colors;
                cb.normalColor = Theme.ButtonIdle;
                cb.highlightedColor = Theme.ButtonHover;
                cb.selectedColor = Theme.ButtonHover;
                cb.pressedColor = Theme.ButtonPressed;
                cb.disabledColor = Theme.ButtonDisabled;
                cb.colorMultiplier = 1f;
                s.colors = cb;
                s.transition = Selectable.Transition.ColorTint;
                done.Add(img.GetInstanceID());
                changed++;
            }

            GetComponentsInChildren(true, images);
            foreach (Image img in images)
            {
                if (!done.Add(img.GetInstanceID())) continue;
                if (MenuTheme.IsFrame(img.sprite))
                {
                    if (HasStyler(img.gameObject)) continue;
                    img.sprite = Theme.Rounded;
                    img.type = Image.Type.Sliced;
                    img.color = Theme.DarkSurface(img.color);
                    changed++;
                }
                else if (IsDarkGlyph(img))
                {
                    // A small icon tinted dark for a light background (dropdown arrow, globe, checkmark).
                    Color light = Theme.TextOnDark;
                    light.a = img.color.a;
                    img.color = light;
                    changed++;
                }
            }

            GetComponentsInChildren(true, tmpTexts);
            foreach (TMPro.TMP_Text t in tmpTexts)
            {
                if (!done.Add(t.GetInstanceID()) || HasStyler(t.gameObject)) continue;
                Color c = Theme.LightText(t.color);
                if (c != t.color) { t.color = c; changed++; }
            }

            GetComponentsInChildren(true, texts);
            foreach (Text t in texts)
            {
                if (!done.Add(t.GetInstanceID()) || HasStyler(t.gameObject)) continue;
                Color c = Theme.LightText(t.color);
                if (c != t.color) { t.color = c; changed++; }
            }
            return changed;
        }

        // Dark tint + small + mostly opaque: a monochrome glyph. Illustrations are drawn with a white tint, and
        // soft shadows are large or faint, so neither matches.
        private static bool IsDarkGlyph(Image img)
        {
            if (img.sprite == null || img.color.a < 0.6f || Theme.Luminance(img.color) >= 0.3f) return false;
            Rect r = img.rectTransform.rect;
            return r.width <= 64f && r.height <= 64f;
        }

        // Elements with a *Styler already follow the themed style sheet.
        private static bool HasStyler(GameObject go)
        {
            foreach (MonoBehaviour b in go.GetComponents<MonoBehaviour>())
            {
                if (b != null && b.GetType().Name.EndsWith("Styler", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
