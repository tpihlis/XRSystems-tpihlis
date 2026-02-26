using System.Collections;
using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Minimal physical stat button:
    /// - place as a physical GameObject with a trigger collider
    /// - set `buttonTop` to the part that moves down when pressed (local position changed)
    /// - touching the collider triggers press; releasing triggers release
    /// - spends player money (if cost>0) and applies stat increase when pressed
    /// - no UI, physical-only
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PhysicalStatButton : MonoBehaviour
    {
        public enum StatType { Fishing, Strength, Luck, Trading }

        [Header("Button")]
        [Tooltip("Part that visually moves. If empty, the whole object will be moved.")]
        public Transform buttonTop;

        [Tooltip("Local depth the button moves when pressed (meters).")]
        public float pressDepth = 0.02f;

        [Tooltip("Seconds to animate press/release.")]
        public float pressAnimTime = 0.08f;

        [Header("Upgrade")]
        public StatType stat = StatType.Fishing;
        [Tooltip("How much stat is increased when pressed.")]
        public int increment = 1;

        [Tooltip("Cost in euros for this upgrade. Set 0 for free.")]
        public float cost = 0f;

        [Header("References")]
        public PlayerStats playerStats; // assign in inspector or found at runtime

        [Header("Debug")]
        public bool debugLogs = true;

        Vector3 topInitialLocalPos;
        bool isPressed = false;
        Coroutine animRoutine = null;

        void Awake()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;

            if (buttonTop == null) buttonTop = transform;

            topInitialLocalPos = buttonTop.localPosition;

            if (playerStats == null)
            {
                // best-effort find
#if UNITY_2023_2_OR_NEWER
                playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
#else
                playerStats = FindObjectOfType<PlayerStats>();
#endif
                if (debugLogs && playerStats == null)
                    Debug.Log("[PhysicalStatButton] Could not find PlayerStats in scene. Assign manually to enable buying.");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // any collider touching will press (keeps it simple)
            BeginPress();
        }

        void OnTriggerExit(Collider other)
        {
            // release when collider leaves
            EndPress();
        }

        void BeginPress()
        {
            if (isPressed) return;
            isPressed = true;

            // attempt purchase (if cost > 0) or just apply if free
            bool affordable = true;
            if (cost > 0f)
            {
                if (playerStats != null)
                    affordable = playerStats.SpendMoney(cost);
                else
                    affordable = false;
            }

            if (!affordable)
            {
                if (debugLogs) Debug.Log($"[PhysicalStatButton] Not enough money for cost {cost:F2}€");
                // still animate a short press-feedback but do not apply upgrade
                if (animRoutine != null) StopCoroutine(animRoutine);
                animRoutine = StartCoroutine(AnimatePressFeedback(false));
                return;
            }

            // apply upgrade
            ApplyUpgrade();

            // animate press down
            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimatePressFeedback(true));
        }

        void EndPress()
        {
            if (!isPressed) return;
            isPressed = false;
            // animate release back to initial
            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateRelease());
        }

        IEnumerator AnimatePressFeedback(bool stayDown)
        {
            // move toward pressed position
            Vector3 target = topInitialLocalPos + Vector3.down * pressDepth;
            float t = 0f;
            Vector3 start = buttonTop.localPosition;
            while (t < pressAnimTime)
            {
                t += Time.deltaTime;
                float f = Mathf.SmoothStep(0f, 1f, t / pressAnimTime);
                buttonTop.localPosition = Vector3.Lerp(start, target, f);
                yield return null;
            }
            buttonTop.localPosition = target;

            if (!stayDown)
            {
                // return immediately
                yield return StartCoroutine(AnimateRelease());
            }
            else
            {
                // stay down until release
            }

            animRoutine = null;
        }

        IEnumerator AnimateRelease()
        {
            Vector3 start = buttonTop.localPosition;
            Vector3 target = topInitialLocalPos;
            float t = 0f;
            while (t < pressAnimTime)
            {
                t += Time.deltaTime;
                float f = Mathf.SmoothStep(0f, 1f, t / pressAnimTime);
                buttonTop.localPosition = Vector3.Lerp(start, target, f);
                yield return null;
            }
            buttonTop.localPosition = target;
            animRoutine = null;
        }

        void ApplyUpgrade()
        {
            if (playerStats == null)
            {
                if (debugLogs) Debug.Log("[PhysicalStatButton] No PlayerStats assigned; cannot apply upgrade.");
                return;
            }

            switch (stat)
            {
                case StatType.Fishing:
                    playerStats.IncreaseFishing(increment);
                    break;
                case StatType.Strength:
                    playerStats.IncreaseStrength(increment);
                    break;
                case StatType.Luck:
                    playerStats.IncreaseLuck(increment);
                    break;
                case StatType.Trading:
                    playerStats.IncreaseTrading(increment);
                    break;
            }

            if (debugLogs) Debug.Log($"[PhysicalStatButton] Applied {stat} +{increment}");
        }
    }
}