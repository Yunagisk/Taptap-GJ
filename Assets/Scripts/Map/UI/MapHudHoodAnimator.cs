using UnityEngine;

// Render order: fixed disk, rear cloth, stationary head, front cloth. The cloth
// passes behind the hair while its brim travels over the crown.
[DisallowMultipleComponent]
public sealed class MapHudHoodAnimator : MonoBehaviour
{
    public MapHudHoodLayerEffect backCloth;
    public MapHudHoodLayerEffect frontCloth;
    public MapHudHoodLayerEffect head;

    public void SetPose(float progress)
    {
        if (backCloth != null) backCloth.SetPose(progress);
        if (frontCloth != null) frontCloth.SetPose(progress);
        if (head != null) head.SetPose(progress);
    }
}
