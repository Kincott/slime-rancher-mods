using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MonomiPark.SlimeRancher.Regions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRPing
{
    // Pings on the map screen. The map places its own markers in MapUI.mapMarkerSection, at a position computed
    // by MapUI.GetMapPosClamped(worldPos, coefficients, min, max), with one set of coefficients and bounds for the
    // main world and one for the Glass Desert. A ping pin is added to the same section with the same math, so it
    // lands exactly where the game would put a marker for that spot. The section zooms with the map, so the pin
    // is counter-scaled each frame to keep a constant size on screen.
    internal partial class PingView
    {
        private static readonly MethodInfo mapPosMethod = AccessTools.Method(typeof(MapUI), "GetMapPosClamped");
        private static readonly FieldInfo mainCoefField = AccessTools.Field(typeof(MapUI), "mainCoefficients");
        private static readonly FieldInfo desertCoefField = AccessTools.Field(typeof(MapUI), "desertCoefficients");
        private static readonly FieldInfo mainMinField = AccessTools.Field(typeof(MapUI), "worldMarkerPositionMin");
        private static readonly FieldInfo mainMaxField = AccessTools.Field(typeof(MapUI), "worldMarkerPositionMax");
        private static readonly FieldInfo desertMinField = AccessTools.Field(typeof(MapUI), "desertMarkerPositionMin");
        private static readonly FieldInfo desertMaxField = AccessTools.Field(typeof(MapUI), "desertMarkerPositionMax");

        private static bool mapWarned;

        private static MapUI OpenMapUI()
        {
            Map map = SRSingleton<Map>.Instance;
            if (map == null || map.mapUI == null || !map.mapUI.gameObject.activeInHierarchy) return null;
            return map.mapUI;
        }

        internal static bool MapOpen()
        {
            return OpenMapUI() != null;
        }

        // A spot inside a Glass Desert region uses the desert's map coordinates.
        private static bool InDesert(Vector3 pos)
        {
            try
            {
                RegionRegistry registry = SRSingleton<SceneContext>.Instance.RegionRegistry;
                var regions = new List<Region>();
                registry.GetContaining(RegionRegistry.RegionSetId.DESERT, ref regions, pos);
                return regions.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void PlaceMapMarkers()
        {
            MapUI mapUI = OpenMapUI();
            if (mapUI == null)
            {
                foreach (Ping p in pings) if (p.mapRoot != null && p.mapRoot.gameObject.activeSelf) p.mapRoot.gameObject.SetActive(false);
                return;
            }
            if (pings.Count == 0 || mapUI.mapMarkerSection == null || mapPosMethod == null) return;

            try
            {
                Transform section = mapUI.mapMarkerSection.transform;
                Camera cam = Camera.main;
                // Counter the map zoom: the pin keeps the size it would have directly on the map's canvas.
                Canvas root = mapUI.GetComponentInParent<Canvas>();
                float rootScale = root != null ? root.rootCanvas.transform.lossyScale.x : 1f;
                float sectionScale = section.lossyScale.x;
                float scale = sectionScale > 1e-6f ? rootScale / sectionScale : 1f;

                foreach (Ping p in pings)
                {
                    if (p.mapRoot == null) BuildMapPin(p, section);
                    if (!p.mapRoot.gameObject.activeSelf) p.mapRoot.gameObject.SetActive(true);
                    p.mapRoot.SetAsLastSibling();

                    object[] args = p.desert
                        ? new object[] { p.pos, desertCoefField.GetValue(mapUI), desertMinField.GetValue(mapUI), desertMaxField.GetValue(mapUI) }
                        : new object[] { p.pos, mainCoefField.GetValue(mapUI), mainMinField.GetValue(mapUI), mainMaxField.GetValue(mapUI) };
                    p.mapRoot.anchoredPosition = (Vector2)mapPosMethod.Invoke(mapUI, args);
                    p.mapRoot.localScale = new Vector3(scale, scale, 1f);

                    float age = Time.time - p.born;
                    p.mapRoot.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(Main.PingSeconds - age);
                    float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f); // the map pauses the game
                    p.mapRoot.GetChild(0).localScale = new Vector3(pulse, pulse, 1f);

                    int meters = cam != null ? Mathf.RoundToInt(Vector3.Distance(cam.transform.position, p.pos)) : 0;
                    string who = "<color=#" + ColorUtility.ToHtmlStringRGB(p.color) + ">" + p.sender + "</color>  <alpha=#AA>" + meters + " m";
                    string text = p.objectName != null ? "<b>" + p.objectName + "</b>\n<size=13>" + who + "</size>" : "<size=14>" + who + "</size>";
                    if (p.mapLabel.text != text)
                    {
                        p.mapLabel.text = text;
                        Vector2 size = p.mapLabel.GetPreferredValues(text);
                        p.mapLabelBox.sizeDelta = new Vector2(size.x + 22f, size.y + 10f);
                    }
                }
            }
            catch (Exception e)
            {
                if (mapWarned) return;
                mapWarned = true;
                Main.Log("map pin failed: " + e.Message);
            }
        }

        private void BuildMapPin(Ping p, Transform section)
        {
            p.mapRoot = NewRect("SRPing pin " + p.sender, section);
            // Same anchoring as the game's own markers (copied from one), so the positions mean the same thing.
            MapMarker sample = section.GetComponentInChildren<MapMarker>(true);
            var sampleRect = sample != null ? sample.GetComponent<RectTransform>() : null;
            if (sampleRect != null)
            {
                p.mapRoot.anchorMin = sampleRect.anchorMin;
                p.mapRoot.anchorMax = sampleRect.anchorMax;
            }
            else
            {
                p.mapRoot.anchorMin = p.mapRoot.anchorMax = new Vector2(0f, 1f);
            }
            var group = p.mapRoot.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform diamond = NewImage("Diamond", p.mapRoot, Sprites.Diamond(), p.color, new Vector2(30f, 30f));
            NewImage("Core", diamond, Sprites.Diamond(), new Color(1f, 1f, 1f, 0.95f), new Vector2(11f, 11f));

            p.mapLabelBox = NewImage("LabelBox", p.mapRoot, Sprites.Rounded(), new Color(0.055f, 0.063f, 0.082f, 0.8f), new Vector2(120f, 40f));
            p.mapLabelBox.pivot = new Vector2(0.5f, 0f);
            p.mapLabelBox.anchoredPosition = new Vector2(0f, 22f);
            p.mapLabelBox.GetComponent<Image>().type = Image.Type.Sliced;

            p.mapLabel = NewRect("Label", p.mapLabelBox).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) p.mapLabel.font = font;
            p.mapLabel.fontSize = 15f;
            p.mapLabel.alignment = TextAlignmentOptions.Center;
            p.mapLabel.enableWordWrapping = false;
            p.mapLabel.raycastTarget = false;
            p.mapLabel.color = Color.white;
            RectTransform lrt = p.mapLabel.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 4f);
            lrt.offsetMax = new Vector2(-10f, -4f);
        }
    }
}
