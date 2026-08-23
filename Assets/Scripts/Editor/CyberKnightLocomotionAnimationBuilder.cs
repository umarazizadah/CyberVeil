using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CyberVeil.Editor.Animation
{
    /// <summary>
    /// Rebuilds responsive locomotion variants from the CyberKnight's compatible
    /// generic-rig clips. Runtime turn banking remains procedural so controls stay immediate.
    /// </summary>
    public static class CyberKnightLocomotionAnimationBuilder
    {
        private const string PlayerFolder = "Assets/Art/Characters/PLAYER";
        private const string OutputFolder = PlayerFolder + "/Locomotion";
        private const string ControllerPath = PlayerFolder + "/cyberKnightAnimator.controller";

        [MenuItem("CyberVeil/Animation/Rebuild Player Locomotion Variants")]
        public static void Rebuild()
        {
            EnsureFolder(OutputFolder);

            AnimationClip start = BuildVariant(
                PlayerFolder + "/LASTWALKANI.anim",
                OutputFolder + "/LocomotionStart.anim",
                "LocomotionStart",
                PoseEnvelope.Start,
                new Vector3(9f, 0f, 0f));
            AnimationClip brake = BuildVariant(
                PlayerFolder + "/LASTWALKANI.anim",
                OutputFolder + "/LocomotionBrake.anim",
                "LocomotionBrake",
                PoseEnvelope.Brake,
                new Vector3(-11f, 0f, 0f));
            AnimationClip turnLeft = BuildVariant(
                PlayerFolder + "/run.anim",
                OutputFolder + "/TurnLeft.anim",
                "TurnLeft",
                PoseEnvelope.Turn,
                new Vector3(2f, -7f, 10f));
            AnimationClip turnRight = BuildVariant(
                PlayerFolder + "/run.anim",
                OutputFolder + "/TurnRight.anim",
                "TurnRight",
                PoseEnvelope.Turn,
                new Vector3(2f, 7f, -10f));

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new InvalidOperationException($"Animator Controller not found at {ControllerPath}");

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            EnsureState(stateMachine, "LocomotionStart", start, 2.25f, new Vector3(90f, 280f));
            EnsureState(stateMachine, "LocomotionBrake", brake, 2.05f, new Vector3(310f, 280f));
            EnsureState(stateMachine, "TurnLeft", turnLeft, 2.15f, new Vector3(530f, 250f));
            EnsureState(stateMachine, "TurnRight", turnRight, 2.15f, new Vector3(530f, 310f));
            RemoveInvalidUnconditionalAnyStateTransitions(stateMachine);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CyberKnight responsive locomotion clips and Animator states rebuilt.");
        }

        private static AnimationClip BuildVariant(
            string sourcePath,
            string destinationPath,
            string clipName,
            PoseEnvelope envelope,
            Vector3 spineOffset)
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

            UnityEditor.AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            ApplyQuaternionOffset(clip, "Armature/Spine1", spineOffset * 0.55f, envelope);
            ApplyQuaternionOffset(clip, "Armature/Spine1/Spine2", spineOffset * 0.3f, envelope);
            ApplyQuaternionOffset(clip, "Armature/Spine1/Spine2/Spine3", spineOffset * 0.15f, envelope);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ApplyQuaternionOffset(
            AnimationClip clip,
            string path,
            Vector3 eulerOffset,
            PoseEnvelope envelope)
        {
            EditorCurveBinding[] bindings =
            {
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"),
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"),
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"),
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w")
            };

            AnimationCurve[] sourceCurves = new AnimationCurve[4];
            for (int i = 0; i < bindings.Length; i++)
            {
                sourceCurves[i] = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                if (sourceCurves[i] == null)
                    return;
            }

            const int sampleCount = 13;
            var keys = new List<Keyframe>[4]
            {
                new List<Keyframe>(sampleCount),
                new List<Keyframe>(sampleCount),
                new List<Keyframe>(sampleCount),
                new List<Keyframe>(sampleCount)
            };

            float duration = Mathf.Max(clip.length, 1f / Mathf.Max(clip.frameRate, 1f));
            for (int sample = 0; sample < sampleCount; sample++)
            {
                float normalizedTime = sample / (sampleCount - 1f);
                float time = normalizedTime * duration;
                Quaternion original = Normalize(new Quaternion(
                    sourceCurves[0].Evaluate(time),
                    sourceCurves[1].Evaluate(time),
                    sourceCurves[2].Evaluate(time),
                    sourceCurves[3].Evaluate(time)));
                Quaternion offset = Quaternion.Euler(eulerOffset * EvaluateEnvelope(envelope, normalizedTime));
                Quaternion posed = original * offset;

                keys[0].Add(new Keyframe(time, posed.x));
                keys[1].Add(new Keyframe(time, posed.y));
                keys[2].Add(new Keyframe(time, posed.z));
                keys[3].Add(new Keyframe(time, posed.w));
            }

            for (int i = 0; i < bindings.Length; i++)
                AnimationUtility.SetEditorCurve(clip, bindings[i], new AnimationCurve(keys[i].ToArray()));
        }

        private static float EvaluateEnvelope(PoseEnvelope envelope, float time)
        {
            switch (envelope)
            {
                case PoseEnvelope.Start:
                    return Mathf.Sin(Mathf.Clamp01(time / 0.55f) * Mathf.PI);
                case PoseEnvelope.Brake:
                    return Mathf.Sin(Mathf.Clamp01(time / 0.6f) * Mathf.PI);
                default:
                    return Mathf.Sin(Mathf.Clamp01(time / 0.7f) * Mathf.PI);
            }
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

        private static void EnsureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Motion motion,
            float speed,
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
            state.speed = speed;
            state.writeDefaultValues = true;
        }

        private static void RemoveInvalidUnconditionalAnyStateTransitions(AnimatorStateMachine stateMachine)
        {
            AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
            foreach (AnimatorStateTransition transition in transitions)
            {
                if (transition.destinationState != null
                    && transition.destinationState.name == "TakeDamage"
                    && transition.conditions.Length == 0
                    && !transition.hasExitTime)
                {
                    stateMachine.RemoveAnyStateTransition(transition);
                }
            }
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

        private enum PoseEnvelope
        {
            Start,
            Brake,
            Turn
        }
    }
}
