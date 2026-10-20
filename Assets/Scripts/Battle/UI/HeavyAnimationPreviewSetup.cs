using UnityEngine;

// Isolated review controls; never changes a production run or production weapons.
[DefaultExecutionOrder(-1200)]
public sealed class HeavyAnimationPreviewSetup : MonoBehaviour
{
    public WeaponData[] weapons;
    PlayerCombatant player;
    BattleFlowController flow;
    bool selected;
    bool IsPreview => Application.isEditor && gameObject.scene.name == "HeavyAnimationPreview";
    void Awake()
    {
        if (!IsPreview || GameRoot.I != null) return;
        var root=new GameObject("HeavyAnimationPreviewRun").AddComponent<GameRoot>();
        root.state.player.maxHp=root.state.player.hp=1000;
    }
    void LateUpdate()
    {
        if(!IsPreview)return;
        if(player==null)player=FindObjectOfType<PlayerCombatant>();
        if(flow==null)flow=FindObjectOfType<BattleFlowController>();
        if(!selected&&player!=null&&weapons!=null&&weapons.Length>0){player.EquipWeapon(weapons[0]);selected=true;}
    }
    void OnGUI()
    {
        if(!IsPreview||player==null||weapons==null)return;
        GUI.enabled=flow!=null&&flow.CurrentState==BattleState.PlayerTurn;
        for(int i=0;i<weapons.Length;i++)if(weapons[i]!=null)
            if(GUI.Button(new Rect(15+i*128,285,122,34),weapons[i].WeaponId))player.EquipWeapon(weapons[i]);
        GUI.enabled=true;
    }
}
