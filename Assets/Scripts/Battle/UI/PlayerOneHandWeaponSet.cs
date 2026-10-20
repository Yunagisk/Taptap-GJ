using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Player One Hand Weapon Variants")]
public sealed class PlayerOneHandWeaponSet : ScriptableObject
{
    [Serializable] public sealed class Weapon
    {
        public string id;
        public bool swing;
        public Sprite sprite, flames;
        public float scale = .55f;
        public Vector2 trailHeel, trailTip;
    }
    public Weapon[] weapons;
    public Weapon Find(string id)
    {
        if (weapons != null) foreach (var weapon in weapons)
            if (weapon != null && weapon.id == id && weapon.sprite != null) return weapon;
        return null;
    }
}
