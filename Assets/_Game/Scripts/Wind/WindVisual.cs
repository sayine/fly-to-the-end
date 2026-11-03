using UnityEngine;

namespace Game.Wind
{
    /// <summary>
    /// Makes a wind zone readable and pretty (SPEC: "If a player can't see the wind, the game fails").
    ///   Ribbons: a few comet-like wind lines with soft tapering trails that flow through the zone's exact cone
    ///            and wander with gentle noise. Thermals spiral upward; downdrafts twist slowly on the way down.
    ///   Motes:   tiny soft dust specks drifting slowly through the zone, so its volume reads at a glance.
    ///   Ring:    vertical zones get a softly pulsing ring on the floor marking their footprint.
    /// Everything is configured in code from the zone shape and WindConfig (no Inspector setup).
    /// </summary>
    [RequireComponent(typeof(WindVolume), typeof(ParticleSystem))]
    public sealed class WindVisual : MonoBehaviour
    {
        [SerializeField] private WindConfig _config;
        [Tooltip("Child particle system for the ribbons (trails).")]
        [SerializeField] private ParticleSystem _ribbons;
        [Tooltip("Optional floor ring (quad) under vertical zones.")]
        [SerializeField] private Renderer _floorMarker;
        [SerializeField, HideInInspector] private WindVolume _volume;
        [SerializeField, HideInInspector] private ParticleSystem _motes;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _block;
        private Vector3 _markerScale;
        private Color _markerColor;

        private void Awake()
        {
            Apply();
            if (_floorMarker != null) _markerScale = _floorMarker.transform.localScale;
        }

        private void Update()
        {
            if (_floorMarker == null || _config == null || _block == null) return;
            var wave = Mathf.Sin(Time.time * _config.FloorMarkerPulse * Mathf.PI * 2f);
            _block.SetColor(ColorId, new Color(_markerColor.r, _markerColor.g, _markerColor.b, _markerColor.a * (0.7f + 0.3f * wave)));
            _floorMarker.SetPropertyBlock(_block);
            _floorMarker.transform.localScale = _markerScale * (1f + 0.04f * wave);
        }

        /// <summary>Applies the zone shape and config to both particle layers and the floor ring (also used by the scene builder).</summary>
        public void Apply()
        {
            if (_volume == null) _volume = GetComponent<WindVolume>();
            if (_motes == null) _motes = GetComponent<ParticleSystem>();
            if (_config == null) return;

            var kind = _volume.Kind;
            var color = _config.ColorFor(kind);
            var speed = _config.StreakSpeed * (kind == WindKind.Thermal ? 0.7f : 1f);
            var lifetime = _volume.Length / speed;
            var avgRadius = (_volume.StartRadius + _volume.EndRadius) * 0.5f;

            // Motes: slow, small, many, faint.
            ConfigureCommon(_motes, lifetime * 1.6f, speed * 0.6f, color.WithAlphaScale(0.55f), _config.MaxParticlesPerZone);
            var motesMain = _motes.main;
            motesMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            var zoneVolume = Mathf.PI * avgRadius * avgRadius * _volume.Length;
            var motesEmission = _motes.emission;
            motesEmission.rateOverTime = Mathf.Min(_config.StreakDensity * zoneVolume, _config.MaxParticlesPerZone / (lifetime * 1.6f));
            var twinkle = _motes.sizeOverLifetime;
            twinkle.enabled = kind == WindKind.Thermal;
            if (twinkle.enabled)
                twinkle.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.3f), new Keyframe(0.25f, 1.2f), new Keyframe(0.5f, 0.5f), new Keyframe(0.75f, 1.2f), new Keyframe(1f, 0.3f)));
            var motesRenderer = _motes.GetComponent<ParticleSystemRenderer>();
            motesRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            if (_ribbons != null) ConfigureRibbons(kind, color, speed, lifetime, avgRadius);

            if (_floorMarker != null)
            {
                _block ??= new MaterialPropertyBlock();
                _markerColor = new Color(color.r, color.g, color.b, _config.FloorMarkerAlpha);
                _block.SetColor(ColorId, _markerColor);
                _floorMarker.SetPropertyBlock(_block);
            }

            if (Application.isPlaying)
            {
                _motes.Play(true);
            }
        }

        private void ConfigureRibbons(WindKind kind, Color color, float speed, float lifetime, float avgRadius)
        {
            const int maxRibbons = 45;
            ConfigureCommon(_ribbons, lifetime, speed, color, maxRibbons);
            var main = _ribbons.main;
            main.startSize = new ParticleSystem.MinMaxCurve(_config.RibbonWidth * 0.7f, _config.RibbonWidth);
            var emission = _ribbons.emission;
            emission.rateOverTime = Mathf.Min(_config.RibbonRate * avgRadius, maxRibbons / lifetime);

            var noise = _ribbons.noise;
            noise.enabled = _config.RibbonWander > 0f;
            noise.strength = _config.RibbonWander;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.25f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low; // mobile

            var orbit = _ribbons.velocityOverLifetime;
            var spin = kind == WindKind.Thermal ? _config.ThermalSpin : kind == WindKind.Downdraft ? _config.ThermalSpin * 0.5f : 0f;
            orbit.enabled = spin > 0f;
            if (orbit.enabled)
            {
                orbit.space = ParticleSystemSimulationSpace.Local;
                orbit.x = orbit.y = orbit.z = new ParticleSystem.MinMaxCurve(0f);
                orbit.orbitalX = orbit.orbitalY = new ParticleSystem.MinMaxCurve(0f);
                orbit.orbitalZ = new ParticleSystem.MinMaxCurve(spin); // around the zone axis (local Z)
            }

            var trails = _ribbons.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = _config.RibbonTrail;
            trails.minVertexDistance = 0.08f;
            trails.worldSpace = false;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var tail = new Gradient();
            tail.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = tail;

            var renderer = _ribbons.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard; // the bright comet head
            if (Application.isPlaying) _ribbons.Play(true);
        }

        /// <summary>Settings shared by both layers: looped, prewarmed, local space, cone = the zone, fade in/out.</summary>
        private void ConfigureCommon(ParticleSystem ps, float lifetime, float speed, Color color, int maxParticles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true; // zone is visible from the first frame
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startColor = color;
            main.maxParticles = maxParticles;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = _volume.StartRadius;
            shape.radiusThickness = 1f;
            shape.angle = Mathf.Atan2(_volume.EndRadius - _volume.StartRadius, _volume.Length) * Mathf.Rad2Deg;
            shape.length = _volume.Length;

            var emission = ps.emission;
            emission.enabled = true;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_volume == null) _volume = GetComponent<WindVolume>();
            if (_motes == null) _motes = GetComponent<ParticleSystem>();
        }
#endif
    }

    internal static class ColorExtensions
    {
        public static Color WithAlphaScale(this Color c, float scale) => new Color(c.r, c.g, c.b, c.a * scale);
    }
}
