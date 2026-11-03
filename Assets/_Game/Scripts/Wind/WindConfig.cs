using UnityEngine;

namespace Game.Wind
{
    public enum WindKind
    {
        Fan,       // horizontal push (floor fan, open window)
        Thermal,   // updraft (radiator, sunny spot)
        Downdraft, // push down (AC vent)
    }

    /// <summary>
    /// Wind tuning (SPEC "Room and wind"). Wind never crashes the plane: it only adds acceleration.
    /// Strengths are accelerations (m/s²) at the source; they fade along the zone and toward its edge.
    /// Visual settings make every zone readable: flowing ribbons and dust motes inside the exact zone shape.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Configs/Wind Config", fileName = "WindConfig")]
    public sealed class WindConfig : ScriptableObject
    {
        [Header("Strength (m/s² at the source)")]
        [Tooltip("Multiplier on every zone. 0 turns wind off.")]
        [SerializeField, Range(0f, 3f)] private float _globalStrength = 1f;
        [SerializeField, Range(0f, 15f)] private float _fanStrength = 6.5f;
        [SerializeField, Range(0f, 15f)] private float _thermalStrength = 5f;
        [SerializeField, Range(0f, 15f)] private float _downdraftStrength = 5.5f;

        [Header("Falloff")]
        [Tooltip("Strength along the zone = (1 - t)^this, t = 0 at the source, 1 at the end. <1 keeps it strong for longer.")]
        [SerializeField, Range(0.1f, 3f)] private float _lengthFalloff = 0.5f;
        [Tooltip("Fraction of the radius over which strength fades to 0 at the edge (soft border).")]
        [SerializeField, Range(0.05f, 1f)] private float _edgeSoftness = 0.4f;

        [Header("Turbulence")]
        [Tooltip("Strength varies by ±this fraction (Perlin noise over time and position).")]
        [SerializeField, Range(0f, 1f)] private float _turbulence = 0.3f;
        [SerializeField, Range(0.1f, 5f)] private float _turbulenceFrequency = 1.2f;

        [Header("Visuals: ribbons (flowing wind lines)")]
        [Tooltip("Ribbon speed (m/s) along the zone. Visual only.")]
        [SerializeField, Range(0.5f, 10f)] private float _streakSpeed = 3.2f;
        [Tooltip("Ribbons per second per metre of zone radius.")]
        [SerializeField, Range(0.5f, 20f)] private float _ribbonRate = 6f;
        [SerializeField, Range(0.01f, 0.4f)] private float _ribbonWidth = 0.15f;
        [Tooltip("Trail length as a fraction of the ribbon's life.")]
        [SerializeField, Range(0.05f, 1f)] private float _ribbonTrail = 0.5f;
        [Tooltip("Noise wander of the ribbons (m).")]
        [SerializeField, Range(0f, 2f)] private float _ribbonWander = 0.35f;
        [Tooltip("Spiral speed (rad/s) of thermal ribbons around the column; downdrafts twist at half.")]
        [SerializeField, Range(0f, 5f)] private float _thermalSpin = 1.4f;

        [Header("Visuals: dust motes and floor ring")]
        [Tooltip("Motes per second per m³ of zone, clamped by MaxParticles.")]
        [SerializeField, Range(0.1f, 10f)] private float _streakDensity = 1.2f;
        [SerializeField, Range(10, 300)] private int _maxParticlesPerZone = 60;
        [SerializeField] private Color _fanColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] private Color _thermalColor = new Color(1f, 0.55f, 0.2f, 0.9f);
        [SerializeField] private Color _downdraftColor = new Color(0.5f, 0.75f, 1f, 0.8f);
        [Tooltip("Floor-ring alpha under vertical zones.")]
        [SerializeField, Range(0f, 1f)] private float _floorMarkerAlpha = 0.45f;
        [Tooltip("Floor-ring pulse speed (cycles per second).")]
        [SerializeField, Range(0f, 3f)] private float _floorMarkerPulse = 0.6f;
        [Tooltip("Fan blade spin (deg/s). Visual only.")]
        [SerializeField, Range(0f, 2000f)] private float _bladeSpeed = 900f;

        public float GlobalStrength => _globalStrength;
        public float LengthFalloff => _lengthFalloff;
        public float EdgeSoftness => _edgeSoftness;
        public float Turbulence => _turbulence;
        public float TurbulenceFrequency => _turbulenceFrequency;
        public float StreakSpeed => _streakSpeed;
        public float StreakDensity => _streakDensity;
        public int MaxParticlesPerZone => _maxParticlesPerZone;
        public float RibbonRate => _ribbonRate;
        public float RibbonWidth => _ribbonWidth;
        public float RibbonTrail => _ribbonTrail;
        public float RibbonWander => _ribbonWander;
        public float ThermalSpin => _thermalSpin;
        public float FloorMarkerPulse => _floorMarkerPulse;
        public float FloorMarkerAlpha => _floorMarkerAlpha;
        public float BladeSpeed => _bladeSpeed;

        public float Strength(WindKind kind) => _globalStrength * kind switch
        {
            WindKind.Thermal => _thermalStrength,
            WindKind.Downdraft => _downdraftStrength,
            _ => _fanStrength,
        };

        public Color ColorFor(WindKind kind) => kind switch
        {
            WindKind.Thermal => _thermalColor,
            WindKind.Downdraft => _downdraftColor,
            _ => _fanColor,
        };
    }
}
