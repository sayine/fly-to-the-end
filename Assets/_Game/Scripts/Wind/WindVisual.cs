using UnityEngine;

namespace Game.Wind
{
    /// <summary>
    /// Makes a wind zone readable (SPEC: "If a player can't see the wind, the game fails").
    /// Streaks are emitted from the zone's source and travel its exact cone over their lifetime, so they show
    /// both direction and reach. Thermals use slower, thicker warm streaks. Vertical zones also get a soft
    /// ring on the floor under them. Configured in code from the zone shape and WindConfig (no Inspector setup).
    /// </summary>
    [RequireComponent(typeof(WindVolume), typeof(ParticleSystem))]
    public sealed class WindVisual : MonoBehaviour
    {
        [SerializeField] private WindConfig _config;
        [Tooltip("Optional floor ring (quad) under vertical zones.")]
        [SerializeField] private Renderer _floorMarker;
        [SerializeField, HideInInspector] private WindVolume _volume;
        [SerializeField, HideInInspector] private ParticleSystem _particles;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake() => Apply();

        /// <summary>Applies the zone shape and config to the particles and floor ring (also used by the scene builder).</summary>
        public void Apply()
        {
            if (_volume == null) _volume = GetComponent<WindVolume>();
            if (_particles == null) _particles = GetComponent<ParticleSystem>();
            if (_config == null) return;

            var kind = _volume.Kind;
            var thermal = kind == WindKind.Thermal;
            var color = _config.ColorFor(kind);
            var speed = _config.StreakSpeed * (thermal ? 0.6f : 1f);
            var lifetime = _volume.Length / speed;

            var wasPlaying = _particles.isPlaying;
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.loop = true;
            main.prewarm = true; // zone is visible from the first frame
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = thermal ? new ParticleSystem.MinMaxCurve(0.06f, 0.12f) : new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = color;
            main.maxParticles = _config.MaxParticlesPerZone;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = _volume.StartRadius;
            shape.radiusThickness = 1f;
            shape.angle = Mathf.Atan2(_volume.EndRadius - _volume.StartRadius, _volume.Length) * Mathf.Rad2Deg;
            shape.length = _volume.Length;

            var avgRadius = (_volume.StartRadius + _volume.EndRadius) * 0.5f;
            var zoneVolume = Mathf.PI * avgRadius * avgRadius * _volume.Length;
            var emission = _particles.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Min(_config.StreakDensity * zoneVolume, _config.MaxParticlesPerZone / lifetime);

            var fade = _particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = thermal ? 0.2f : 0.12f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (_floorMarker != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor(ColorId, new Color(color.r, color.g, color.b, _config.FloorMarkerAlpha));
                _floorMarker.SetPropertyBlock(block);
            }

            if (wasPlaying || Application.isPlaying) _particles.Play(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_volume == null) _volume = GetComponent<WindVolume>();
            if (_particles == null) _particles = GetComponent<ParticleSystem>();
        }
#endif
    }
}
