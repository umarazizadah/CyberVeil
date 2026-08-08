using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CyberVeil.UI
{
    /// <summary>
    /// Cinematic, presentation-only motion for the authored home-screen archive.
    /// Every offset is evaluated from a cached rest pose, so repeated selections
    /// cannot accumulate transform drift. Child level props remain parented to
    /// their FragmentShard and therefore inherit the complete shard motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CyberVeilArchiveShardSystem : MonoBehaviour
    {
        [Header("Scene References")]
        public Transform archiveRoot;
        public Camera menuCamera;
        public Transform focusAnchor;
        public Transform collapseTarget;
        public Transform portalEnvironmentFx;
        public Transform portalFragment;
        public Transform floatingDebrisRoot;
        public Transform[] shards;

        [Header("Archive Orbit")]
        [Min(4f)] public float archiveOrbitPeriod = 30f;
        [Range(0f, 8f)] public float archiveOrbitDegrees = 4.2f;
        [Range(1f, 12f)] public float archiveRotationResponse = 4.8f;
        [Min(0.1f)] public float motionFadeIn = 1.35f;

        [Header("Individual Shard Motion")]
        public Vector3 driftAmplitude = new Vector3(0.42f, 0.26f, 0.30f);
        [Range(0.05f, 2f)] public float driftSpeed = 0.34f;
        public Vector3 tiltAmplitude = new Vector3(2.4f, 3.1f, 1.8f);
        [Range(1f, 12f)] public float positionResponse = 4.5f;
        [Range(1f, 12f)] public float rotationResponse = 5.2f;
        [Range(1f, 12f)] public float scaleResponse = 5.8f;

        [Header("Selection Focus")]
        public Vector2 focusViewport = new Vector2(0.62f, 0.70f);
        [Min(1f)] public float focusDepth = 14.5f;
        [Range(1f, 1.2f)] public float focusedScale = 1.085f;
        [Range(0f, 18f)] public float focusedFacingDegrees = 9f;
        [Min(0.05f)] public float focusSpringTime = 0.58f;
        [Min(0f)] public float focusArcHeight = 1.25f;
        [Min(0f)] public float focusArcSide = 0.32f;
        [Min(0f)] public float backgroundDepth = 0.9f;
        [Min(0f)] public float backgroundSeparation = 0.24f;
        [Range(0.35f, 1f)] public float backgroundBrightness = 0.68f;

        [Header("Runtime Framing")]
        public Vector2 horizontalSafeViewport = new Vector2(0.10f, 0.90f);
        public Vector2 verticalSafeViewport = new Vector2(0.20f, 0.92f);

        [Header("Energy")]
        [Range(0.05f, 1f)] public float energyPulseSpeed = 0.42f;
        [Range(0f, 0.25f)] public float energyPulseStrength = 0.10f;

        [Header("ENTER Collapse")]
        [Min(0.1f)] public float outwardDuration = 0.52f;
        [Min(0f)] public float outwardDistance = 0.75f;
        [Min(0.1f)] public float shardCollapseDuration = 0.64f;
        [Min(0.05f)] public float shardCollapseStagger = 0.72f;
        [Min(0.1f)] public float portalExpandDuration = 0.78f;
        [Range(0f, 0.75f)] public float portalExpansion = 0.34f;

        public int FocusedShardIndex { get { return focusedShardIndex; } }
        public bool IsCollapsing { get { return collapsing; } }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private sealed class RendererState
        {
            public Renderer renderer;
            public bool hasBaseColor;
            public bool hasColor;
            public bool hasEmission;
            public Color baseColor;
            public Color color;
            public Color emission;
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        }

        private sealed class LightState
        {
            public Light light;
            public float intensity;
        }

        private sealed class ShardState
        {
            public Transform transform;
            public Transform parent;
            public Vector3 restLocalPosition;
            public Quaternion restLocalRotation;
            public Vector3 restLocalScale;
            public Vector3 positionVelocity;
            public Vector3 scaleVelocity;
            public float phase;
            public float speed;
            public float amplitude;
            public float lane;
            public float focusWeight;
            public float focusVelocity;
            public RendererState[] renderers;
            public LightState[] lights;
        }

        private sealed class DebrisState
        {
            public Transform transform;
            public Vector3 restLocalPosition;
            public Quaternion restLocalRotation;
            public Vector3 restLocalScale;
            public float phase;
            public float speed;
        }

        private sealed class CollapseItem
        {
            public Transform transform;
            public Vector3 startPosition;
            public Quaternion startRotation;
            public Vector3 startScale;
            public Vector3 controlOne;
            public Vector3 controlTwo;
            public float delay;
            public float duration;
            public float spinDirection;
            public float lane;
            public float phase;
            public bool started;
        }

        private ShardState[] shardStates;
        private DebrisState[] debrisStates;
        private Quaternion archiveRestRotation;
        private Vector3 portalRestScale;
        private float elapsedTime;
        private float idleBlend;
        private float idleBlendVelocity;
        private float orbitWeight = 1f;
        private float orbitWeightVelocity;
        private int focusedShardIndex = -1;
        private bool initialized;
        private bool motionEnabled;
        private bool manualOverride;
        private bool collapsing;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void RebuildCache()
        {
            initialized = false;
            EnsureInitialized();
        }

        public void SuspendMotion()
        {
            motionEnabled = false;
        }

        public void BeginIdle()
        {
            EnsureInitialized();
            motionEnabled = true;
        }

        public int FocusRandomShard()
        {
            EnsureInitialized();
            if (shardStates == null || shardStates.Length == 0 || collapsing)
            {
                return -1;
            }

            int candidate = focusedShardIndex;
            if (shardStates.Length == 1)
            {
                candidate = 0;
            }
            else
            {
                int guard = 0;
                while (candidate == focusedShardIndex && guard++ < 16)
                {
                    candidate = Random.Range(0, shardStates.Length);
                }
            }

            focusedShardIndex = candidate;
            return focusedShardIndex;
        }

        public void ClearFocus()
        {
            focusedShardIndex = -1;
        }

        private void LateUpdate()
        {
            if (!motionEnabled || manualOverride || !EnsureInitialized())
            {
                return;
            }

            float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            elapsedTime += delta;
            idleBlend = Mathf.SmoothDamp(
                idleBlend,
                1f,
                ref idleBlendVelocity,
                motionFadeIn,
                Mathf.Infinity,
                delta);
            orbitWeight = Mathf.SmoothDamp(
                orbitWeight,
                1f,
                ref orbitWeightVelocity,
                0.7f,
                Mathf.Infinity,
                delta);

            AnimateArchiveRoot(delta);
            AnimateShards(delta);
            AnimateDebris();
        }

        private void AnimateArchiveRoot(float delta)
        {
            float orbitRadians = elapsedTime * Mathf.PI * 2f / Mathf.Max(4f, archiveOrbitPeriod);
            float yaw = Mathf.Sin(orbitRadians) * archiveOrbitDegrees * idleBlend * orbitWeight;
            Quaternion target = archiveRestRotation * Quaternion.Euler(0f, yaw, 0f);
            archiveRoot.localRotation = Quaternion.Slerp(
                archiveRoot.localRotation,
                target,
                Damp(archiveRotationResponse, delta));
        }

        private void AnimateShards(float delta)
        {
            float globalFocus = 0f;
            for (int i = 0; i < shardStates.Length; i++)
            {
                ShardState state = shardStates[i];
                float targetWeight = i == focusedShardIndex ? 1f : 0f;
                state.focusWeight = Mathf.SmoothDamp(
                    state.focusWeight,
                    targetWeight,
                    ref state.focusVelocity,
                    focusSpringTime,
                    4f,
                    delta);
                globalFocus = Mathf.Max(globalFocus, state.focusWeight);
            }

            Vector3 focusWorld = GetFocusWorldPosition();
            Vector3 cameraForward = menuCamera.transform.forward;
            Vector3 cameraRight = menuCamera.transform.right;
            Vector3 cameraUp = menuCamera.transform.up;

            for (int i = 0; i < shardStates.Length; i++)
            {
                ShardState state = shardStates[i];
                float wave = elapsedTime * driftSpeed * state.speed + state.phase;
                Vector3 drift = new Vector3(
                    Mathf.Sin(wave) * driftAmplitude.x,
                    Mathf.Sin(wave * 0.73f + state.phase * 0.41f) * driftAmplitude.y,
                    Mathf.Cos(wave * 0.91f + state.phase * 0.67f) * driftAmplitude.z);
                drift *= state.amplitude * idleBlend;

                Vector3 idleLocalPosition = state.restLocalPosition + drift;
                float weight = state.focusWeight;
                float arc = Mathf.Sin(weight * Mathf.PI);
                Vector3 visualCenterWorld = GetVisualCenter(state);
                Vector3 focusRootWorld = focusWorld
                    + (state.transform.position - visualCenterWorld);
                Vector3 focusLocalPosition = state.parent.InverseTransformPoint(focusRootWorld);
                Vector3 arcWorld = cameraUp * (focusArcHeight * arc)
                    + cameraRight * (state.lane * focusArcSide * arc);
                Vector3 desiredLocalPosition = Vector3.Lerp(
                    idleLocalPosition,
                    focusLocalPosition,
                    weight);
                desiredLocalPosition += state.parent.InverseTransformVector(arcWorld);

                if (i != focusedShardIndex && globalFocus > 0.001f)
                {
                    Vector3 idleWorld = state.parent.TransformPoint(idleLocalPosition);
                    float side = Mathf.Sign(menuCamera.WorldToViewportPoint(idleWorld).x - focusViewport.x);
                    if (Mathf.Approximately(side, 0f))
                    {
                        side = state.lane >= 0f ? 1f : -1f;
                    }
                    Vector3 backgroundWorld = cameraForward * backgroundDepth
                        + cameraRight * (side * backgroundSeparation);
                    desiredLocalPosition += state.parent.InverseTransformVector(backgroundWorld) * globalFocus;
                }

                desiredLocalPosition = ConstrainVisualCenterToViewport(
                    state,
                    desiredLocalPosition);

                state.transform.localPosition = Vector3.SmoothDamp(
                    state.transform.localPosition,
                    desiredLocalPosition,
                    ref state.positionVelocity,
                    1f / Mathf.Max(1f, positionResponse),
                    Mathf.Infinity,
                    delta);

                Vector3 tilt = new Vector3(
                    Mathf.Sin(wave * 0.61f + 0.7f) * tiltAmplitude.x,
                    Mathf.Sin(wave * 0.49f + 1.4f) * tiltAmplitude.y,
                    Mathf.Cos(wave * 0.57f + 0.2f) * tiltAmplitude.z);
                Quaternion idleLocalRotation = state.restLocalRotation * Quaternion.Euler(tilt * idleBlend);
                Quaternion desiredLocalRotation = idleLocalRotation;
                if (weight > 0.001f)
                {
                    Quaternion idleWorldRotation = state.parent.rotation * idleLocalRotation;
                    Vector3 toCamera = menuCamera.transform.position - state.transform.position;
                    if (toCamera.sqrMagnitude > 0.001f)
                    {
                        Quaternion faceCamera = Quaternion.LookRotation(toCamera.normalized, cameraUp);
                        Quaternion constrained = Quaternion.RotateTowards(
                            idleWorldRotation,
                            faceCamera,
                            focusedFacingDegrees * weight);
                        desiredLocalRotation = Quaternion.Inverse(state.parent.rotation) * constrained;
                    }
                }
                state.transform.localRotation = Quaternion.Slerp(
                    state.transform.localRotation,
                    desiredLocalRotation,
                    Damp(rotationResponse, delta));

                Vector3 desiredScale = state.restLocalScale * Mathf.Lerp(1f, focusedScale, weight);
                if (i != focusedShardIndex)
                {
                    desiredScale *= Mathf.Lerp(1f, 0.985f, globalFocus);
                }
                state.transform.localScale = Vector3.SmoothDamp(
                    state.transform.localScale,
                    desiredScale,
                    ref state.scaleVelocity,
                    1f / Mathf.Max(1f, scaleResponse),
                    Mathf.Infinity,
                    delta);

                float occasionalPulse = Mathf.Pow(
                    Mathf.Max(0f, Mathf.Sin(elapsedTime * energyPulseSpeed * state.speed + state.phase * 1.7f)),
                    12f);
                float backgroundFactor = i == focusedShardIndex
                    ? 1f
                    : Mathf.Lerp(1f, backgroundBrightness, globalFocus);
                float energy = occasionalPulse * energyPulseStrength * (0.55f + weight * 0.75f);
                ApplyPresentation(state, backgroundFactor * (1f + energy), energy);
            }
        }

        private void AnimateDebris()
        {
            if (debrisStates == null)
            {
                return;
            }

            for (int i = 0; i < debrisStates.Length; i++)
            {
                DebrisState state = debrisStates[i];
                float wave = elapsedTime * 0.31f * state.speed + state.phase;
                state.transform.localPosition = state.restLocalPosition + new Vector3(
                    Mathf.Sin(wave) * 0.08f,
                    Mathf.Sin(wave * 0.69f + 1.2f) * 0.11f,
                    Mathf.Cos(wave * 0.83f) * 0.07f) * idleBlend;
                state.transform.localRotation = state.restLocalRotation * Quaternion.Euler(
                    Mathf.Sin(wave * 0.43f) * 7f,
                    elapsedTime * (3.2f + i * 0.27f),
                    Mathf.Cos(wave * 0.51f) * 5f);
            }
        }

        public IEnumerator CollapseIntoPortal()
        {
            if (!EnsureInitialized() || collapsing)
            {
                yield break;
            }

            collapsing = true;
            manualOverride = true;
            motionEnabled = false;

            Vector3 ingestionPoint = collapseTarget != null
                ? collapseTarget.position
                : (portalEnvironmentFx != null ? portalEnvironmentFx.position : archiveRoot.position);
            float transitionStartTime = elapsedTime;
            float portalElapsed = 0f;

            Vector3[] shardStartLocalPositions = new Vector3[shardStates.Length];
            Quaternion[] shardStartLocalRotations = new Quaternion[shardStates.Length];
            Vector3[] shardStartScales = new Vector3[shardStates.Length];
            Vector3[] shardOutwardLocalPositions = new Vector3[shardStates.Length];
            for (int i = 0; i < shardStates.Length; i++)
            {
                ShardState state = shardStates[i];
                shardStartLocalPositions[i] = state.transform.localPosition;
                shardStartLocalRotations[i] = state.transform.localRotation;
                shardStartScales[i] = state.transform.localScale;
                Vector3 outward = state.transform.position - archiveRoot.position;
                outward -= menuCamera.transform.forward * Vector3.Dot(outward, menuCamera.transform.forward);
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = menuCamera.transform.right * (state.lane >= 0f ? 1f : -1f);
                }
                shardOutwardLocalPositions[i] = shardStartLocalPositions[i]
                    + state.parent.InverseTransformVector(outward.normalized * outwardDistance);
            }

            float elapsed = 0f;
            while (elapsed < outwardDuration)
            {
                float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                elapsed += delta;
                elapsedTime += delta;
                float progress = Mathf.Clamp01(elapsed / outwardDuration);
                float eased = Smoother(progress);
                AnimateArchiveRoot(delta);
                AnimateDebris();
                AdvancePortalExpansion(ref portalElapsed, delta);

                for (int i = 0; i < shardStates.Length; i++)
                {
                    ShardState state = shardStates[i];
                    Vector3 hoverDelta = GetTransitionHover(state, elapsedTime)
                        - GetTransitionHover(state, transitionStartTime);
                    hoverDelta += state.parent.InverseTransformVector(
                        menuCamera.transform.up * (Mathf.Sin(progress * Mathf.PI) * 0.16f));
                    state.transform.localPosition = Vector3.Lerp(
                        shardStartLocalPositions[i],
                        shardOutwardLocalPositions[i],
                        eased) + hoverDelta;
                    Vector3 tiltDelta = GetTransitionTilt(state, elapsedTime)
                        - GetTransitionTilt(state, transitionStartTime);
                    state.transform.localRotation = shardStartLocalRotations[i]
                        * Quaternion.Euler(tiltDelta)
                        * Quaternion.Euler(0f, state.lane * 2.5f * eased, 0f);
                    state.transform.localScale = Vector3.Lerp(
                        shardStartScales[i],
                        shardStartScales[i] * 1.025f,
                        eased);
                    ApplyPresentation(state, 1f, 0.05f * eased);
                }
                yield return null;
            }

            ingestionPoint = GetCurrentIngestionPoint();
            List<CollapseItem> items = BuildCollapseItems(ingestionPoint);
            Quaternion sequenceRootRotation = archiveRoot.rotation;
            float sequenceDuration = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                sequenceDuration = Mathf.Max(sequenceDuration, items[i].delay + items[i].duration);
            }

            elapsed = 0f;
            while (elapsed < sequenceDuration)
            {
                float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                elapsed += delta;
                elapsedTime += delta;
                AnimateArchiveRoot(delta);
                AdvancePortalExpansion(ref portalElapsed, delta);
                ingestionPoint = GetCurrentIngestionPoint();
                Quaternion orbitDelta = archiveRoot.rotation
                    * Quaternion.Inverse(sequenceRootRotation);

                for (int i = 0; i < items.Count; i++)
                {
                    CollapseItem item = items[i];
                    float progress = Mathf.Clamp01((elapsed - item.delay) / item.duration);
                    if (progress <= 0f)
                    {
                        AnimateWaitingItem(item, orbitDelta, elapsed);
                        continue;
                    }

                    if (!item.started)
                    {
                        item.started = true;
                        item.startPosition = item.transform.position;
                        item.startRotation = item.transform.rotation;
                        item.startScale = item.transform.localScale;
                        ConfigureCollapseCurve(item, ingestionPoint);
                    }

                    float eased = Smoother(progress);
                    item.transform.position = CubicBezier(
                        item.startPosition,
                        item.controlOne,
                        item.controlTwo,
                        ingestionPoint,
                        eased);
                    Quaternion spin = Quaternion.AngleAxis(
                        item.spinDirection * 540f * eased,
                        menuCamera.transform.forward)
                        * Quaternion.AngleAxis(
                            item.spinDirection * 120f * eased,
                            menuCamera.transform.up);
                    item.transform.rotation = spin * item.startRotation;
                    item.transform.localScale = Vector3.Lerp(
                        item.startScale,
                        Vector3.zero,
                        Mathf.Pow(eased, 1.15f));
                }
                yield return null;
            }

            ingestionPoint = GetCurrentIngestionPoint();
            for (int i = 0; i < items.Count; i++)
            {
                items[i].transform.position = ingestionPoint;
                items[i].transform.localScale = Vector3.zero;
            }

            if (portalEnvironmentFx != null)
            {
                portalEnvironmentFx.localScale = portalRestScale * (1f + portalExpansion);
            }
        }

        private List<CollapseItem> BuildCollapseItems(Vector3 ingestionPoint)
        {
            List<CollapseItem> items = new List<CollapseItem>();
            List<int> shardOrder = new List<int>();
            for (int i = 0; i < shardStates.Length; i++)
            {
                if (i != focusedShardIndex)
                {
                    shardOrder.Add(i);
                }
            }
            if (focusedShardIndex >= 0 && focusedShardIndex < shardStates.Length)
            {
                shardOrder.Add(focusedShardIndex);
            }

            for (int order = 0; order < shardOrder.Count; order++)
            {
                int index = shardOrder[order];
                ShardState state = shardStates[index];
                items.Add(CreateCollapseItem(
                    state.transform,
                    ingestionPoint,
                    order * shardCollapseStagger,
                    shardCollapseDuration,
                    state.lane,
                    order % 2 == 0 ? 1f : -1f));
            }

            float lastShardEnd = shardStates.Length == 0
                ? 0f
                : (shardStates.Length - 1) * shardCollapseStagger + shardCollapseDuration;
            if (debrisStates != null)
            {
                for (int i = 0; i < debrisStates.Length; i++)
                {
                    float normalized = debrisStates.Length <= 1 ? 0f : (float)i / (debrisStates.Length - 1);
                    float delay = Mathf.Lerp(0.08f, Mathf.Max(0.1f, lastShardEnd - 0.48f), normalized);
                    items.Add(CreateCollapseItem(
                        debrisStates[i].transform,
                        ingestionPoint,
                        delay,
                        0.42f,
                        (i % 3) - 1f,
                        i % 2 == 0 ? 1f : -1f));
                }
            }

            if (portalFragment != null)
            {
                items.Add(CreateCollapseItem(
                    portalFragment,
                    ingestionPoint,
                    Mathf.Max(0.05f, lastShardEnd - 0.50f),
                    0.45f,
                    0f,
                    1f));
            }
            return items;
        }

        private CollapseItem CreateCollapseItem(
            Transform target,
            Vector3 ingestionPoint,
            float delay,
            float duration,
            float lane,
            float spinDirection)
        {
            CollapseItem item = new CollapseItem
            {
                transform = target,
                startPosition = target.position,
                startRotation = target.rotation,
                startScale = target.localScale,
                delay = delay,
                duration = duration,
                spinDirection = spinDirection,
                lane = lane,
                phase = 0.73f + delay * 2.31f + lane * 1.47f
            };
            ConfigureCollapseCurve(item, ingestionPoint);
            return item;
        }

        private void ConfigureCollapseCurve(CollapseItem item, Vector3 ingestionPoint)
        {
            Vector3 outward = item.startPosition - ingestionPoint;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = menuCamera.transform.right;
            }
            Vector3 side = menuCamera.transform.right * item.lane * 0.38f;
            item.controlOne = item.startPosition + outward.normalized * 0.72f
                + menuCamera.transform.up * (0.65f + Mathf.Abs(item.lane) * 0.16f)
                + side;
            item.controlTwo = ingestionPoint - menuCamera.transform.forward * 1.25f
                + menuCamera.transform.up * 0.24f
                - side * 0.35f;
        }

        private void AnimateWaitingItem(
            CollapseItem item,
            Quaternion orbitDelta,
            float sequenceElapsed)
        {
            Vector3 orbitPosition = archiveRoot.position
                + orbitDelta * (item.startPosition - archiveRoot.position);
            float wave = sequenceElapsed * (0.72f + Mathf.Abs(item.lane) * 0.08f)
                + item.phase;
            Vector3 hover = menuCamera.transform.up * (Mathf.Sin(wave) * 0.09f)
                + menuCamera.transform.right * (Mathf.Cos(wave * 0.79f) * 0.045f)
                + menuCamera.transform.forward * (Mathf.Sin(wave * 0.63f) * 0.035f);
            item.transform.position = orbitPosition + hover;
            item.transform.rotation = orbitDelta * item.startRotation
                * Quaternion.Euler(
                    Mathf.Sin(wave * 0.67f) * 1.4f,
                    Mathf.Cos(wave * 0.53f) * 1.8f,
                    Mathf.Sin(wave * 0.61f) * 1.2f);
        }

        private Vector3 GetTransitionHover(ShardState state, float time)
        {
            float wave = time * driftSpeed * state.speed + state.phase;
            return new Vector3(
                Mathf.Sin(wave) * 0.075f,
                Mathf.Sin(wave * 0.73f + state.phase * 0.41f) * 0.105f,
                Mathf.Cos(wave * 0.91f + state.phase * 0.67f) * 0.06f);
        }

        private Vector3 GetTransitionTilt(ShardState state, float time)
        {
            float wave = time * driftSpeed * state.speed + state.phase;
            return new Vector3(
                Mathf.Sin(wave * 0.61f + 0.7f) * 1.2f,
                Mathf.Sin(wave * 0.49f + 1.4f) * 1.5f,
                Mathf.Cos(wave * 0.57f + 0.2f) * 0.9f);
        }

        private Vector3 GetCurrentIngestionPoint()
        {
            return collapseTarget != null
                ? collapseTarget.position
                : (portalEnvironmentFx != null ? portalEnvironmentFx.position : archiveRoot.position);
        }

        private void AdvancePortalExpansion(ref float elapsed, float delta)
        {
            if (portalEnvironmentFx == null || elapsed >= portalExpandDuration)
            {
                return;
            }

            elapsed = Mathf.Min(portalExpandDuration, elapsed + delta);
            float progress = Mathf.Clamp01(elapsed / portalExpandDuration);
            float expansion = 1f - Mathf.Pow(1f - progress, 3f)
                + Mathf.Sin(progress * Mathf.PI) * 0.10f;
            portalEnvironmentFx.localScale = portalRestScale
                * (1f + portalExpansion * expansion);
        }

        private bool EnsureInitialized()
        {
            if (initialized)
            {
                return true;
            }
            if (archiveRoot == null || menuCamera == null)
            {
                return false;
            }

            AutoDiscoverReferences();
            if (shards == null || shards.Length == 0)
            {
                return false;
            }

            archiveRestRotation = archiveRoot.localRotation;
            portalRestScale = portalEnvironmentFx != null
                ? portalEnvironmentFx.localScale
                : Vector3.one;

            shardStates = new ShardState[shards.Length];
            float center = (shards.Length - 1) * 0.5f;
            for (int i = 0; i < shards.Length; i++)
            {
                Transform shard = shards[i];
                if (shard == null)
                {
                    continue;
                }
                ShardState state = new ShardState
                {
                    transform = shard,
                    parent = shard.parent,
                    restLocalPosition = shard.localPosition,
                    restLocalRotation = shard.localRotation,
                    restLocalScale = shard.localScale,
                    phase = 0.37f + i * 2.173f,
                    speed = 0.86f + i * 0.17f,
                    amplitude = i == 1 ? 1.08f : (i == 2 ? 0.94f : 1f),
                    lane = i - center,
                    renderers = CaptureRenderers(shard),
                    lights = CaptureLights(shard)
                };
                shardStates[i] = state;
            }

            List<DebrisState> debris = new List<DebrisState>();
            if (floatingDebrisRoot != null)
            {
                for (int i = 0; i < floatingDebrisRoot.childCount; i++)
                {
                    Transform piece = floatingDebrisRoot.GetChild(i);
                    debris.Add(new DebrisState
                    {
                        transform = piece,
                        restLocalPosition = piece.localPosition,
                        restLocalRotation = piece.localRotation,
                        restLocalScale = piece.localScale,
                        phase = 0.61f + i * 0.83f,
                        speed = 0.78f + i * 0.065f
                    });
                }
            }
            debrisStates = debris.ToArray();
            initialized = true;
            return true;
        }

        private void AutoDiscoverReferences()
        {
            if (shards == null || shards.Length == 0)
            {
                List<Transform> found = new List<Transform>();
                for (int i = 0; i < archiveRoot.childCount; i++)
                {
                    Transform child = archiveRoot.GetChild(i);
                    if (child.name == "FragmentShard" || child.name.StartsWith("FragmentShard ("))
                    {
                        found.Add(child);
                    }
                }
                shards = found.ToArray();
            }
            if (floatingDebrisRoot == null)
            {
                floatingDebrisRoot = archiveRoot.Find("Floating_Archive_Debris");
            }
            if (portalFragment == null)
            {
                portalFragment = archiveRoot.Find("Fragment_Glowing_PortalArchive");
            }
        }

        private RendererState[] CaptureRenderers(Transform shard)
        {
            Renderer[] renderers = shard.GetComponentsInChildren<Renderer>(true);
            RendererState[] states = new RendererState[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Material material = renderer.sharedMaterial;
                RendererState state = new RendererState { renderer = renderer };
                if (material != null)
                {
                    state.hasBaseColor = material.HasProperty(BaseColorId);
                    state.hasColor = material.HasProperty(ColorId);
                    state.hasEmission = material.HasProperty(EmissionColorId);
                    if (state.hasBaseColor)
                    {
                        state.baseColor = material.GetColor(BaseColorId);
                    }
                    if (state.hasColor)
                    {
                        state.color = material.GetColor(ColorId);
                    }
                    if (state.hasEmission)
                    {
                        state.emission = material.GetColor(EmissionColorId);
                    }
                }
                states[i] = state;
            }
            return states;
        }

        private LightState[] CaptureLights(Transform shard)
        {
            Light[] lights = shard.GetComponentsInChildren<Light>(true);
            LightState[] states = new LightState[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                states[i] = new LightState { light = lights[i], intensity = lights[i].intensity };
            }
            return states;
        }

        private void ApplyPresentation(ShardState state, float brightness, float energy)
        {
            if (state == null)
            {
                return;
            }
            for (int i = 0; i < state.renderers.Length; i++)
            {
                RendererState rendererState = state.renderers[i];
                if (rendererState.renderer == null)
                {
                    continue;
                }
                rendererState.renderer.GetPropertyBlock(rendererState.block);
                if (rendererState.hasBaseColor)
                {
                    rendererState.block.SetColor(BaseColorId, MultiplyRgb(rendererState.baseColor, brightness));
                }
                if (rendererState.hasColor)
                {
                    rendererState.block.SetColor(ColorId, MultiplyRgb(rendererState.color, brightness));
                }
                if (rendererState.hasEmission)
                {
                    rendererState.block.SetColor(
                        EmissionColorId,
                        MultiplyRgb(rendererState.emission, 1f + energy * 1.8f));
                }
                rendererState.renderer.SetPropertyBlock(rendererState.block);
            }
            for (int i = 0; i < state.lights.Length; i++)
            {
                if (state.lights[i].light != null)
                {
                    state.lights[i].light.intensity = state.lights[i].intensity * brightness * (1f + energy);
                }
            }
        }

        private Vector3 GetFocusWorldPosition()
        {
            float depth = focusDepth;
            if (focusAnchor != null)
            {
                float anchorDepth = Vector3.Dot(
                    focusAnchor.position - menuCamera.transform.position,
                    menuCamera.transform.forward);
                if (anchorDepth > 0.1f)
                {
                    depth = anchorDepth;
                }
            }
            Vector3 target = menuCamera.ViewportToWorldPoint(
                new Vector3(focusViewport.x, focusViewport.y, depth));
            if (focusAnchor != null)
            {
                focusAnchor.position = target;
            }
            return target;
        }

        private Vector3 GetVisualCenter(ShardState state)
        {
            bool found = false;
            Bounds bounds = new Bounds(state.transform.position, Vector3.zero);
            for (int i = 0; i < state.renderers.Length; i++)
            {
                Renderer renderer = state.renderers[i].renderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found ? bounds.center : state.transform.position;
        }

        private Vector3 ConstrainVisualCenterToViewport(
            ShardState state,
            Vector3 desiredLocalPosition)
        {
            Vector3 desiredRootWorld = state.parent.TransformPoint(desiredLocalPosition);
            Vector3 predictedCenterWorld = GetVisualCenter(state)
                + (desiredRootWorld - state.transform.position);
            Vector3 viewport = menuCamera.WorldToViewportPoint(predictedCenterWorld);
            if (viewport.z <= 0.1f)
            {
                return desiredLocalPosition;
            }

            float minimumX = Mathf.Min(horizontalSafeViewport.x, horizontalSafeViewport.y);
            float maximumX = Mathf.Max(horizontalSafeViewport.x, horizontalSafeViewport.y);
            float minimumY = Mathf.Min(verticalSafeViewport.x, verticalSafeViewport.y);
            float maximumY = Mathf.Max(verticalSafeViewport.x, verticalSafeViewport.y);
            Vector3 constrainedViewport = new Vector3(
                Mathf.Clamp(viewport.x, minimumX, maximumX),
                Mathf.Clamp(viewport.y, minimumY, maximumY),
                viewport.z);
            if ((constrainedViewport - viewport).sqrMagnitude < 0.000001f)
            {
                return desiredLocalPosition;
            }

            Vector3 correctionWorld = menuCamera.ViewportToWorldPoint(constrainedViewport)
                - predictedCenterWorld;
            return desiredLocalPosition + state.parent.InverseTransformVector(correctionWorld);
        }

        private static Color MultiplyRgb(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        private static float Damp(float response, float delta)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * delta);
        }

        private static float Smoother(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private static Vector3 CubicBezier(
            Vector3 start,
            Vector3 controlOne,
            Vector3 controlTwo,
            Vector3 end,
            float value)
        {
            float inverse = 1f - value;
            return inverse * inverse * inverse * start
                + 3f * inverse * inverse * value * controlOne
                + 3f * inverse * value * value * controlTwo
                + value * value * value * end;
        }
    }
}
