using System.Collections;
using CyberVeil.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CyberVeil.UI
{
    /// <summary>
    /// Procedural, non-destructive slash-charge display drawn over the blue HUD gem.
    /// The authoritative charge values always come from <see cref="AttackLimiterMechanic"/>.
    /// </summary>
    [AddComponentMenu("CyberVeil/UI/Blade Core Indicator")]
    [DisallowMultipleComponent]
    public sealed class BladeCoreIndicator : MaskableGraphic
    {
        private const int FacetCount = 4;

        private static readonly Vector2[] CoreShape =
        {
            new Vector2(0.08f, 0.27f),
            new Vector2(0.16f, 0.10f),
            new Vector2(0.39f, 0.02f),
            new Vector2(0.72f, 0.08f),
            new Vector2(0.91f, 0.28f),
            new Vector2(0.97f, 0.55f),
            new Vector2(0.88f, 0.79f),
            new Vector2(0.65f, 0.94f),
            new Vector2(0.34f, 0.96f),
            new Vector2(0.12f, 0.78f),
            new Vector2(0.03f, 0.52f)
        };

        private static readonly Vector2[][] FacetShapes =
        {
            new[]
            {
                new Vector2(0.12f, 0.33f), new Vector2(0.20f, 0.17f),
                new Vector2(0.36f, 0.11f), new Vector2(0.34f, 0.57f),
                new Vector2(0.23f, 0.79f), new Vector2(0.11f, 0.67f)
            },
            new[]
            {
                new Vector2(0.36f, 0.11f), new Vector2(0.50f, 0.08f),
                new Vector2(0.50f, 0.52f), new Vector2(0.37f, 0.76f),
                new Vector2(0.34f, 0.57f)
            },
            new[]
            {
                new Vector2(0.51f, 0.08f), new Vector2(0.68f, 0.13f),
                new Vector2(0.68f, 0.59f), new Vector2(0.52f, 0.86f),
                new Vector2(0.50f, 0.52f)
            },
            new[]
            {
                new Vector2(0.69f, 0.14f), new Vector2(0.82f, 0.24f),
                new Vector2(0.91f, 0.47f), new Vector2(0.83f, 0.73f),
                new Vector2(0.68f, 0.59f)
            }
        };

        private static readonly Vector2[][] CrackPaths =
        {
            new[] { new Vector2(0.78f, 0.78f), new Vector2(0.68f, 0.64f), new Vector2(0.73f, 0.53f) },
            new[] { new Vector2(0.58f, 0.89f), new Vector2(0.53f, 0.69f), new Vector2(0.45f, 0.58f) },
            new[] { new Vector2(0.28f, 0.83f), new Vector2(0.34f, 0.62f), new Vector2(0.27f, 0.45f) },
            new[] { new Vector2(0.17f, 0.30f), new Vector2(0.31f, 0.36f), new Vector2(0.39f, 0.22f) }
        };

        private static readonly Vector2[] FacetCenters =
        {
            new Vector2(0.23f, 0.48f),
            new Vector2(0.41f, 0.43f),
            new Vector2(0.58f, 0.45f),
            new Vector2(0.78f, 0.47f)
        };

        [Header("Gameplay Sources")]
        [SerializeField] private AttackLimiterMechanic chargeSource;
        [SerializeField] private PlayerAttack playerAttack;

        [Header("Crystalline Palette")]
        [SerializeField] private Color coreShadowColor = new Color(0.005f, 0.025f, 0.10f, 0.94f);
        [SerializeField] private Color activeFacetColor = new Color(0.02f, 0.38f, 1f, 0.82f);
        [SerializeField] private Color facetHighlightColor = new Color(0.56f, 0.92f, 1f, 0.92f);
        [SerializeField] private Color inactiveFacetColor = new Color(0.006f, 0.045f, 0.15f, 0.88f);
        [SerializeField] private Color reducedCapacityColor = new Color(0.025f, 0.035f, 0.09f, 0.86f);
        [SerializeField] private Color glowColor = new Color(0.03f, 0.45f, 1f, 0.34f);
        [SerializeField] private Color fractureColor = new Color(0.35f, 0.78f, 1f, 0.46f);
        [SerializeField] private Color impactFlashColor = new Color(0.80f, 0.96f, 1f, 0.92f);

        [Header("Slash Spend Feedback")]
        [SerializeField, Min(0.01f)] private float spendSeconds = 0.14f;
        [SerializeField, Range(0f, 0.2f)] private float spendPunch = 0.075f;
        [SerializeField, Range(0f, 2f)] private float streakIntensity = 0.9f;

        [Header("Blink Recharge Feedback")]
        [SerializeField, Min(0.01f)] private float rechargeSeconds = 0.30f;
        [SerializeField, Range(0f, 0.2f)] private float rechargePunch = 0.055f;

        [Header("Depleted Feedback")]
        [SerializeField, Min(0.01f)] private float emptyFlickerSeconds = 0.16f;
        [SerializeField, Range(0f, 1f)] private float emptyFlickerIntensity = 0.42f;

        [Header("Idle Energy")]
        [SerializeField, Min(0.2f)] private float idleShimmerSeconds = 2.4f;
        [SerializeField, Range(0f, 1f)] private float idleShimmerIntensity = 0.24f;

        [Header("Timing")]
        [Tooltip("HUD feedback stays responsive during hit stop or time-scale changes.")]
        [SerializeField] private bool useUnscaledTime = true;

        private readonly float[] facetEnergy = { 1f, 1f, 1f, 1f };

        private int displayedRemaining = FacetCount;
        private int displayedMaximum = FacetCount;
        private int feedbackFacet = -1;

        private float flashAmount;
        private float streakProgress = -1f;
        private float rechargeProgress = -1f;
        private float emptyFlickerAmount;
        private float shimmerPosition = -1f;

        private Vector3 restingScale = Vector3.one;
        private Coroutine feedbackRoutine;
        private Coroutine idleRoutine;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            restingScale = rectTransform.localScale;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            restingScale = rectTransform.localScale;
            BindSources();

            if (chargeSource != null)
                SetImmediate(chargeSource.RemainingCharges, chargeSource.Limit);
            else
                SetImmediate(FacetCount, FacetCount);
        }

        protected override void OnDisable()
        {
            UnbindSources();
            StopAllCoroutines();
            feedbackRoutine = null;
            idleRoutine = null;
            ResetTransientVisuals();
            rectTransform.localScale = restingScale;

            base.OnDisable();
        }

        /// <summary>
        /// Re-resolves gameplay sources and synchronizes immediately. This is useful for an
        /// in-place respawn flow; normal scene reloads initialize automatically in OnEnable.
        /// </summary>
        public void RefreshSources()
        {
            UnbindSources();
            BindSources();

            if (chargeSource != null)
                SetImmediate(chargeSource.RemainingCharges, chargeSource.Limit);
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            float totalEnergy = 0f;
            for (int i = 0; i < FacetCount; i++)
                totalEnergy += facetEnergy[i];

            float energizedRatio = totalEnergy / FacetCount;
            float depletedRatio = 1f - Mathf.Clamp01(energizedRatio);
            float flicker = emptyFlickerAmount * emptyFlickerIntensity;

            Color glow = glowColor;
            glow.a *= Mathf.Clamp01(0.14f + energizedRatio * 0.70f + flashAmount * 0.5f + flicker);
            AddPolygon(vertexHelper, CoreShape, rect, Tint(glow), 1.08f);

            Color shadow = coreShadowColor;
            shadow.a *= Mathf.Lerp(0.34f, 1f, depletedRatio);
            shadow = Color.Lerp(shadow, facetHighlightColor, flicker * 0.18f);
            AddPolygon(vertexHelper, CoreShape, rect, Tint(shadow), 1f);

            DrawFacets(vertexHelper, rect);
            DrawFractures(vertexHelper, rect);
            DrawSpendStreak(vertexHelper, rect);
            DrawRechargeStreaks(vertexHelper, rect);

            if (flashAmount > 0f)
            {
                Color flash = impactFlashColor;
                flash.a *= flashAmount;
                AddPolygon(vertexHelper, CoreShape, rect, Tint(flash), 1.01f);
            }
        }

        private void DrawFacets(VertexHelper vertexHelper, Rect rect)
        {
            for (int i = 0; i < FacetCount; i++)
            {
                bool capacityAvailable = i < displayedMaximum;
                float energy = capacityAvailable ? Mathf.Clamp01(facetEnergy[i]) : 0f;
                Color fill = capacityAvailable
                    ? Color.Lerp(inactiveFacetColor, activeFacetColor, energy)
                    : reducedCapacityColor;

                float shimmer = 0f;
                if (energy > 0.01f && shimmerPosition >= 0f)
                    shimmer = Mathf.Clamp01(1f - Mathf.Abs(shimmerPosition - FacetCenters[i].x) * 7f);

                fill = Color.Lerp(fill, facetHighlightColor, shimmer * idleShimmerIntensity);
                AddPolygon(vertexHelper, FacetShapes[i], rect, Tint(fill), 0.96f);

                if (energy > 0.01f)
                {
                    Color inner = facetHighlightColor;
                    inner.a *= energy * (0.22f + shimmer * 0.46f);
                    AddPolygon(vertexHelper, FacetShapes[i], rect, Tint(inner), 0.63f);
                }

                Color edge = facetHighlightColor;
                edge.a *= capacityAvailable ? Mathf.Lerp(0.10f, 0.58f, energy) : 0.08f;
                AddPolygonOutline(vertexHelper, FacetShapes[i], rect, Tint(edge), 0.75f);
            }
        }

        private void DrawFractures(VertexHelper vertexHelper, Rect rect)
        {
            int missing = Mathf.Clamp(displayedMaximum - displayedRemaining, 0, CrackPaths.Length);
            for (int crackIndex = 0; crackIndex < missing; crackIndex++)
            {
                Vector2[] path = CrackPaths[crackIndex];
                Color crack = fractureColor;
                crack.a *= Mathf.Lerp(0.45f, 1f, missing / (float)FacetCount);

                for (int pointIndex = 0; pointIndex < path.Length - 1; pointIndex++)
                {
                    AddLine(
                        vertexHelper,
                        ToRect(path[pointIndex], rect),
                        ToRect(path[pointIndex + 1], rect),
                        0.7f,
                        Tint(crack));
                }
            }
        }

        private void DrawSpendStreak(VertexHelper vertexHelper, Rect rect)
        {
            if (feedbackFacet < 0 || streakProgress < 0f)
                return;

            Vector2 center = ToRect(FacetCenters[feedbackFacet], rect);
            float travel = Mathf.Clamp01(streakProgress);
            Vector2 direction = new Vector2(0.72f, 0.69f).normalized;
            float distance = Mathf.Min(rect.width, rect.height) * 0.36f;
            Vector2 head = center + direction * (distance * travel);
            Vector2 tail = head - direction * Mathf.Lerp(12f, 3f, travel);

            Color streak = impactFlashColor;
            streak.a *= (1f - travel) * streakIntensity;
            AddLine(vertexHelper, tail, head, Mathf.Lerp(2.2f, 0.6f, travel), Tint(streak));

            Vector2 side = new Vector2(-direction.y, direction.x) * Mathf.Lerp(2.4f, 0.5f, travel);
            AddTriangle(vertexHelper, head + direction * 2f, head + side, head - side, Tint(streak));
        }

        private void DrawRechargeStreaks(VertexHelper vertexHelper, Rect rect)
        {
            if (rechargeProgress < 0f)
                return;

            float p = Mathf.Clamp01(rechargeProgress);
            for (int i = 0; i < displayedRemaining; i++)
            {
                Vector2 end = ToRect(FacetCenters[i], rect);
                Vector2 startNormalized = new Vector2(FacetCenters[i].x, i % 2 == 0 ? 1.06f : -0.06f);
                Vector2 start = ToRect(startNormalized, rect);
                Vector2 head = Vector2.Lerp(start, end, p);
                Vector2 tail = Vector2.Lerp(start, end, Mathf.Max(0f, p - 0.18f));

                Color streak = facetHighlightColor;
                streak.a *= Mathf.Sin(p * Mathf.PI) * 0.55f;
                AddLine(vertexHelper, tail, head, 0.9f, Tint(streak));
            }
        }

        private void BindSources()
        {
            if (!Application.isPlaying)
                return;

            if (chargeSource == null)
                chargeSource = FindFirstObjectByType<AttackLimiterMechanic>();
            if (playerAttack == null)
                playerAttack = FindFirstObjectByType<PlayerAttack>();

            if (chargeSource != null)
            {
                chargeSource.OnChargesChanged -= HandleChargesChanged;
                chargeSource.OnChargesChanged += HandleChargesChanged;
            }

            if (playerAttack != null)
            {
                playerAttack.OnAttackRejected -= HandleAttackRejected;
                playerAttack.OnAttackRejected += HandleAttackRejected;
            }
        }

        private void UnbindSources()
        {
            if (chargeSource != null)
                chargeSource.OnChargesChanged -= HandleChargesChanged;
            if (playerAttack != null)
                playerAttack.OnAttackRejected -= HandleAttackRejected;
        }

        private void HandleChargesChanged(int remaining, int maximum, AttackChargeChangeReason reason)
        {
            int targetMaximum = Mathf.Clamp(maximum, 1, FacetCount);
            int targetRemaining = Mathf.Clamp(remaining, 0, targetMaximum);
            int previousRemaining = displayedRemaining;

            StopFeedbackRoutine();
            StopIdleRoutine();

            displayedMaximum = targetMaximum;
            displayedRemaining = targetRemaining;

            if (reason == AttackChargeChangeReason.Spent && targetRemaining < previousRemaining)
            {
                SetFacetEnergies(previousRemaining, targetMaximum);
                int spentFacet = Mathf.Clamp(previousRemaining - 1, 0, FacetCount - 1);
                feedbackRoutine = StartCoroutine(SpendRoutine(spentFacet));
                return;
            }

            if (reason == AttackChargeChangeReason.Reset && targetRemaining >= previousRemaining)
            {
                SetFacetEnergies(Mathf.Min(previousRemaining, targetRemaining), targetMaximum);
                feedbackRoutine = StartCoroutine(RechargeRoutine(previousRemaining));
                return;
            }

            SetFacetEnergies(targetRemaining, targetMaximum);
            feedbackRoutine = StartCoroutine(RechargeRoutine(targetRemaining));
        }

        private void HandleAttackRejected()
        {
            if (displayedRemaining > 0)
                return;

            StopFeedbackRoutine();
            StopIdleRoutine();
            feedbackRoutine = StartCoroutine(EmptyFlickerRoutine());
        }

        private IEnumerator SpendRoutine(int spentFacet)
        {
            feedbackFacet = spentFacet;
            float duration = Mathf.Max(0.01f, spendSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += DeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);

                facetEnergy[spentFacet] = 1f - eased;
                flashAmount = Mathf.Pow(1f - p, 2f);
                streakProgress = p;

                float punch = Mathf.Sin(p * Mathf.PI) * spendPunch;
                rectTransform.localScale = Vector3.Scale(restingScale, new Vector3(1f + punch, 1f - punch * 0.32f, 1f));
                SetVerticesDirty();
                yield return null;
            }

            SetFacetEnergies(displayedRemaining, displayedMaximum);
            ResetTransientVisuals();
            rectTransform.localScale = restingScale;
            feedbackRoutine = null;
            StartIdleRoutineIfNeeded();
            SetVerticesDirty();
        }

        private IEnumerator RechargeRoutine(int previousRemaining)
        {
            float duration = Mathf.Max(0.01f, rechargeSeconds);
            float elapsed = 0f;
            int firstRelitFacet = Mathf.Clamp(previousRemaining, 0, displayedRemaining);
            int relitCount = displayedRemaining - firstRelitFacet;

            while (elapsed < duration)
            {
                elapsed += DeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                rechargeProgress = p;
                flashAmount = Mathf.Sin(p * Mathf.PI) * 0.22f;

                if (relitCount > 0)
                {
                    float stagedProgress = p * relitCount;
                    for (int i = firstRelitFacet; i < displayedRemaining; i++)
                        facetEnergy[i] = Mathf.Clamp01(stagedProgress - (i - firstRelitFacet));
                }

                float punch = Mathf.Sin(p * Mathf.PI) * rechargePunch;
                rectTransform.localScale = restingScale * (1f + punch);
                SetVerticesDirty();
                yield return null;
            }

            SetFacetEnergies(displayedRemaining, displayedMaximum);
            ResetTransientVisuals();
            rectTransform.localScale = restingScale;
            feedbackRoutine = null;
            StartIdleRoutineIfNeeded();
            SetVerticesDirty();
        }

        private IEnumerator EmptyFlickerRoutine()
        {
            float duration = Mathf.Max(0.01f, emptyFlickerSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += DeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                emptyFlickerAmount = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3f)) * (1f - p);

                float recoil = emptyFlickerAmount * 0.012f;
                rectTransform.localScale = Vector3.Scale(restingScale, new Vector3(1f - recoil, 1f + recoil, 1f));
                SetVerticesDirty();
                yield return null;
            }

            ResetTransientVisuals();
            rectTransform.localScale = restingScale;
            feedbackRoutine = null;
            SetVerticesDirty();
        }

        private IEnumerator IdleShimmerRoutine()
        {
            float phase = 0f;
            float duration = Mathf.Max(0.2f, idleShimmerSeconds);

            while (isActiveAndEnabled && displayedRemaining == displayedMaximum)
            {
                phase = Mathf.Repeat(phase + DeltaTime / duration, 1f);
                shimmerPosition = Mathf.Lerp(0.04f, 0.96f, phase);
                SetVerticesDirty();
                yield return null;
            }

            shimmerPosition = -1f;
            idleRoutine = null;
            SetVerticesDirty();
        }

        private void SetImmediate(int remaining, int maximum)
        {
            displayedMaximum = Mathf.Clamp(maximum, 1, FacetCount);
            displayedRemaining = Mathf.Clamp(remaining, 0, displayedMaximum);
            SetFacetEnergies(displayedRemaining, displayedMaximum);
            ResetTransientVisuals();
            rectTransform.localScale = restingScale;
            StartIdleRoutineIfNeeded();
            SetVerticesDirty();
        }

        private void SetFacetEnergies(int remaining, int maximum)
        {
            for (int i = 0; i < FacetCount; i++)
                facetEnergy[i] = i < maximum && i < remaining ? 1f : 0f;
        }

        private void StopFeedbackRoutine()
        {
            if (feedbackRoutine != null)
                StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
            ResetTransientVisuals();
            rectTransform.localScale = restingScale;
        }

        private void StartIdleRoutineIfNeeded()
        {
            StopIdleRoutine();
            if (Application.isPlaying && isActiveAndEnabled && displayedRemaining == displayedMaximum)
                idleRoutine = StartCoroutine(IdleShimmerRoutine());
        }

        private void StopIdleRoutine()
        {
            if (idleRoutine != null)
                StopCoroutine(idleRoutine);
            idleRoutine = null;
            shimmerPosition = -1f;
        }

        private void ResetTransientVisuals()
        {
            flashAmount = 0f;
            streakProgress = -1f;
            rechargeProgress = -1f;
            emptyFlickerAmount = 0f;
            feedbackFacet = -1;
        }

        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private Color Tint(Color source)
        {
            Color tint = color;
            return new Color(source.r * tint.r, source.g * tint.g, source.b * tint.b, source.a * tint.a);
        }

        private static void AddPolygon(
            VertexHelper vertexHelper,
            Vector2[] normalizedPoints,
            Rect rect,
            Color color,
            float scale)
        {
            int firstVertex = vertexHelper.currentVertCount;
            Vector2 center = Vector2.zero;
            for (int i = 0; i < normalizedPoints.Length; i++)
                center += normalizedPoints[i];
            center /= normalizedPoints.Length;

            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                Vector2 point = Vector2.LerpUnclamped(center, normalizedPoints[i], scale);
                vertexHelper.AddVert(ToRect(point, rect), color, Vector2.zero);
            }

            for (int i = 1; i < normalizedPoints.Length - 1; i++)
                vertexHelper.AddTriangle(firstVertex, firstVertex + i, firstVertex + i + 1);
        }

        private static void AddPolygonOutline(
            VertexHelper vertexHelper,
            Vector2[] normalizedPoints,
            Rect rect,
            Color color,
            float width)
        {
            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                Vector2 from = ToRect(normalizedPoints[i], rect);
                Vector2 to = ToRect(normalizedPoints[(i + 1) % normalizedPoints.Length], rect);
                AddLine(vertexHelper, from, to, width, color);
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
