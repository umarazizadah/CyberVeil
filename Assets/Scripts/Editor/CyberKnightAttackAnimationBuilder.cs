using System;
using System.Collections.Generic;
using CyberVeil.Player;
using CyberVeil.VFX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CyberVeil.Editor.Animation
{
    /// <summary>
    /// Deterministically rebuilds the CyberKnight light-combo clips, Animator states,
    /// combo data, and prefab wiring. The generated clips remain editable in Unity's
    /// Animation window, while gameplay timing stays data driven.
    /// </summary>
    public static class CyberKnightAttackAnimationBuilder
    {
        private const string PlayerFolder = "Assets/Art/Characters/PLAYER";
        private const string OutputFolder = PlayerFolder + "/Combat";
        private const string SlashOneSourcePath = PlayerFolder + "/axeAttack.anim";
        private const string SlashTwoSourcePath = PlayerFolder + "/axeAttack2.anim";
        private const string SlashOnePath = OutputFolder + "/PlayerSlashLeftToRight.anim";
        private const string SlashTwoPath = OutputFolder + "/PlayerSlashDiagonal.anim";
        private const string ComboDefinitionPath = OutputFolder + "/PlayerLightCombo.asset";
        private const string ControllerPath = PlayerFolder + "/cyberKnightAnimator.controller";
        private const string PlayerPrefabPath = "Assets/Prefabs/CyberKnight.prefab";
        private const string AttackSpeedParameter = "AttackSpeed";

        private static readonly string[] RequiredRigPaths =
        {
            "Armature",
            "Armature/Spine1",
            "Armature/Spine1/Spine2",
            "Armature/Spine1/Spine2/Spine3",
            "Armature/Spine1/Spine2/Spine3/Head",
            "Armature/Spine1/Spine2/Spine3/LeftArm",
            "Armature/Spine1/Spine2/Spine3/LeftArm/LeftHand",
            "Armature/Spine1/Spine2/Spine3/RightArm",
            "Armature/Spine1/Spine2/Spine3/RightArm/RightHand",
            "Armature/LeftLeg",
            "Armature/LeftLeg/LeftFoot",
            "Armature/RightLeg",
            "Armature/RightLeg/RightFoot"
        };

        [MenuItem("CyberVeil/Animation/Rebuild Player Slash Combo")]
        public static void Rebuild()
        {
            StopPreview();
            EnsureFolder(OutputFolder);
            ValidateRig();

            AnimationClip slashOne = BuildSlashOne();
            AnimationClip slashTwo = BuildSlashTwo();
            ConfigureAnimator(slashOne, slashTwo);
            PlayerComboDefinition definition = ConfigureComboDefinition();
            ConfigurePlayerPrefab(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "CyberKnight slash combo rebuilt: SlashAttack1 and SlashAttack3 share "
                + "the left-to-right clip; SlashAttack2 uses the diagonal clip.");
        }

        [MenuItem("CyberVeil/Animation/Preview Slash 1 - Anticipation")]
        private static void PreviewSlashOneAnticipation()
        {
            Preview(SlashOnePath, 5f / 24f);
        }

        [MenuItem("CyberVeil/Animation/Preview Slash 1 - Impact")]
        private static void PreviewSlashOneImpact()
        {
            Preview(SlashOnePath, 9f / 24f);
        }

        [MenuItem("CyberVeil/Animation/Preview Slash 1 - Follow Through")]
        private static void PreviewSlashOneFollowThrough()
        {
            Preview(SlashOnePath, 14f / 24f);
        }

        [MenuItem("CyberVeil/Animation/Preview Slash 2 - Anticipation")]
        private static void PreviewSlashTwoAnticipation()
        {
            Preview(SlashTwoPath, 6f / 24f);
        }

        [MenuItem("CyberVeil/Animation/Preview Slash 2 - Impact")]
        private static void PreviewSlashTwoImpact()
        {
            Preview(SlashTwoPath, 10f / 24f);
        }

        [MenuItem("CyberVeil/Animation/Preview Slash 2 - Follow Through")]
        private static void PreviewSlashTwoFollowThrough()
        {
            Preview(SlashTwoPath, 15f / 24f);
        }

        [MenuItem("CyberVeil/Animation/Stop Slash Preview")]
        private static void StopPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            SceneView.RepaintAll();
        }

        private static AnimationClip BuildSlashOne()
        {
            AnimationClip clip = CopyClip(
                SlashOneSourcePath,
                SlashOnePath,
                "PlayerSlashLeftToRight");

            // Anticipation coils the entire torso left/back. The head adds a delayed
            // counter-rotation so it looks carried by the body instead of snapping alone.
            ApplyPose(clip, "Armature", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(1f, -8f, 2f)),
                Pose(7f, new Vector3(0f, -5f, 1f)),
                Pose(9f, new Vector3(-1f, 3f, -1f)),
                Pose(14f, new Vector3(-2f, 7f, -3f)),
                Pose(20f, new Vector3(0f, 2f, -1f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1", SlashOneTorso(0.55f));
            ApplyPose(clip, "Armature/Spine1/Spine2", SlashOneTorso(0.75f));
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3", SlashOneTorso(0.9f));
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/Head", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(-2f, -26f, 5f)),
                Pose(7f, new Vector3(-2f, -22f, 4f)),
                Pose(9f, new Vector3(0f, -13f, 2f)),
                Pose(14f, new Vector3(1f, 9f, -4f)),
                Pose(17f, new Vector3(1f, 7f, -3f)),
                Pose(20f, new Vector3(0f, 3f, -1f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/LeftArm", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(-12f, -9f, 22f)),
                Pose(7f, new Vector3(-8f, -5f, 14f)),
                Pose(9f, new Vector3(5f, 5f, -10f)),
                Pose(14f, new Vector3(14f, 12f, -24f)),
                Pose(17f, new Vector3(10f, 8f, -18f)),
                Pose(20f, new Vector3(3f, 2f, -6f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/LeftArm/LeftHand", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(-6f, -4f, 10f)),
                Pose(7f, new Vector3(-4f, -2f, 7f)),
                Pose(9f, new Vector3(2f, 2f, -5f)),
                Pose(14f, new Vector3(7f, 5f, -12f)),
                Pose(20f, new Vector3(2f, 1f, -4f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/RightArm", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(5f, 4f, -10f)),
                Pose(7f, new Vector3(3f, 2f, -6f)),
                Pose(9f, new Vector3(-2f, -2f, 4f)),
                Pose(14f, new Vector3(-7f, -5f, 12f)),
                Pose(20f, new Vector3(-2f, -1f, 3f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/RightArm/RightHand", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(2f, 2f, -5f)),
                Pose(9f, new Vector3(-1f, -1f, 2f)),
                Pose(14f, new Vector3(-3f, -2f, 6f)),
                Pose(20f, new Vector3(-1f, 0f, 2f)),
                Pose(23f, Vector3.zero)
            });
            ApplyWeightShift(clip, true);

            SetEvents(clip, new[]
            {
                Event(9f, "Hit"),
                Event(10f, "OpenComboWindow"),
                Event(17f, "CloseComboWindow"),
                Event(22f, "FinishAttack")
            });
            return clip;
        }

        private static AnimationClip BuildSlashTwo()
        {
            AnimationClip clip = CopyClip(
                SlashTwoSourcePath,
                SlashTwoPath,
                "PlayerSlashDiagonal");

            // The second strike rises over the right shoulder, then travels through
            // the target toward the lower left instead of stopping at contact.
            ApplyPose(clip, "Armature", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(-3f, 7f, 5f)),
                Pose(8f, new Vector3(-2f, 4f, 3f)),
                Pose(10f, new Vector3(2f, -4f, -3f)),
                Pose(15f, new Vector3(5f, -9f, -7f)),
                Pose(20f, new Vector3(1f, -2f, -2f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1", SlashTwoTorso(0.55f));
            ApplyPose(clip, "Armature/Spine1/Spine2", SlashTwoTorso(0.75f));
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3", SlashTwoTorso(0.9f));
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/Head", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(-4f, 12f, 7f)),
                Pose(8f, new Vector3(-3f, 9f, 5f)),
                Pose(10f, new Vector3(1f, 5f, 2f)),
                Pose(15f, new Vector3(5f, -10f, -7f)),
                Pose(18f, new Vector3(3f, -7f, -5f)),
                Pose(20f, new Vector3(1f, -3f, -2f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/RightArm", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(-16f, 10f, -22f)),
                Pose(7f, new Vector3(-16f, 4f, -26f)),
                Pose(8f, new Vector3(-10f, 7f, -14f)),
                Pose(10f, new Vector3(6f, -5f, 8f)),
                Pose(15f, new Vector3(20f, -14f, 26f)),
                Pose(18f, new Vector3(12f, -9f, 16f)),
                Pose(20f, new Vector3(4f, -3f, 5f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/RightArm/RightHand", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(-8f, 5f, -12f)),
                Pose(7f, new Vector3(-15f, 1f, -9f)),
                Pose(8f, new Vector3(-5f, 3f, -8f)),
                Pose(10f, new Vector3(3f, -2f, 5f)),
                Pose(15f, new Vector3(10f, -7f, 14f)),
                Pose(20f, new Vector3(3f, -2f, 4f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/LeftArm", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(6f, -5f, 10f)),
                Pose(8f, new Vector3(4f, -3f, 7f)),
                Pose(10f, new Vector3(-3f, 2f, -5f)),
                Pose(15f, new Vector3(-9f, 7f, -14f)),
                Pose(20f, new Vector3(-3f, 2f, -4f)),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/Spine1/Spine2/Spine3/LeftArm/LeftHand", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(3f, -2f, 5f)),
                Pose(10f, new Vector3(-1f, 1f, -2f)),
                Pose(15f, new Vector3(-4f, 3f, -7f)),
                Pose(20f, new Vector3(-1f, 1f, -2f)),
                Pose(23f, Vector3.zero)
            });
            ApplyWeightShift(clip, false);

            SetEvents(clip, new[]
            {
                Event(10f, "Hit"),
                Event(11f, "OpenComboWindow"),
                Event(18f, "CloseComboWindow"),
                Event(22f, "FinishAttack")
            });
            return clip;
        }

        private static PoseKey[] SlashOneTorso(float scale)
        {
            return new[]
            {
                Pose(0f, Vector3.zero),
                Pose(5f, new Vector3(3f, -18f, 6f) * scale),
                Pose(7f, new Vector3(1f, -13f, 4f) * scale),
                Pose(9f, new Vector3(-2f, 9f, -3f) * scale),
                Pose(14f, new Vector3(-4f, 19f, -8f) * scale),
                Pose(17f, new Vector3(-3f, 14f, -6f) * scale),
                Pose(20f, new Vector3(-1f, 4f, -2f) * scale),
                Pose(23f, Vector3.zero)
            };
        }

        private static PoseKey[] SlashTwoTorso(float scale)
        {
            return new[]
            {
                Pose(0f, Vector3.zero),
                Pose(6f, new Vector3(-8f, 17f, 13f) * scale),
                Pose(8f, new Vector3(-5f, 11f, 8f) * scale),
                Pose(10f, new Vector3(5f, -9f, -8f) * scale),
                Pose(15f, new Vector3(12f, -23f, -17f) * scale),
                Pose(18f, new Vector3(8f, -16f, -12f) * scale),
                Pose(20f, new Vector3(2f, -5f, -4f) * scale),
                Pose(23f, Vector3.zero)
            };
        }

        private static void ApplyWeightShift(AnimationClip clip, bool leftToRight)
        {
            float windupFrame = leftToRight ? 5f : 6f;
            float hitFrame = leftToRight ? 9f : 10f;
            float followFrame = leftToRight ? 14f : 15f;
            float sign = leftToRight ? 1f : -1f;

            ApplyPose(clip, "Armature/LeftLeg", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(windupFrame, new Vector3(3f, -4f, 4f) * sign),
                Pose(hitFrame, new Vector3(-1f, 2f, -2f) * sign),
                Pose(followFrame, new Vector3(-3f, 5f, -4f) * sign),
                Pose(20f, new Vector3(-1f, 1f, -1f) * sign),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/RightLeg", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(windupFrame, new Vector3(-3f, 4f, -4f) * sign),
                Pose(hitFrame, new Vector3(1f, -2f, 2f) * sign),
                Pose(followFrame, new Vector3(3f, -5f, 4f) * sign),
                Pose(20f, new Vector3(1f, -1f, 1f) * sign),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/LeftLeg/LeftFoot", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(windupFrame, new Vector3(-3f, 2f, 0f) * sign),
                Pose(followFrame, new Vector3(3f, -2f, 0f) * sign),
                Pose(20f, Vector3.zero),
                Pose(23f, Vector3.zero)
            });
            ApplyPose(clip, "Armature/RightLeg/RightFoot", new[]
            {
                Pose(0f, Vector3.zero),
                Pose(windupFrame, new Vector3(3f, -2f, 0f) * sign),
                Pose(followFrame, new Vector3(-3f, 2f, 0f) * sign),
                Pose(20f, Vector3.zero),
                Pose(23f, Vector3.zero)
            });
        }

        private static AnimationClip CopyClip(
            string sourcePath,
            string destinationPath,
            string clipName)
        {
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
            if (source == null)
                throw new InvalidOperationException($"Source AnimationClip not found at {sourcePath}");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(source);
                clip.name = clipName;
                AssetDatabase.CreateAsset(clip, destinationPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, clip);
                clip.name = clipName;
            }

            clip.frameRate = 24f;
            UnityEditor.AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlend = false;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void ApplyPose(AnimationClip clip, string path, PoseKey[] poseKeys)
        {
            EditorCurveBinding[] bindings =
            {
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"),
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"),
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"),
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w")
            };

            var sourceCurves = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                sourceCurves[i] = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                if (sourceCurves[i] == null)
                    throw new InvalidOperationException(
                        $"{clip.name} has no quaternion curve for required bone '{path}'.");
            }

            int lastFrame = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
            var generatedKeys = new List<Keyframe>[4]
            {
                new List<Keyframe>(lastFrame + 1),
                new List<Keyframe>(lastFrame + 1),
                new List<Keyframe>(lastFrame + 1),
                new List<Keyframe>(lastFrame + 1)
            };

            Quaternion previous = Quaternion.identity;
            for (int frame = 0; frame <= lastFrame; frame++)
            {
                float time = Mathf.Min(frame / clip.frameRate, clip.length);
                Quaternion original = Normalize(new Quaternion(
                    sourceCurves[0].Evaluate(time),
                    sourceCurves[1].Evaluate(time),
                    sourceCurves[2].Evaluate(time),
                    sourceCurves[3].Evaluate(time)));
                Quaternion posed = original * Quaternion.Euler(EvaluatePose(poseKeys, frame));

                if (frame > 0 && Quaternion.Dot(previous, posed) < 0f)
                    posed = new Quaternion(-posed.x, -posed.y, -posed.z, -posed.w);

                generatedKeys[0].Add(new Keyframe(time, posed.x));
                generatedKeys[1].Add(new Keyframe(time, posed.y));
                generatedKeys[2].Add(new Keyframe(time, posed.z));
                generatedKeys[3].Add(new Keyframe(time, posed.w));
                previous = posed;
            }

            for (int component = 0; component < bindings.Length; component++)
            {
                AnimationCurve curve = new AnimationCurve(generatedKeys[component].ToArray());
                for (int key = 0; key < curve.length; key++)
                    curve.SmoothTangents(key, 0f);
                AnimationUtility.SetEditorCurve(clip, bindings[component], curve);
            }

            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
        }

        private static Vector3 EvaluatePose(PoseKey[] keys, float frame)
        {
            if (keys == null || keys.Length == 0 || frame <= keys[0].Frame)
                return keys != null && keys.Length > 0 ? keys[0].Euler : Vector3.zero;

            for (int i = 1; i < keys.Length; i++)
            {
                if (frame > keys[i].Frame)
                    continue;

                float duration = Mathf.Max(0.0001f, keys[i].Frame - keys[i - 1].Frame);
                float t = Mathf.Clamp01((frame - keys[i - 1].Frame) / duration);
                t = t * t * (3f - 2f * t);
                return Vector3.LerpUnclamped(keys[i - 1].Euler, keys[i].Euler, t);
            }

            return keys[keys.Length - 1].Euler;
        }

        private static AnimationEvent Event(float frame, string functionName)
        {
            return new AnimationEvent
            {
                time = frame / 24f,
                functionName = functionName
            };
        }

        private static void SetEvents(AnimationClip clip, AnimationEvent[] events)
        {
            AnimationUtility.SetAnimationEvents(clip, events);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureAnimator(AnimationClip slashOne, AnimationClip slashTwo)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new InvalidOperationException($"Animator Controller not found at {ControllerPath}");

            EnsureFloatParameter(controller, AttackSpeedParameter, 1f);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            EnsureAttackState(
                stateMachine,
                "SlashAttack1",
                slashOne,
                new Vector3(760f, 80f));
            EnsureAttackState(
                stateMachine,
                "SlashAttack2",
                slashTwo,
                new Vector3(760f, 145f));
            EnsureAttackState(
                stateMachine,
                "SlashAttack3",
                slashOne,
                new Vector3(760f, 210f));
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureFloatParameter(
            AnimatorController controller,
            string name,
            float defaultValue)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.name != name)
                    continue;

                if (parameter.type != AnimatorControllerParameterType.Float)
                {
                    throw new InvalidOperationException(
                        $"Animator parameter '{name}' exists but is not a float.");
                }

                parameter.defaultFloat = defaultValue;
                parameters[i] = parameter;
                controller.parameters = parameters;
                return;
            }

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue
            });
        }

        private static void EnsureAttackState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Motion motion,
            Vector3 position)
        {
            AnimatorState state = null;
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == stateName)
                {
                    state = child.state;
                    break;
                }
            }

            if (state == null)
                state = stateMachine.AddState(stateName, position);

            state.motion = motion;
            state.speed = 1f;
            state.speedParameter = AttackSpeedParameter;
            state.speedParameterActive = true;
            state.writeDefaultValues = true;
        }

        private static PlayerComboDefinition ConfigureComboDefinition()
        {
            PlayerComboDefinition definition =
                AssetDatabase.LoadAssetAtPath<PlayerComboDefinition>(ComboDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PlayerComboDefinition>();
                AssetDatabase.CreateAsset(definition, ComboDefinitionPath);
            }

            definition.EditorConfigure(0.65f, new[]
            {
                new PlayerAttackStep(
                    "Left-to-right opener",
                    "SlashAttack1",
                    1.45f,
                    0.055f,
                    0.98f,
                    1f,
                    1f,
                    0.22f,
                    VFXType.Slash1,
                    VFXType.SurgeSlash1,
                    1f,
                    new Vector3(0f, 0.8f, 0f),
                    new Vector3(0f, -60f, 0f),
                    0.005f,
                    0f,
                    false),
                new PlayerAttackStep(
                    "High-right diagonal",
                    "SlashAttack2",
                    1.4f,
                    0.05f,
                    0.98f,
                    1f,
                    1f,
                    0.28f,
                    VFXType.Slash2,
                    VFXType.SurgeSlash2,
                    1.2f,
                    new Vector3(0f, 1f, 1.2f),
                    new Vector3(0f, 0f, -75f),
                    0.01f,
                    0f,
                    true),
                new PlayerAttackStep(
                    "Left-to-right finisher",
                    "SlashAttack3",
                    1.55f,
                    0.045f,
                    0.98f,
                    1f,
                    1f,
                    0.36f,
                    VFXType.Slash3,
                    VFXType.SurgeSlash3,
                    0f,
                    new Vector3(0f, 1f, 0f),
                    new Vector3(20f, -60f, 0f),
                    0.04f,
                    0f,
                    false)
            });
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigurePlayerPrefab(PlayerComboDefinition definition)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerAttack playerAttack = prefabRoot.GetComponentInChildren<PlayerAttack>(true);
                if (playerAttack == null)
                    throw new InvalidOperationException("CyberKnight prefab has no PlayerAttack component.");

                PlayerSlashEmitter emitter =
                    playerAttack.GetComponent<PlayerSlashEmitter>()
                    ?? playerAttack.gameObject.AddComponent<PlayerSlashEmitter>();
                if (playerAttack.GetComponent<PlayerAttackAnimationEvents>() == null)
                    playerAttack.gameObject.AddComponent<PlayerAttackAnimationEvents>();

                SerializedObject serializedAttack = new SerializedObject(playerAttack);
                serializedAttack.FindProperty("comboDefinition").objectReferenceValue = definition;
                serializedAttack.FindProperty("slashEmitter").objectReferenceValue = emitter;
                serializedAttack.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidateRig()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    throw new InvalidOperationException("CyberKnight prefab has no Animator component.");

                foreach (string path in RequiredRigPaths)
                {
                    if (animator.transform.Find(path) == null)
                    {
                        throw new InvalidOperationException(
                            $"CyberKnight rig is missing required animation path '{path}'.");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void Preview(string clipPath, float time)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Slash pose preview is only available in Edit Mode.");
                return;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning("Build the player slash combo before previewing it.");
                return;
            }

            PlayerAttack playerAttack = UnityEngine.Object.FindFirstObjectByType<PlayerAttack>();
            Animator animator = playerAttack != null
                ? playerAttack.GetComponent<Animator>()
                : null;
            if (animator == null)
            {
                Debug.LogWarning("No active CyberKnight player was found in the loaded scene.");
                return;
            }

            StopPreview();
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, Mathf.Clamp(time, 0f, clip.length));
            AnimationMode.EndSampling();
            Selection.activeGameObject = animator.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
        }

        private static PoseKey Pose(float frame, Vector3 euler)
        {
            return new PoseKey(frame, euler);
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w);
            if (magnitude <= 0.0001f)
                return Quaternion.identity;

            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private readonly struct PoseKey
        {
            public readonly float Frame;
            public readonly Vector3 Euler;

            public PoseKey(float frame, Vector3 euler)
            {
                Frame = frame;
                Euler = euler;
            }
        }
    }
}
