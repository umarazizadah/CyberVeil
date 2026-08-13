using UnityEngine;
using UnityEngine.UI;

namespace CyberVeil.UI
{
    /// <summary>
    /// Draws one low-opacity crystal texture inside the blue and purple HUD gem faces.
    /// The mesh silhouettes prevent the texture from covering the silver frame or divider.
    /// </summary>
    [AddComponentMenu("CyberVeil/UI/Gem Crystal Texture Overlay")]
    [DisallowMultipleComponent]
    public sealed class GemCrystalTextureOverlay : MaskableGraphic
    {
        private const float ReferenceWidth = 470f;
        private const float ReferenceHeight = 150f;

        private static readonly Vector2[] BlueGemShape =
        {
            new Vector2(0.08f, 0.27f), new Vector2(0.16f, 0.10f),
            new Vector2(0.39f, 0.02f), new Vector2(0.72f, 0.08f),
            new Vector2(0.91f, 0.28f), new Vector2(0.97f, 0.55f),
            new Vector2(0.88f, 0.79f), new Vector2(0.65f, 0.94f),
            new Vector2(0.34f, 0.96f), new Vector2(0.12f, 0.78f),
            new Vector2(0.03f, 0.52f)
        };

        private static readonly Vector2[] PurpleGemShape =
        {
            new Vector2(0.05f, 0.35f), new Vector2(0.16f, 0.13f),
            new Vector2(0.39f, 0.03f), new Vector2(0.68f, 0.08f),
            new Vector2(0.89f, 0.25f), new Vector2(0.97f, 0.52f),
            new Vector2(0.86f, 0.79f), new Vector2(0.63f, 0.95f),
            new Vector2(0.34f, 0.91f), new Vector2(0.10f, 0.70f)
        };

        [Header("Crystal Texture")]
        [SerializeField] private Texture2D crystalTexture;
        [SerializeField] private Color overlayTint = new Color(1f, 1f, 1f, 0.14f);

        public override Texture mainTexture => crystalTexture != null ? crystalTexture : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (crystalTexture == null)
                return;

            Rect parentRect = GetPixelAdjustedRect();
            Color vertexColor = overlayTint * color;

            Rect blueRect = ScaleReferenceRect(parentRect, new Vector2(331f, 76f), new Vector2(68f, 92f));
            Rect purpleRect = ScaleReferenceRect(parentRect, new Vector2(389f, 80f), new Vector2(56f, 80f));

            AddTexturedPolygon(vertexHelper, BlueGemShape, blueRect, vertexColor);
            AddTexturedPolygon(vertexHelper, PurpleGemShape, purpleRect, vertexColor);
        }

        private static Rect ScaleReferenceRect(Rect parent, Vector2 center, Vector2 size)
        {
            float scaleX = parent.width / ReferenceWidth;
            float scaleY = parent.height / ReferenceHeight;
            return new Rect(
                parent.xMin + (center.x - size.x * 0.5f) * scaleX,
                parent.yMin + (center.y - size.y * 0.5f) * scaleY,
                size.x * scaleX,
                size.y * scaleY);
        }

        private static void AddTexturedPolygon(
            VertexHelper vertexHelper,
            Vector2[] normalizedPoints,
            Rect targetRect,
            Color vertexColor)
        {
            int firstVertex = vertexHelper.currentVertCount;
            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                Vector2 point = normalizedPoints[i];
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(
                    Mathf.LerpUnclamped(targetRect.xMin, targetRect.xMax, point.x),
                    Mathf.LerpUnclamped(targetRect.yMin, targetRect.yMax, point.y));
                vertex.color = vertexColor;
                vertex.uv0 = point;
                vertexHelper.AddVert(vertex);
            }

            for (int i = 1; i < normalizedPoints.Length - 1; i++)
                vertexHelper.AddTriangle(firstVertex, firstVertex + i, firstVertex + i + 1);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            SetAllDirty();
        }
#endif
    }
}
