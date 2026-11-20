using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SRSeasons
{
    // Snowfall in winter: a particle box that follows the camera, so it snows wherever the player is (and costs
    // the same everywhere). It stops in caves and under water, and fades in and out around the season change.
    internal class Snow : MonoBehaviour
    {
        private static readonly FieldInfo caveField = AccessTools.Field(typeof(AmbianceDirector), "caveDarkness");
        private static readonly FieldInfo waterField = AccessTools.Field(typeof(AmbianceDirector), "waterCount");
        private static readonly FieldInfo seaField = AccessTools.Field(typeof(AmbianceDirector), "seaCount");

        private const float MaxRate = 260f;

        private ParticleSystem ps;

        private void Start()
        {
            Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Main.Log("no particle shader found: no snowfall");
                enabled = false;
                return;
            }

            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.maxParticles = 3000;
            main.gravityModifier = 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // flakes stay put while the player moves
            main.startColor = new Color(1f, 1f, 1f, 0.9f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 1f, 60f);

            // A gentle sideways drift.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.25f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(shader) { mainTexture = FlakeTexture() };
            ps.Play();
        }

        private void LateUpdate()
        {
            if (ps == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            transform.position = cam.transform.position + Vector3.up * 18f;

            var emission = ps.emission;
            emission.rateOverTime = MaxRate * Strength();
        }

        // 1 in winter, 0 otherwise, blending over the last day before and after; 0 in caves and under water.
        private static float Strength()
        {
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            if (scene == null || scene.TimeDirector == null || scene.AmbianceDirector == null) return 0f;
            AmbianceDirector amb = scene.AmbianceDirector;
            if (caveField != null && (float)caveField.GetValue(amb) > 0.5f) return 0f;
            if ((waterField != null && (int)waterField.GetValue(amb) > 0) || (seaField != null && (int)seaField.GetValue(amb) > 0)) return 0f;

            if (Season.Forced.HasValue) return Season.Forced.Value == SeasonId.Winter ? 1f : 0f;
            int day = scene.TimeDirector.CurrDay();
            float fraction = scene.TimeDirector.CurrHour() / 24f;
            bool lastDay = Season.DayInSeason(day) == Season.DaysPerSeason;
            if (Season.Of(day) == SeasonId.Winter) return lastDay ? 1f - fraction : 1f; // fades out into spring
            if (lastDay && Season.Of(day + 1) == SeasonId.Winter) return fraction;      // fades in before winter
            return 0f;
        }

        private static Texture2D FlakeTexture()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
