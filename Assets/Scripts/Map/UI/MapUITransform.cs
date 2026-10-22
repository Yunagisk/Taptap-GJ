using UnityEngine;

public static class MapUITransform
{
    // Use the local hierarchy up to the shared ancestor. Panning or zooming that
    // ancestor must not introduce floating-point changes into road snapshots.
    public static Matrix4x4 RelativeMatrix(Transform from, Transform to)
    {
        Transform ancestor = to;
        while (ancestor != null && !from.IsChildOf(ancestor)) ancestor = ancestor.parent;
        return ToAncestor(to, ancestor).inverse * ToAncestor(from, ancestor);
    }

    static Matrix4x4 ToAncestor(Transform value, Transform ancestor)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        while (value != ancestor && value != null)
        {
            matrix = Matrix4x4.TRS(value.localPosition, value.localRotation, value.localScale) * matrix;
            value = value.parent;
        }
        return matrix;
    }

    public static Rect Bounds(RectTransform target, Transform space)
    {
        var matrix = RelativeMatrix(target, space);
        Rect rect = target.rect;
        Vector2 min = matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMin));
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 point = matrix.MultiplyPoint3x4(new Vector3(i == 1 ? rect.xMin : rect.xMax, i == 3 ? rect.yMin : rect.yMax));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
