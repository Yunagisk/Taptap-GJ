using UnityEngine;

// Only the isolated flame layer bends; the shaft and both grip points stay rigid.
public sealed class PlayerHeavyFlameMesh : MonoBehaviour
{
    const int Grid = 16;
    Sprite sprite;
    Mesh mesh;
    MeshRenderer appearance;
    Vector3[] vertices;
    int lastTick = -1;
    Material material;
    int layer, order;
    public void Initialize(SpriteRenderer weapon)
    {
        material = weapon.sharedMaterial; layer = weapon.sortingLayerID; order = weapon.sortingOrder;
    }
    public void SetSprite(Sprite value)
    {
        if (sprite == value) return;
        sprite = value;
        if (sprite == null) { if (appearance != null) appearance.enabled = false; return; }
        if (mesh == null)
        {
            mesh = new Mesh { name = "HeavyFlameLayer30FPS" }; mesh.MarkDynamic();
            vertices = new Vector3[(Grid+1)*(Grid+1)];
            var triangles = new int[Grid*Grid*6]; int k=0;
            for(int y=0;y<Grid;y++)for(int x=0;x<Grid;x++)
            {
                int a=y*(Grid+1)+x,b=a+1,c=a+Grid+1;
                triangles[k++]=a;triangles[k++]=b;triangles[k++]=c;triangles[k++]=b;triangles[k++]=c+1;triangles[k++]=c;
            }
            mesh.vertices=vertices;mesh.triangles=triangles;
            var colors=new Color32[vertices.Length];for(int i=0;i<colors.Length;i++)colors[i]=new Color32(255,255,255,255);mesh.colors32=colors;
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            appearance=gameObject.AddComponent<MeshRenderer>();appearance.sharedMaterial=material;appearance.sortingLayerID=layer;appearance.sortingOrder=order;
        }
        var uv=new Vector2[vertices.Length];Rect rect=sprite.textureRect;Vector2 trim=sprite.textureRectOffset;
        for(int y=0;y<=Grid;y++)for(int x=0;x<=Grid;x++)uv[y*(Grid+1)+x]=new Vector2(
            (rect.x-trim.x+x/(float)Grid*sprite.rect.width)/sprite.texture.width,
            (rect.y-trim.y+y/(float)Grid*sprite.rect.height)/sprite.texture.height);
        mesh.uv=uv;
        var block=new MaterialPropertyBlock();block.SetTexture("_MainTex",sprite.texture);block.SetColor("_Color",Color.white);
        block.SetColor("_RendererColor",Color.white);block.SetVector("_Flip",new Vector4(1,1,1,1));appearance.SetPropertyBlock(block);
        appearance.enabled=true;lastTick=-1;Apply();
    }
    void LateUpdate() { if(sprite!=null)Apply(); }
    void Apply()
    {
        int tick=Mathf.FloorToInt(Time.time*30f);if(tick==lastTick)return;lastTick=tick;float time=tick/30f;
        for(int y=0;y<=Grid;y++)for(int x=0;x<=Grid;x++)
        {
            float px=x/(float)Grid*sprite.rect.width,py=y/(float)Grid*sprite.rect.height;
            float weight=py/sprite.rect.height;
            vertices[y*(Grid+1)+x]=new Vector3((px-sprite.pivot.x+Mathf.Sin(time*7f+py*.015f)*10f*weight)/sprite.pixelsPerUnit,
                (py-sprite.pivot.y+Mathf.Sin(time*9f+px*.02f)*5f*weight)/sprite.pixelsPerUnit,0);
        }
        mesh.SetVertices(vertices);mesh.RecalculateBounds();
    }
    void OnDestroy() { if(mesh!=null)Destroy(mesh); }
}
