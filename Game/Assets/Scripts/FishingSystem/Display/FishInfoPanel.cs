using TMPro;
using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Simple singleton controller for the floating info panel.
    /// Put one instance in the scene (world-space). The panel stays visible,
    /// but the text only appears when ShowFishData(...) is called.
    /// </summary>
    [DisallowMultipleComponent]
    public class FishInfoPanel : MonoBehaviour
    {
        public static FishInfoPanel Instance { get; private set; }

        [Tooltip("The TextMeshPro (3D) text used to display fish info.")]
        public TMP_Text infoText;

        [Tooltip("If true, the panel will rotate each frame to face Camera.main.")]
        public bool billboardToCamera = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FishInfoPanel] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (infoText == null)
            {
                infoText = GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (infoText == null)
                    Debug.LogWarning("[FishInfoPanel] infoText not assigned and no TMP child found.");
            }

            // Start with empty text (panel visible but no content)
            Clear();
        }

        void Update()
        {
            if (!billboardToCamera) return;
            var cam = Camera.main;
            if (cam == null) return;
            // face the camera while keeping upright
            Vector3 dir = cam.transform.position - transform.position;
            dir.y = 0f; // keep upright
            if (dir.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.LookRotation(-dir);
        }

        /// <summary>
        /// Show fish information. Pass null to clear.
        /// </summary>
        public void ShowFishData(FishData data)
        {
            if (infoText == null) return;

            if (data == null)
            {
                infoText.text = "";
                return;
            }

            string name = (data.speciesSO != null && !string.IsNullOrEmpty(data.speciesSO.displayName))
                ? data.speciesSO.displayName
                : data.speciesId;

            infoText.text =
                $"{name}\n" +
                $"Size: {data.sizeCm:F1} cm\n" +
                $"Quality: {data.qualityDisplay:F1}/10\n" +
                $"Rarity: {data.rarityDisplay:F1}/10\n" +
                $"Price: {data.priceEuros:F2}€" +
                (data.specialTrait ? "\n★ Special" : "");
        }

        /// <summary>
        /// Clear the displayed text (panel remains visible).
        /// </summary>
        public void Clear()
        {
            if (infoText != null) infoText.text = "";
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}