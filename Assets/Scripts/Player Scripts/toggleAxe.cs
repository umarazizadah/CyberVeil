using UnityEngine;
using System.Collections;

namespace CyberVeil.Player
{
    public class toggleAxe : MonoBehaviour
    {
        [Header("Swing Motion")]
        [SerializeField] private Transform axeRoot;
        [SerializeField] private Vector3 windupLocalOffset = new Vector3(0f, 0f, -0.05f);
        [SerializeField] private Vector3 windupLocalEuler = new Vector3(-10f, -35f, -35f);
        [SerializeField] private Vector3 swingLocalOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private Vector3 swingLocalEuler = new Vector3(20f, 55f, 70f);
        [SerializeField] private float windupSeconds = 0.06f;
        [SerializeField] private float swingSeconds = 0.10f;
        [SerializeField] private float returnSeconds = 0.14f;

        private Vector3 baseLocalPos;
        private Quaternion baseLocalRot;
        private Coroutine swingRoutine;

        private void Awake()
        {
            if (axeRoot == null) axeRoot = transform;
            baseLocalPos = axeRoot.localPosition;
            baseLocalRot = axeRoot.localRotation;
        }

        public void ShowAxe()
        {
            gameObject.SetActive(true); //enable the GameObject

            if (swingRoutine != null)
                StopCoroutine(swingRoutine);
            swingRoutine = StartCoroutine(SwingSequence());
        }

        /// <summary>
        /// Shows the weapon without a second code-driven swing. Light attacks animate
        /// the hand and arm in their AnimationClip, so moving the axe separately would
        /// fight the authored pose and create visible sliding.
        /// </summary>
        public void ShowAxeStatic()
        {
            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
                swingRoutine = null;
            }

            if (axeRoot != null)
            {
                axeRoot.localPosition = baseLocalPos;
                axeRoot.localRotation = baseLocalRot;
            }
            gameObject.SetActive(true);
        }

        public void HideAxe()
        {
            gameObject.SetActive(false); //disable the GameObject

            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
                swingRoutine = null;
            }

            if (axeRoot != null)
            {
                axeRoot.localPosition = baseLocalPos;
                axeRoot.localRotation = baseLocalRot;
            }

        }

        private IEnumerator SwingSequence()
        {
            if (axeRoot == null)
                yield break;

            yield return LerpLocal(baseLocalPos, baseLocalRot,
                baseLocalPos + windupLocalOffset, baseLocalRot * Quaternion.Euler(windupLocalEuler),
                windupSeconds);

            yield return LerpLocal(baseLocalPos + windupLocalOffset, baseLocalRot * Quaternion.Euler(windupLocalEuler),
                baseLocalPos + swingLocalOffset, baseLocalRot * Quaternion.Euler(swingLocalEuler),
                swingSeconds);

            yield return LerpLocal(baseLocalPos + swingLocalOffset, baseLocalRot * Quaternion.Euler(swingLocalEuler),
                baseLocalPos, baseLocalRot,
                returnSeconds);

            swingRoutine = null;
        }

        private IEnumerator LerpLocal(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float seconds)
        {
            float t = 0f;
            float duration = Mathf.Max(0.0001f, seconds);

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = Mathf.SmoothStep(0f, 1f, p);

                axeRoot.localPosition = Vector3.LerpUnclamped(fromPos, toPos, eased);
                axeRoot.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, eased);
                yield return null;
            }

            axeRoot.localPosition = toPos;
            axeRoot.localRotation = toRot;
        }

    }
}
