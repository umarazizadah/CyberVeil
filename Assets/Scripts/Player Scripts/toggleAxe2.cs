using UnityEngine;
using System.Collections;

namespace CyberVeil.Player
{
    public class toggleAxe2 : MonoBehaviour
    {
        [Header("Swing Motion")]
        [SerializeField] private Transform axeRoot;
        [SerializeField] private Vector3 windupLocalOffset = new Vector3(0f, 0f, -0.05f);
        [SerializeField] private Vector3 windupLocalEuler = new Vector3(-10f, 35f, 35f);
        [SerializeField] private Vector3 swingLocalOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private Vector3 swingLocalEuler = new Vector3(10f, -20f, -20f);
        [SerializeField] private float windupSeconds = 0.56f;
        [SerializeField] private float swingSeconds = 1f;
        [SerializeField] private float returnSeconds = 0.54f;

        private Vector3 baseLocalPos;
        private Quaternion baseLocalRot;
        private Coroutine swingRoutine;
        private Coroutine hideRoutine;

        private void Awake()
        {
            if (axeRoot == null) axeRoot = transform;
            baseLocalPos = axeRoot.localPosition;
            baseLocalRot = axeRoot.localRotation;
        }

        public void ShowAxe2()
        {
            gameObject.SetActive(true); //enable the GameObject

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            if (swingRoutine != null)
                StopCoroutine(swingRoutine);
            swingRoutine = StartCoroutine(SwingSequence());
        }

        /// <summary>Shows the weapon while leaving motion to the authored attack clip.</summary>
        public void ShowAxe2Static()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            StopSwingAndReset();
            gameObject.SetActive(true);
        }

        public void HideAxe2()
        {
            if (gameObject.activeInHierarchy)
            {
                if (hideRoutine != null)
                    StopCoroutine(hideRoutine);
                hideRoutine = StartCoroutine(HideAxeWithDelay(0.35f));
            }
        }

        /// <summary>Immediately hides and resets the secondary weapon.</summary>
        public void HideAxe2Immediate()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            StopSwingAndReset();
            gameObject.SetActive(false);
        }

        private IEnumerator HideAxeWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            StopSwingAndReset();
            gameObject.SetActive(false);
            hideRoutine = null;

        }

        private void StopSwingAndReset()
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

