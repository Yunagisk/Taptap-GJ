using System;
using System.Collections;
using UnityEngine;

// First playable art sample. Unsupported weapons retain the existing portrait
// and attack behavior; the timing comes from the same JSON used by the preview.
[DisallowMultipleComponent]
public sealed class PlayerAttackAnimation : MonoBehaviour
{
    const float WeaponSizeMultiplier = 1.2f;
    [SerializeField] PlayerCombatant player;
    [SerializeField] SpriteRenderer fallbackRenderer;
    [SerializeField] TextAsset motionJson;
    [SerializeField] Sprite[] bodyFrames;
    [SerializeField] Sprite weaponSprite;
    [SerializeField] Sprite rigBodySprite;
    [SerializeField] TextAsset swingMotionJson;
    [SerializeField] Sprite torchSprite;
    [SerializeField] TextAsset turnMotionJson;
    [SerializeField] Sprite[] turnFrames;
    [SerializeField] Sprite[] turnHandFrames;
    [SerializeField] Vector3 visualOffset = new Vector3(3.8f, -7.5f, 0f);
    [SerializeField, Min(.01f)] float visualScale = 1.18f;

    PlayerAttackMotion motion;
    PlayerAttackMotion swingMotion;
    Transform visualRoot;
    SpriteRenderer body, weapon;
    PlayerThrustMesh skin;
    PlayerTurnSpriteBody turnBody;
    PlayerSwordEffects effects;
    PlayerTorchMesh torchMesh;
    float idleClock;
    bool initialized, valid, fallbackWasEnabled, playing;
    int playbackVersion;

    void Awake() => Initialize();

    void Update()
    {
        if (!valid || player == null || !Supports(player.CurrentWeapon) || player.IsDead) return;
        idleClock += Time.deltaTime;
        if (!playing && (skin != null || turnBody != null)) ApplyIdle();
    }

    void OnEnable()
    {
        Initialize();
        if (player != null) player.OnWeaponChanged += HandleWeaponChanged;
        RefreshIdle();
    }

    void OnDisable()
    {
        if (player != null) player.OnWeaponChanged -= HandleWeaponChanged;
        playbackVersion++;
        playing = false;
        if (effects != null) effects.Cancel();
        RestoreFallback();
    }

    void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (player == null) player = GetComponent<PlayerCombatant>();
        if (fallbackRenderer == null) fallbackRenderer = GetComponent<SpriteRenderer>();
        fallbackWasEnabled = fallbackRenderer != null && fallbackRenderer.enabled;
        if (motionJson == null || bodyFrames == null || weaponSprite == null || player == null)
            return;
        try { motion = JsonUtility.FromJson<PlayerAttackMotion>(motionJson.text); }
        catch (ArgumentException) { return; }
        if (swingMotionJson != null)
        {
            try { swingMotion = JsonUtility.FromJson<PlayerAttackMotion>(swingMotionJson.text); }
            catch (ArgumentException) { swingMotion = null; }
            if (swingMotion != null && !swingMotion.IsUsable(bodyFrames.Length)) swingMotion = null;
        }
        valid = motion != null && motion.IsUsable(bodyFrames.Length);
        if (valid)
            foreach (Sprite sprite in bodyFrames)
                if (sprite == null) { valid = false; break; }
        if (!valid) return;

