using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Keeps a UI element positioned over a world-space target without requiring extra helper objects.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldSpaceHudAnchor : MonoBehaviour
    {
        private static readonly Vector2 ScreenPadding = new Vector2(0.5f, 0.5f);

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] private bool hideWhenOffscreen = true;
        [SerializeField] private Canvas explicitCanvas;
        [SerializeField] private CanvasGroup visibilityGroup;

        private RectTransform rectTransform;
        private Canvas cachedCanvas;
        private Camera cachedCamera;
        private Vector3 originalScale;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector3 WorldOffset
        {
            get => worldOffset;
            set => worldOffset = value;
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;

            cachedCanvas = explicitCanvas != null ? explicitCanvas : rectTransform.GetComponentInParent<Canvas>();

            if (cachedCanvas == null)
            {
                Debug.LogWarning($"{nameof(WorldSpaceHudAnchor)} requires the HUD to live under a Canvas.", this);
                enabled = false;
                return;
            }

            if (visibilityGroup == null)
            {
                visibilityGroup = GetComponent<CanvasGroup>();
            }

            cachedCamera = ResolveCamera(cachedCanvas);
        }

        private void LateUpdate()
        {
            if (target == null || cachedCanvas == null)
            {
                SetVisible(false);
                return;
            }

            if (cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay && cachedCamera == null)
            {
                cachedCamera = ResolveCamera(cachedCanvas);
                if (cachedCamera == null)
                {
                    SetVisible(false);
                    return;
                }
            }

            Vector3 worldPosition = target.position + worldOffset;
            Camera cameraToUse = cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? Camera.main : cachedCamera;
            if (cameraToUse == null)
            {
                cameraToUse = Camera.main;
            }

            Vector3 screenPoint = cameraToUse != null
                ? cameraToUse.WorldToScreenPoint(worldPosition)
                : new Vector3(float.NegativeInfinity, float.NegativeInfinity, -1f);

            bool isVisible = screenPoint.z > 0f;
            if (isVisible && hideWhenOffscreen)
            {
                bool insideScreen =
                    screenPoint.x >= -ScreenPadding.x &&
                    screenPoint.x <= Screen.width + ScreenPadding.x &&
                    screenPoint.y >= -ScreenPadding.y &&
                    screenPoint.y <= Screen.height + ScreenPadding.y;
                isVisible = insideScreen;
            }

            SetVisible(isVisible || !hideWhenOffscreen);

            if (!isVisible)
            {
                return;
            }

            switch (cachedCanvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    rectTransform.position = screenPoint;
                    break;
                case RenderMode.ScreenSpaceCamera:
                case RenderMode.WorldSpace:
                    RectTransform canvasRect = cachedCanvas.transform as RectTransform;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cachedCamera,
                            out Vector2 localPoint))
                    {
                        rectTransform.localPosition = localPoint;
                    }
                    break;
            }
        }

        private static Camera ResolveCamera(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            return Camera.main;
        }

        private void SetVisible(bool value)
        {
            if (visibilityGroup != null)
            {
                visibilityGroup.alpha = value ? 1f : 0f;
                visibilityGroup.interactable = value;
                visibilityGroup.blocksRaycasts = value;
                return;
            }

            rectTransform.localScale = value ? originalScale : Vector3.zero;
        }
    }
}
