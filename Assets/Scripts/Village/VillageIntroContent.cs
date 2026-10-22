using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Village Introduction")]
public sealed class VillageIntroContent : ScriptableObject
{
    public GameObject dialoguePrefab;
    public TMP_FontAsset font;
    public DialogueData arrivalDialogue;
    public DialogueData streetDialogue;
    public DialogueData mayorApproachDialogue;
    public DialogueData blacksmithDialogue;
    public DialogueData outsideDialogue;
    public DialogueData encounterDialogue;
    public DialogueData returnDialogue;
    public DialogueData reportDialogue;
    public DialogueData switchTutorial, lanternTutorial, resolveTutorial, breakTutorial, itemTutorial;
    public Sprite arrivalBackground;
    public Sprite streetBackground;
    public Sprite mayorBackground;
    public Sprite blacksmithBackground;
    public Sprite outsideBackground;
    public Sprite slothShrineBackground;
    public SmallMonsterData tutorialEnemy;
}