        visualRoot = new GameObject("AttackArt").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = visualOffset;
        visualRoot.localScale = Vector3.one * visualScale;
        weapon = CreateLayer("Weapon", 1);
        body = CreateLayer("Body", 2);
        weapon.sprite = weaponSprite;
        body.sprite = bodyFrames[0];
        if (turnMotionJson != null && turnFrames != null && turnFrames.Length > 0)
        {
            var turnRoot = new GameObject("RightHandTurn30FPS");
            turnRoot.transform.SetParent(visualRoot, false);
            var candidate = turnRoot.AddComponent<PlayerTurnSpriteBody>();
            if (candidate.Initialize(turnFrames, turnHandFrames, turnMotionJson, body))
            {
                turnBody = candidate;
                body.enabled = false;
                // The fist overlay covers the handle; the torso/cape must not.
                weapon.sortingOrder = body.sortingOrder + 1;
            }
            else Destroy(turnRoot);
        }
        if (turnBody == null && rigBodySprite != null)
        {
            var meshRoot = new GameObject("ContinuousRig");
            meshRoot.transform.SetParent(visualRoot, false);
            skin = meshRoot.AddComponent<PlayerThrustMesh>();
            skin.Initialize(rigBodySprite, bodyFrames[0], body);
            body.enabled = false;
        }
        if (torchSprite != null && swingMotion != null)
        {
            var torchRoot = new GameObject("TorchArt");
            torchRoot.transform.SetParent(weapon.transform, false);
            torchMesh = torchRoot.AddComponent<PlayerTorchMesh>();
            torchMesh.Initialize(torchSprite, weapon);
            torchRoot.SetActive(false);
        }
        effects = visualRoot.gameObject.AddComponent<PlayerSwordEffects>();
        effects.Initialize(weapon, body);
        visualRoot.gameObject.SetActive(false);
    }

    SpriteRenderer CreateLayer(string layerName, int orderOffset)
    {
        var layer = new GameObject(layerName).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(visualRoot, false);
        if (fallbackRenderer != null)
        {
            layer.sharedMaterial = fallbackRenderer.sharedMaterial;
            layer.sortingLayerID = fallbackRenderer.sortingLayerID;
            layer.sortingOrder = fallbackRenderer.sortingOrder + orderOffset;
            layer.color = fallbackRenderer.color;
        }
        else layer.sortingOrder = orderOffset;
        return layer;
    }

    public bool Supports(WeaponData equippedWeapon)
    {
        return isActiveAndEnabled && valid && equippedWeapon != null
            && (equippedWeapon.WeaponId == "sword"
                || (equippedWeapon.WeaponId == "torch" && swingMotion != null && torchMesh != null && (skin != null || turnBody != null)));
    }

    bool IsSwing => player != null && player.CurrentWeapon != null && player.CurrentWeapon.WeaponId == "torch";
    PlayerAttackMotion CurrentMotion => IsSwing ? swingMotion : motion;
    float AttackDuration => turnBody != null ? turnBody.Duration(IsSwing) : CurrentMotion.duration;
    float AttackHitTime => turnBody != null ? turnBody.HitTime(IsSwing) : CurrentMotion.hitEvents[0].time;

    void HandleWeaponChanged(WeaponData equippedWeapon)
    {
        playbackVersion++;
        playing = false;
        if (effects != null) effects.Cancel();
        RefreshIdle();
    }

    void RefreshIdle()
    {
        if (!isActiveAndEnabled || player == null || !Supports(player.CurrentWeapon))
        {
            RestoreFallback();
            return;
        }
        if (fallbackRenderer != null) fallbackRenderer.enabled = false;
        bool swing = IsSwing;
        weapon.sprite = swing ? torchSprite : weaponSprite;
        weapon.enabled = !swing;
        if (torchMesh != null) torchMesh.gameObject.SetActive(swing);
        if (effects != null) effects.ConfigureWeapon(swing);
        visualRoot.gameObject.SetActive(true);
        if (skin != null || turnBody != null) ApplyIdle();
        else ApplyPose(0f);
    }

    void RestoreFallback()
    {
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        if (fallbackRenderer != null) fallbackRenderer.enabled = fallbackWasEnabled;
    }

    void ApplyPose(float time)
    {
        if (effects != null) effects.SetPoseTime(time);
        if (turnBody != null)
        {
            ApplyWeapon(turnBody.Apply(time, IsSwing, AttackDuration, AttackHitTime, idleClock));
            if (effects != null) effects.SetPoseTime(turnBody.LastFrameTime);
            return;
        }
        if (skin != null)
        {
            var current = CurrentMotion;
            ApplyWeapon(IsSwing ? skin.ApplySwing(time, current.duration, current.hitEvents[0].time, idleClock)
                : skin.Apply(time, current.duration, current.hitEvents[0].time, idleClock, true));
            return;
        }
        PlayerAttackMotion.Pose pose = motion.frames[motion.PoseIndexAt(time)];
        body.sprite = bodyFrames[pose.frame];
        weapon.transform.localPosition = new Vector3(pose.weaponPositionUnity.x, pose.weaponPositionUnity.y, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0f, 0f, pose.weaponRotationZ);
        ApplyWeaponScale(pose.weaponScale);
    }

    void ApplyIdle() => ApplyWeapon(turnBody != null ? turnBody.ApplyIdle(idleClock) : skin.ApplyIdle(idleClock));

    void ApplyWeapon(PlayerThrustRig.Pose pose)
    {
        weapon.transform.localPosition = new Vector3((pose.grip.x - PlayerThrustRig.RootX) / 100f,
            (PlayerThrustRig.RootY - pose.grip.y) / 100f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0f, 0f, pose.weaponAngle - 90f);
        ApplyWeaponScale(CurrentMotion.frames[0].weaponScale);
    }

    void ApplyWeaponScale(float authoredScale)
    {
        float size = authoredScale * WeaponSizeMultiplier;
        // Mirror the sword across its lengthwise axis, around the grip. The
        // blade curve changes sides while the hand and pointing angle stay put.
        // The same transform also keeps the trail registered to the blade.
        weapon.transform.localScale = new Vector3(IsSwing ? size : -size, size, size);
    }

    public void ShowImpact(Combatant target)
    {
        if (playing && target != null && effects != null) effects.PlayImpact(target);
    }

    public IEnumerator PlayAttack(WeaponData equippedWeapon, Func<bool> attackerAlive,
        Func<bool> targetAlive, Action onHit)
    {
        if (!Supports(equippedWeapon) || playing) yield break;
        int version = ++playbackVersion;
        bool hit = false;
        float elapsed = 0f;
        float duration = AttackDuration, hitTime = AttackHitTime;
        float release = turnBody != null ? turnBody.ReleaseTime(IsSwing) : -1f;
        playing = true;
        if (effects != null) effects.BeginAttack(duration, hitTime, release);
        try
        {
            while (true)
            {
                if (this == null || !isActiveAndEnabled || playbackVersion != version
                    || !attackerAlive() || (!hit && !targetAlive()))
                    yield break;

                bool deliveredNow = !hit && elapsed >= hitTime;
                if (deliveredNow)
                {
                    // Even after a long frame, show the impact pose on the same
                    // frame as damage. Recovery still finishes before turn end.
                    ApplyPose(hitTime);
                    hit = true;
                    onHit?.Invoke();
                }
                else ApplyPose(Mathf.Min(elapsed, duration));

                if (elapsed >= duration && !deliveredNow) break;
                yield return null;
                elapsed += Time.deltaTime;
            }
        }
        finally
        {
            if (this != null && playbackVersion == version)
            {
                playing = false;
                if (effects != null) effects.EndAttack();
                RefreshIdle();
            }
        }
    }
}
