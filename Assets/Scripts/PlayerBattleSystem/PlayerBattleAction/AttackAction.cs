using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackAction : IBattleAction
{
    public IEnumerator Execute(Combatant user, Combatant target)
    {
        for (int i = 0; i < user.AttackCount; i++)
        {
            if (target.IsDead)
            {
                yield break;
            }

            Debug.Log($"{user.gameObject.name}攻击{target.gameObject.name}，造成{user.Attack}点伤害");

            target.TakeDamage(user.Attack);
            yield return new WaitForSeconds(0.3f);
        }

    }

}
