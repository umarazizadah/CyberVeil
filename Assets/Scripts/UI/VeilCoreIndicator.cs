using System.Collections;
using CyberVeil.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CyberVeil.UI
{
    /// <summary>
    /// Procedural, non-destructive blink feedback drawn over the purple HUD gem.
    /// Blink acceptance and cooldown energy always come from <see cref="PlayerDash"/>.
    /// </summary>
    [AddComponentMenu("CyberVeil/UI/Veil Core Indicator")]
    [DisallowMultipleComponent]
    public sealed class VeilCoreIndicator : MaskableGraphic
    {
        private static readonly Vector2 CoreCenter = new Vector2(0.5f, 0.5f);

        private static readonly Vector2[] CoreShape =
        {
            new Vector2(0.05f, 0.35f),
            new Vector2(0.16f, 0.13f),
            new Vector2(0.39f, 0.03f),
            new Vector2(0.68f, 0.08f),
            new Vector2(0.89f, 0.25f),
            new Vector2(0.97f, 0.52f),
            new Vector2(0.86f, 0.79f),
            new Vector2(0.63f, 0.95f),
            new Vector2(0.34f, 0.91f),
            new Vector2(0.10f, 0.70f)
        };

        private static readonly Vector2[][] CrystalPlanes =
        {
            new[]
            {
                new Vector2(0.13f, 0.36f), new Vector2(0.22f, 0.18f),
                new Vector2(0.43f, 0.10f), new Vector2(0.47f, 0.48f),
                new Vector2(0.28f, 0.74f), new Vector2(0.12f, 0.64f)
            },
            new[]
            {
                new Vector2(0.44f, 0.10f), new Vector2(0.66f, 0.15f),
                new Vector2(0.75f, 0.44f), new Vector2(0.58f, 0.83f),
                new Vector2(0.47f, 0.49f)
            },
            new[]
            {
                new Vector2(0.67f, 0.16f), new Vector2(0.85f, 0.30f),
                new Vector2(0.91f, 0.54f), new Vector2(0.79f, 0.73f),
                new Vector2(0.58f, 0.83f), new Vector2(0.75f, 0.44f)
            }
        };

        private static readonly Vector2[][] RiftPaths =
        {
            new[]
            {
                new Vector2(0.43f, 0.13f), new Vector2(0.52f, 0.34f),
                new Vector2(0.46f, 0.49f), new Vector2(0.57f, 0.66f),
                new Vector2(0.50f, 0.88f)
            },
            new[]
            {
                new Vector2(0.51f, 0.35f), new Vector2(0.67f, 0.27f),
                new Vector2(0.59f, 0.45f)
            },
            new[]
            {
                new Vector2(0.47f, 0.50f), new Vector2(0.31f, 0.60f),
                new Vector2(0.39f, 0.72f)
            }
        };

        [Header("Gameplay Source")]
        [SerializeField] private PlayerDash dashSource;

        [Header("Dimensional Palette")]
        [SerializeField] private Color voidColor = new Color(0.015f, 0.002f, 0.035f, 0.97f);
        [SerializeField] private Color shadowColor = new Color(0.055f, 0.008f, 0.12f, 0.88f);
        [SerializeField] private Color energyColor = new Color(0.49f, 0.02f, 1f, 0.72f);
        [SerializeField] private Color magentaColor = new Color(1f, 0.04f, 0.82f, 0.54f);
        [SerializeField] private Color fractureColor = new Color(0.90f, 0.78f, 1f, 0.96f);
        [SerializeField] private Color smokeColor = new Color(0.63f, 0.18f, 1f, 0.28f);
        [SerializeField] private Color glowColor = new Color(0.72f, 0.03f, 1f, 0.24f);

        [Header("Successful Blink")]
        [SerializeField, Min(0.08f)] private float blinkSeconds = 0.28f;
        [SerializeField, Range(0.25f, 0.95f)] private float collapseStrength = 0.82f;
        [SerializeField, Range(0f, 1f)] private float portalDarkness = 0.94f;
        [SerializeField, Range(0f, 2f)] private float fractureIntensity = 1.15f;
        [SerializeField, Range(0.05f, 0.5f)] private float shardTravel = 0.28f;
        [SerializeField, Range(0f, 0.2f)] private float snapRippleScale = 0.09f;

        [Header("Cooldown Energy")]
        [SerializeField, Range(0f, 0.75f)] private float cooldownMinimumEnergy = 0.22f;
        [SerializeField, Range(0f, 1f)] private float cooldownInstability = 0.32f;

        [Header("Idle Interior")]
        [SerializeField, Min(0.5f)] private float idleCycleSeconds = 3.2f;
        [SerializeField, Range(0f, 1f)] private float idleReflectionStrength = 0.18f;
        [SerializeField, Range(0f, 1f)] private float smokeStrength = 0.38f;

        [Header("Secondary Feedback")]
        [SerializeField, Min(0.03f)] private float readyGlintSeconds = 0.12f;
        [SerializeField, Min(0.03f)] private float rejectedFlickerSeconds = 0.14f;
        [SerializeField, Range(0f, 1f)] private float rejectedFlickerStrength = 0.46f;

        [Header("Timing")]
        [Tooltip("Cosmetic motion stays responsive during hit stop. Cooldown energy still comes from PlayerDash.")]
        [SerializeField] private bool useUnscaledTime = true;

        private float cooldownProgress = 1f;
        private float idlePhase;
        private float collapseAmount;
        private float portalAmount;
        private float fractureAmount;
        private float afterimageProgress = -1f;
        private float snapAmount;
        private float snapProgress;
        private float readyGlintAmount;
        private float readyGlintProgress;
        private float rejectedFlickerAmount;

        private Vector2 dashScreenDirection = Vector2.right;
        private bool hasDirectionalFeedback;

        private Coroutine blinkRoutine;
        private Coroutine idleRoutine;
        private Coroutine readyRoutine;
        private Coroutine rejectedRoutine;

        public PlayerDash DashSource => dashSource;
        public float DisplayedCooldownProgress => cooldownProgress;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            BindSource();

            cooldownProgress = dashSource != null ? dashSource.CooldownProgress : 1f;
            if (Application.isPlaying)
                idleRoutine = StartCoroutine(IdleRoutine());

            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            UnbindSource();
            StopAllCoroutines();
            blinkRoutine = null;
            idleRoutine = null;
            readyRoutine = null;
            rejectedRoutine = null;
            ResetBlinkVisuals();
            readyGlintAmount = 0f;
            readyGlintProgress = 0f;
            rejectedFlickerAmount = 0f;

            base.OnDisable();
        }

        /// <summary>
        /// Re-resolves the scene-local player after an in-place respawn. Normal scene reloads
        /// bind automatically when the prefab is enabled.
        /// </summary>
        public void RefreshSource()
        {
            UnbindSource();
            dashSource = null;
            BindSource();
            cooldownProgress = dashSource != null ? dashSource.CooldownProgress : 1f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();

            float authoritativeEnergy = Mathf.Lerp(cooldownMinimumEnergy, 1f, cooldownProgress);
            float unstableWave = (1f - cooldownProgress) * cooldownInstability
                * (0.45f + Mathf.Abs(Mathf.Sin(idlePhase * Mathf.PI * 5f)) * 0.55f);
            float visibleEnergy = Mathf.Clamp01(authoritativeEnergy - unstableWave * 0.25f);
            float reflection = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(idlePhase * Mathf.PI * 2f)), 6f)
                * idleReflectionStrength * visibleEnergy;
            float energyScale = Mathf.Max(0.06f, 1f - collapseAmount * collapseStrength);

            Color glow = glowColor;
            glow.a *= visibleEnergy * (0.55f + reflection) + fractureAmount * 0.35f + snapAmount * 0.48f;
            AddPolygon(vertexHelper, CoreShape, rect, Tint(glow), 1.015f + snapAmount * snapRippleScale);

            Color shadow = Color.Lerp(voidColor, shadowColor, visibleEnergy);
            shadow.a *= 0.82f + (1f - visibleEnergy) * 0.15f;
            AddPolygon(vertexHelper, CoreShape, rect, Tint(shadow), 0.97f);

            DrawCrystalInterior(vertexHelper, rect, visibleEnergy, reflection, energyScale);
            DrawSmokeRibbons(vertexHelper, rect, visibleEnergy, energyScale);
            DrawRestingRift(vertexHelper, rect, visibleEnergy, energyScale);

            if (portalAmount > 0f)
            {
                Color portal = voidColor;
                portal.a *= portalAmount * portalDarkness;
                AddPolygon(vertexHelper, CoreShape, rect, Tint(portal), Mathf.Lerp(0.38f, 1.0f, portalAmount));
            }

            DrawFractureFlash(vertexHelper, rect);
            DrawSpectralShards(vertexHelper, rect);
            DrawSnapRipple(vertexHelper, rect);
            DrawReadyGlint(vertexHelper, rect);

            if (rejectedFlickerAmount > 0f)
            {
                Color rejected = voidColor;
                rejected.a *= rejectedFlickerAmount * rejectedFlickerStrength;
                AddPolygon(vertexHelper, CoreShape, rect, Tint(rejected), 0.94f);
            }
        }

        private void DrawCrystalInterior(
            VertexHelper vertexHelper,
            Rect rect,
            float energy,
            float reflection,
            float energyScale)
        {
            for (int i = 0; i < CrystalPlanes.Length; i++)
            {
                float offsetPhase = idlePhase + i * 0.21f;
                float livingLight = (Mathf.Sin(offsetPhase * Mathf.PI * 2f) + 1f) * 0.5f;
                Color plane = Color.Lerp(shadowColor, i == 1 ? magentaColor : energyColor, energy);
                plane = Color.Lerp(plane, fractureColor, reflection * (i == 1 ? 0.75f : 0.38f));
                plane.a *= Mathf.Lerp(0.36f, 0.88f, energy) * Mathf.Lerp(0.88f, 1f, livingLight);
                AddPolygonTransformed(vertexHelper, CrystalPlanes[i], rect, Tint(plane), energyScale, Vector2.zero);

                Color edge = fractureColor;
                edge.a *= energy * (0.08f + livingLight * 0.16f + reflection * 0.30f);
                AddPolygonOutlineTransformed(
                    vertexHelper,
                    CrystalPlanes[i],
                    rect,
                    Tint(edge),
                    0.55f,
                    energyScale,
                    Vector2.zero);
            }
        }

        private void DrawSmokeRibbons(VertexHelper vertexHelper, Rect rect, float energy, float energyScale)
        {
            float alpha = smokeStrength * energy * (0.42f + cooldownProgress * 0.58f);
            if (alpha <= 0.001f)
                return;

            DrawSmokeRibbon(vertexHelper, rect, idlePhase, 0.24f, 0.10f, -18f, 0, alpha, energyScale);
            DrawSmokeRibbon(vertexHelper, rect, 1f - idlePhase, 0.18f, 0.17f, 31f, 1, alpha * 0.68f, energyScale);
        }

        private void DrawSmokeRibbon(
            VertexHelper vertexHelper,
            Rect rect,
            float phase,
            float radiusX,
            float radiusY,
            float rotationDegrees,
            int skipOffset,
            float alpha,
            float energyScale)
        {
            const int segmentCount = 14;
            float rotation = rotationDegrees * Mathf.Deg2Rad;
            float cosRotation = Mathf.Cos(rotation);
            float sinRotation = Mathf.Sin(rotation);
            float phaseAngle = phase * Mathf.PI * 2f;

            for (int i = 0; i < segmentCount; i++)
            {
                if ((i + skipOffset) % 5 == 0)
                    continue;

                float a0 = phaseAngle + i / (float)segmentCount * Mathf.PI * 2f;
                float a1 = phaseAngle + (i + 0.72f) / segmentCount * Mathf.PI * 2f;
                Vector2 from = EllipsePoint(a0, radiusX, radiusY, cosRotation, sinRotation);
                Vector2 to = EllipsePoint(a1, radiusX, radiusY, cosRotation, sinRotation);
                from = TransformNormalizedPoint(CoreCenter + from, energyScale, Vector2.zero);
                to = TransformNormalizedPoint(CoreCenter + to, energyScale, Vector2.zero);

                Color smoke = Color.Lerp(smokeColor, magentaColor, i / (float)segmentCount * 0.34f);
                smoke.a *= alpha * (0.48f + 0.52f * Mathf.Sin((i + 1f) / segmentCount * Mathf.PI));
                AddLine(vertexHelper, ToRect(from, rect), ToRect(to, rect), 0.65f, Tint(smoke));
            }
        }

        private void DrawRestingRift(VertexHelper vertexHelper, Rect rect, float energy, float energyScale)
        {
            Vector2[] path = RiftPaths[0];
            Color darkRift = voidColor;
            darkRift.a *= 0.78f;
            Color liveEdge = fractureColor;
            liveEdge.a *= energy * (0.13f + idleReflectionStrength * 0.22f);

            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 from = ToRect(TransformNormalizedPoint(path[i], energyScale, Vector2.zero), rect);
                Vector2 to = ToRect(TransformNormalizedPoint(path[i + 1], energyScale, Vector2.zero), rect);
                AddLine(vertexHelper, from, to, 1.35f, Tint(darkRift));
                AddLine(vertexHelper, from, to, 0.42f, Tint(liveEdge));
            }
        }

        private void DrawFractureFlash(VertexHelper vertexHelper, Rect rect)
        {
            if (fractureAmount <= 0f)
                return;

            for (int pathIndex = 0; pathIndex < RiftPaths.Length; pathIndex++)
            {
                Vector2[] path = RiftPaths[pathIndex];
                Color fracture = fractureColor;
                fracture.a *= fractureAmount * fractureIntensity * (pathIndex == 0 ? 1f : 0.72f);

                for (int pointIndex = 0; pointIndex < path.Length - 1; pointIndex++)
                {
                    AddLine(
                        vertexHelper,
                        ToRect(path[pointIndex], rect),
                        ToRect(path[pointIndex + 1], rect),
                        Mathf.Lerp(0.75f, 1.8f, fractureAmount),
                        Tint(fracture));
                }
            }
        }

        private void DrawSpectralShards(VertexHelper vertexHelper, Rect rect)
        {
            // If projection is unavailable, the collapse/void sequence remains deliberately
            // neutral instead of inventing a direction the player did not travel.
            if (afterimageProgress < 0f || !hasDirectionalFeedback)
                return;

            float progress = Mathf.Clamp01(afterimageProgress);
            float life = Mathf.Sin(progress * Mathf.PI);
            float distance = Mathf.Min(rect.width, rect.height) * shardTravel;

            for (int i = 0; i < 3; i++)
            {
                Vector2 direction = dashScreenDirection;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float stagger = i * 0.10f;
                float travel = Mathf.Clamp01((progress - stagger) / Mathf.Max(0.01f, 1f - stagger));
                Vector2 origin = rect.center + perpendicular * ((i - 1) * 3.2f);
                Vector2 tip = origin + direction * distance * travel;
                float length = Mathf.Lerp(7.5f, 3f, travel);
                float width = Mathf.Lerp(3.2f, 0.8f, travel);
                Vector2 baseCenter = tip - direction * length;

                Color shard = Color.Lerp(energyColor, fractureColor, 0.72f);
                shard.a *= life * (0.72f - i * 0.12f);
                AddTriangle(
                    vertexHelper,
                    tip,
                    baseCenter + perpendicular * width,
                    baseCenter - perpendicular * width,
                    Tint(shard));

                Color trail = magentaColor;
                trail.a *= life * (0.48f - i * 0.08f);
                AddLine(vertexHelper, origin, baseCenter, Mathf.Max(0.45f, width * 0.42f), Tint(trail));
            }
        }

        private void DrawSnapRipple(VertexHelper vertexHelper, Rect rect)
        {
            if (snapAmount <= 0f)
                return;

            Color pulse = fractureColor;
            pulse.a *= snapAmount * 0.72f;
            float scale = Mathf.Lerp(0.80f, 0.98f + snapRippleScale, snapProgress);
            AddPolygonOutlineTransformed(vertexHelper, CoreShape, rect, Tint(pulse), 1.05f, scale, Vector2.zero);
        }

        private void DrawReadyGlint(VertexHelper vertexHelper, Rect rect)
        {
            if (readyGlintAmount <= 0f)
                return;

            Vector2 direction = new Vector2(0.62f, 0.78f).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 center = rect.center + perpendicular
                * Mathf.Lerp(-rect.width * 0.30f, rect.width * 0.30f, readyGlintProgress);
            float halfLength = Mathf.Min(rect.width, rect.height) * 0.34f;
            Color glint = fractureColor;
            glint.a *= readyGlintAmount * 0.82f;
            AddLine(vertexHelper, center - direction * halfLength, center + direction * halfLength, 1.05f, Tint(glint));
        }

        private void BindSource()
        {
            if (!Application.isPlaying)
                return;

            if (dashSource == null)
                dashSource = FindFirstObjectByType<PlayerDash>();
            if (dashSource == null)
                return;

            dashSource.OnDashStarted += HandleDashStarted;
            dashSource.OnCooldownProgressChanged += HandleCooldownProgressChanged;
            dashSource.OnDashReady += HandleDashReady;
            dashSource.OnDashRejected += HandleDashRejected;
        }

        private void UnbindSource()
        {
            if (dashSource == null)
                return;

            dashSource.OnDashStarted -= HandleDashStarted;
            dashSource.OnCooldownProgressChanged -= HandleCooldownProgressChanged;
            dashSource.OnDashReady -= HandleDashReady;
            dashSource.OnDashRejected -= HandleDashRejected;
        }

        private void HandleDashStarted(Vector3 worldDirection)
        {
            ResolveScreenDirection(worldDirection);

            if (blinkRoutine != null)
                StopCoroutine(blinkRoutine);
            ResetBlinkVisuals();
            blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        private void HandleCooldownProgressChanged(float progress)
        {
            cooldownProgress = Mathf.Clamp01(progress);
            SetVerticesDirty();
        }

        private void HandleDashReady()
        {
            cooldownProgress = 1f;
            if (readyRoutine != null)
                StopCoroutine(readyRoutine);
            readyRoutine = StartCoroutine(ReadyGlintRoutine());
            SetVerticesDirty();
        }

        private void HandleDashRejected()
        {
            if (rejectedRoutine != null)
                StopCoroutine(rejectedRoutine);
            rejectedRoutine = StartCoroutine(RejectedFlickerRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.08f, blinkSeconds);

            while (elapsed < duration)
            {
                elapsed += DeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (progress < 0.22f)
                {
                    float phase = progress / 0.22f;
                    collapseAmount = EaseInCubic(phase);
                    portalAmount = phase * 0.28f;
                }
                else if (progress < 0.42f)
                {
                    float phase = (progress - 0.22f) / 0.20f;
                    collapseAmount = 1f;
                    portalAmount = Mathf.Lerp(0.28f, 1f, EaseOutCubic(phase));
                }
                else if (progress < 0.61f)
                {
                    float phase = (progress - 0.42f) / 0.19f;
                    collapseAmount = Mathf.Lerp(1f, 0.46f, EaseOutCubic(phase));
                    portalAmount = 1f - phase * 0.56f;
                    fractureAmount = Mathf.Sin(phase * Mathf.PI);
                }
                else if (progress < 0.83f)
                {
                    float phase = (progress - 0.61f) / 0.22f;
                    collapseAmount = Mathf.Lerp(0.46f, 0f, EaseOutCubic(phase));
                    portalAmount = Mathf.Lerp(0.44f, 0f, phase);
                    fractureAmount = 1f - phase;
                    afterimageProgress = phase;
                }
                else
                {
                    float phase = (progress - 0.83f) / 0.17f;
                    collapseAmount = 0f;
                    portalAmount = 0f;
                    fractureAmount = 0f;
                    afterimageProgress = Mathf.Lerp(0.75f, 1f, phase);
                    snapProgress = phase;
                    snapAmount = Mathf.Sin(phase * Mathf.PI);
                }

                SetVerticesDirty();
                yield return null;
            }

            ResetBlinkVisuals();
            blinkRoutine = null;
            SetVerticesDirty();
        }

        private IEnumerator IdleRoutine()
        {
            float duration = Mathf.Max(0.5f, idleCycleSeconds);
            while (isActiveAndEnabled)
            {
                idlePhase = Mathf.Repeat(idlePhase + DeltaTime / duration, 1f);
                SetVerticesDirty();
                yield return null;
            }

            idleRoutine = null;
        }

        private IEnumerator ReadyGlintRoutine()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.03f, readyGlintSeconds);
            while (elapsed < duration)
            {
                elapsed += DeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                readyGlintProgress = progress;
                readyGlintAmount = Mathf.Sin(progress * Mathf.PI);
                SetVerticesDirty();
                yield return null;
            }

            readyGlintAmount = 0f;
            readyGlintProgress = 0f;
            readyRoutine = null;
            SetVerticesDirty();
        }

        private IEnumerator RejectedFlickerRoutine()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.03f, rejectedFlickerSeconds);
            while (elapsed < duration)
            {
                elapsed += DeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                rejectedFlickerAmount = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 3f)) * (1f - progress);
                SetVerticesDirty();
                yield return null;
            }

            rejectedFlickerAmount = 0f;
            rejectedRoutine = null;
            SetVerticesDirty();
        }

        private void ResolveScreenDirection(Vector3 worldDirection)
        {
            hasDirectionalFeedback = false;
            Camera camera = dashSource != null && dashSource.mainCam != null ? dashSource.mainCam : Camera.main;
            if (camera == null || dashSource == null || worldDirection.sqrMagnitude < 0.0001f)
                return;

            Vector3 worldOrigin = dashSource.transform.position;
            Vector3 screenOrigin = camera.WorldToScreenPoint(worldOrigin);
            Vector3 screenEnd = camera.WorldToScreenPoint(worldOrigin + worldDirection.normalized);
            Vector2 screenDelta = new Vector2(screenEnd.x - screenOrigin.x, screenEnd.y - screenOrigin.y);
            if (screenDelta.sqrMagnitude < 0.001f)
                return;

            dashScreenDirection = screenDelta.normalized;
            hasDirectionalFeedback = true;
        }

        private void ResetBlinkVisuals()
        {
            collapseAmount = 0f;
            portalAmount = 0f;
            fractureAmount = 0f;
            afterimageProgress = -1f;
            snapAmount = 0f;
            snapProgress = 0f;
        }

        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private Color Tint(Color source)
        {
            Color tint = color;
            return new Color(source.r * tint.r, source.g * tint.g, source.b * tint.b, source.a * tint.a);
        }

        private static float EaseInCubic(float value)
        {
            return value * value * value;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        private static Vector2 EllipsePoint(
            float angle,
            float radiusX,
            float radiusY,
            float cosRotation,
            float sinRotation)
        {
            float x = Mathf.Cos(angle) * radiusX;
            float y = Mathf.Sin(angle) * radiusY;
            return new Vector2(x * cosRotation - y * sinRotation, x * sinRotation + y * cosRotation);
        }

        private static Vector2 TransformNormalizedPoint(Vector2 point, float scale, Vector2 offset)
        {
            return CoreCenter + (point - CoreCenter) * scale + offset;
        }

        private static void AddPolygon(
            VertexHelper vertexHelper,
            Vector2[] normalizedPoints,
            Rect rect,
            Color color,
            float scale)
        {
            AddPolygonTransformed(vertexHelper, normalizedPoints, rect, color, scale, Vector2.zero);
        }

        private static void AddPolygonTransformed(
            VertexHelper vertexHelper,
            Vector2[] normalizedPoints,
            Rect rect,
            Color color,
            float scale,
            Vector2 offset)
        {
            int firstVertex = vertexHelper.currentVertCount;
            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                Vector2 point = TransformNormalizedPoint(normalizedPoints[i], scale, offset);
                vertexHelper.AddVert(ToRect(point, rect), color, Vector2.zero);
            }

            for (int i = 1; i < normalizedPoints.Length - 1; i++)
                vertexHelper.AddTriangle(firstVertex, firstVertex + i, firstVertex + i + 1);
        }

        private static void AddPolygonOutlineTransformed(
            VertexHelper vertexHelper,
            Vector2[] normalizedPoints,
            Rect rect,
            Color color,
            float width,
            float scale,
            Vector2 offset)
        {
            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                Vector2 from = TransformNormalizedPoint(normalizedPoints[i], scale, offset);
                Vector2 to = TransformNormalizedPoint(normalizedPoints[(i + 1) % normalizedPoints.Length], scale, offset);
                AddLine(vertexHelper, ToRect(from, rect), ToRect(to, rect), width, color);
            }
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int firstVertex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(from - normal, color, Vector2.zero);
            vertexHelper.AddVert(from + normal, color, Vector2.zero);
            vertexHelper.AddVert(to + normal, color, Vector2.zero);
            vertexHelper.AddVert(to - normal, color, Vector2.zero);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
        }

        private static void AddTriangle(
            VertexHelper vertexHelper,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Color color)
        {
            int firstVertex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(a, color, Vector2.zero);
            vertexHelper.AddVert(b, color, Vector2.zero);
            vertexHelper.AddVert(c, color, Vector2.zero);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        }

        private static Vector2 ToRect(Vector2 normalizedPoint, Rect rect)
        {
            return new Vector2(
                Mathf.LerpUnclamped(rect.xMin, rect.xMax, normalizedPoint.x),
                Mathf.LerpUnclamped(rect.yMin, rect.yMax, normalizedPoint.y));
        }
    }
}
