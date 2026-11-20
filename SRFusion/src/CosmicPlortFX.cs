using UnityEngine;

namespace SRFusion
{
    // The cosmic plort's look, built on each plort when it appears in the world (stored plorts are just counts,
    // so they cost nothing):
    //   - its colors drift slowly between violet, cyan and magenta over a dark core (material property block,
    //     so the shared material is never modified);
    //   - sparkles orbit around it and trail behind when it moves;
    //   - a soft glow pulses in the current color.
    internal class CosmicPlortFX : MonoBehaviour
    {
        private static readonly int TopId = Shader.PropertyToID("_TopColor");
        private static readonly int MiddleId = Shader.PropertyToID("_MiddleColor");
        private static readonly int BottomId = Shader.PropertyToID("_BottomColor");
        private static readonly Color Core = new Color(0.07f, 0.03f, 0.16f, 1f);

        private static Material sparkMaterial;

        private Renderer[] bodies;
        private MaterialPropertyBlock block;
        private Light glow;
        private float phase;

        private void Start()
        {
            phase = Random.value * 20f;
            block = new MaterialPropertyBlock();
            bodies = GetComponentsInChildren<MeshRenderer>(true);
            glow = BuildGlow();
            BuildSparkles();
        }

        private void Update()
        {
            float t = Time.time + phase;
            // Hue swings between cyan (0.5) and magenta (0.92), never through warm colors.
            float hue = 0.71f + 0.21f * Mathf.Sin(t * 0.45f);
            Color a = Color.HSVToRGB(hue, 0.85f, 1f);
            Color b = Color.HSVToRGB(Mathf.Repeat(hue + 0.18f, 1f), 0.9f, 1f);

            foreach (Renderer r in bodies)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(TopId, a);
                block.SetColor(MiddleId, Core);
                block.SetColor(BottomId, b);
                r.SetPropertyBlock(block);
            }

            if (glow != null)
            {
                glow.color = a;
                glow.intensity = 1.1f + 0.5f * Mathf.Sin(t * 2.6f);
            }
        }

        private Light BuildGlow()
        {
            var go = new GameObject("CosmicGlow");
            go.transform.SetParent(transform, false);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 2.4f;
            l.shadows = LightShadows.None;
            return l;
        }

        private void BuildSparkles()
        {
            Material mat = SparkMaterial();
            if (mat == null) return; // no particle shader available: the plort keeps its color and glow

            var go = new GameObject("CosmicSparkles");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // trail behind when thrown
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.95f, 1f), new Color(1f, 0.55f, 1f));

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            // Fade in, sparkle, fade out.
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        // One shared additive-looking material with a 4-point star texture drawn in code.
        private static Material SparkMaterial()
        {
            if (sparkMaterial != null) return sparkMaterial;
            Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Main.Log("no particle shader found: cosmic plorts have no sparkles");
                return null;
            }
            sparkMaterial = new Material(shader) { mainTexture = StarTexture() };
            return sparkMaterial;
        }

        private static Texture2D StarTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f, dy = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                    // A soft dot plus two thin rays: a twinkling star.
                    float dot = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                    float rays = Mathf.Clamp01(1f - dx * 8f) * Mathf.Clamp01(1f - dy) + Mathf.Clamp01(1f - dy * 8f) * Mathf.Clamp01(1f - dx);
                    float a = Mathf.Clamp01(dot + rays * 0.8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
