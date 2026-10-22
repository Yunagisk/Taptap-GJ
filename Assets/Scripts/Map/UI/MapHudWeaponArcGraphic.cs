using System;
using UnityEngine;
using UnityEngine.UI;

// Reuse the existing raster's distressed ink on exact circular UI geometry.
// Frame and stroke positions are never baked into a four-slot texture.
public sealed class MapHudWeaponArcGraphic : MaskableGraphic
{
    public Sprite inkTexture;
    [SerializeField, Range(0, 4)] int slotCount = 4;
    public int SlotCount => slotCount;
    public override Texture mainTexture => inkTexture != null ? inkTexture.texture : base.mainTexture;

    // The first circular rim in WeaponArcFrames.png, in source pixel coordinates.
    const float AtlasWidth = 1248, AtlasHeight = 2048;
    const float SourceX = 719.5f, SourceY = 1724.5f, SourceRadius = 260;
    const int CircleSegments = 128;

    public void SetSlotCount(int count)
    {
        count = Mathf.Clamp(count, 0, MapHudWeaponArcLayout.Capacity);
        if (slotCount == count) return;
        slotCount = count;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        int count = Mathf.Clamp(slotCount, 0, MapHudWeaponArcLayout.Capacity);
        // The full brushstroke belongs to the HUD; only the frames depend on
        // equipment count. Its appearance still follows the parent ink reveal.
        float start = MapHudWeaponArcLayout.Angle(0, MapHudWeaponArcLayout.Capacity) - MapHudWeaponArcLayout.EndPadding;
        float end = MapHudWeaponArcLayout.Angle(MapHudWeaponArcLayout.Capacity - 1, MapHudWeaponArcLayout.Capacity) + MapHudWeaponArcLayout.EndPadding;
        int segments = (int)Math.Ceiling(end - start);
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments, angle = start + (end - start) * t;
            float taper = Math.Min(1, Math.Min(angle - start, end - angle) / MapHudWeaponArcLayout.EndPadding);
            float halfWidth = MapHudWeaponArcLayout.StrokeWidth * .5f * (float)Math.Sqrt(Math.Max(0, taper));
            Add(MapHudWeaponArcLayout.Point(angle, MapHudWeaponArcLayout.BrushRadius - halfWidth), angle, 200);
            Add(MapHudWeaponArcLayout.Point(angle, MapHudWeaponArcLayout.BrushRadius + halfWidth), angle, 260);
            float previousAngle = start + (end - start) * (i - 1f) / segments;
            if (i > 0 && !MapHudWeaponArcLayout.StrokeCovered((previousAngle + angle) * .5f, count)) Quad();
        }
        for (int slot = 0; slot < count; slot++)
        {
            Vector2 center = MapHudWeaponArcLayout.Position(slot, count);
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = 360f * i / CircleSegments;
                double radians = angle * Math.PI / 180;
                var direction = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians));
                Add(center + direction * MapHudWeaponArcLayout.InnerRadius, angle,
                    SourceRadius * MapHudWeaponArcLayout.InnerRadius / MapHudWeaponArcLayout.FrameRadius);
                Add(center + direction * MapHudWeaponArcLayout.FrameRadius, angle, SourceRadius);
                if (i > 0) Quad();
            }
        }

        void Add(Vector2 position, float sourceAngle, float sourceRadius)
        {
            double radians = sourceAngle * Math.PI / 180;
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = new Vector2((SourceX + (float)Math.Cos(radians) * sourceRadius) / AtlasWidth,
                (SourceY + (float)Math.Sin(radians) * sourceRadius) / AtlasHeight);
            mesh.AddVert(vertex);
        }
        void Quad()
        {
            int first = mesh.currentVertCount - 4;
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 1, first + 3, first + 2);
        }
    }
}
