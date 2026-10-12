using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueTester : MonoBehaviour
{

    public DialogueData introData;


    private void Start()
    {
        DialogueManager.Instance
            .StartDialogue(introData);
    }

}
