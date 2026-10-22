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
    [SerializeField] PlayerHeavyAnimationSet heavyAnimations;
    [SerializeField] PlayerHeavyAnimationSet poleAnimations;
    [SerializeField] PlayerOneHandWeaponSet oneHandWeapons;
    [SerializeField] Vector3 visualOffset = new Vector3(3.8f, -7.5f, 0f);
    [SerializeField, Min(.01f)] float visualScale = 1.18f;
    // Alpha bounds of the registered ready drawing, in its 100 px/unit rig.
    [SerializeField] Rect bodyVisualRect = new Rect(-7.56f, -.08f, 8.36f, 12.72f);

    PlayerAttackMotion motion;
    PlayerAttackMotion swingMotion;
    Transform visualRoot;
    SpriteRenderer body, weapon, heavyShaft;
    PlayerThrustMesh skin;
    PlayerTurnSpriteBody turnBody;
    PlayerTurnSpriteBody heavyBody;
    PlayerTurnSpriteBody poleBody;
    PlayerHeavyEffects heavyEffects;
    PlayerHeavyFlameMesh heavyFlames;
    Transform heavyRoot;
    Transform poleRoot;
    PlayerSwordEffects effects;
    PlayerTorchMesh torchMesh;
    float idleClock;
    bool initialized, valid, fallbackWasEnabled, playing;
    int playbackVersion;

    // Mesh bounds include the transparent canvas; use the painted body for effects.
    public bool TryGetBodyVisual(out SpriteRenderer appearance, out Bounds localBounds)
    {
        appearance = body;
        localBounds = new Bounds(bodyVisualRect.center, bodyVisualRect.size);
        return valid && visualRoot != null && visualRoot.gameObject.activeInHierarchy && body != null;
    }

    void Awake() => Initialize();

    void Update()
    {
        if (!valid || player == null || !Supports(player.CurrentWeapon) || player.IsDead) return;
        idleClock += Time.deltaTime;
        if (!playing && (skin != null || turnBody != null || heavyBody != null || poleBody != null)) ApplyIdle();
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
        if (heavyEffects != null) heavyEffects.Cancel();
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
        if (heavyAnimations != null || oneHandWeapons != null)
        {
            var flameRoot = new GameObject("WeaponFlames");
            flameRoot.transform.SetParent(weapon.transform, false);
            heavyFlames = flameRoot.AddComponent<PlayerHeavyFlameMesh>();
            heavyFlames.Initialize(weapon);
        }
        if (heavyAnimations != null)
        {
            heavyRoot = new GameObject("TwoHandHeavy30FPS").transform;
            heavyRoot.SetParent(visualRoot, false);
            var candidate = heavyRoot.gameObject.AddComponent<PlayerTurnSpriteBody>();
            if (candidate.Initialize(heavyAnimations.bodyFrames, heavyAnimations.handFrames, heavyAnimations.motion, body))
            {
                heavyBody = candidate;
                heavyShaft = CreateLayer("HeavyWeaponShaft", 3);
                heavyShaft.enabled = false;
            }
            else Destroy(heavyRoot.gameObject);
            heavyRoot.gameObject.SetActive(false);
        }
        if (poleAnimations != null)
        {
            poleRoot = new GameObject("TwoHandPole30FPS").transform;
            poleRoot.SetParent(visualRoot, false);
            var candidate = poleRoot.gameObject.AddComponent<PlayerTurnSpriteBody>();
            if (candidate.Initialize(poleAnimations.bodyFrames, poleAnimations.handFrames, poleAnimations.motion, body))
                poleBody = candidate;
            else Destroy(poleRoot.gameObject);
            poleRoot.gameObject.SetActive(false);
        }
        if (heavyBody != null || poleBody != null)
        {
            heavyEffects = visualRoot.gameObject.AddComponent<PlayerHeavyEffects>();
            heavyEffects.Initialize(weapon, body);
        }
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
            && ((poleBody != null && poleAnimations.Find(equippedWeapon.WeaponId) != null)
                || (heavyBody != null && heavyAnimations.Find(equippedWeapon.WeaponId) != null) || equippedWeapon.WeaponId == "sword"
                || (oneHandWeapons != null && oneHandWeapons.Find(equippedWeapon.WeaponId) != null && turnBody != null
                    && (!oneHandWeapons.Find(equippedWeapon.WeaponId).swing || swingMotion != null))
                || (equippedWeapon.WeaponId == "torch" && swingMotion != null && torchMesh != null && (skin != null || turnBody != null)));
    }

    PlayerOneHandWeaponSet.Weapon OneHandWeapon => oneHandWeapons != null && player != null && player.CurrentWeapon != null
        ? oneHandWeapons.Find(player.CurrentWeapon.WeaponId) : null;
    bool IsTorch => player != null && player.CurrentWeapon != null && player.CurrentWeapon.WeaponId == "torch";
    bool IsSwing => IsTorch || (OneHandWeapon != null && OneHandWeapon.swing);
    PlayerHeavyAnimationSet.Weapon PoleWeapon => poleBody != null && player != null && player.CurrentWeapon != null
        ? poleAnimations.Find(player.CurrentWeapon.WeaponId) : null;
    PlayerHeavyAnimationSet.Weapon HeavyWeapon => PoleWeapon ?? (heavyBody != null && player != null && player.CurrentWeapon != null
        ? heavyAnimations.Find(player.CurrentWeapon.WeaponId) : null);
    PlayerTurnSpriteBody TwoHandBody => PoleWeapon != null ? poleBody : heavyBody;
    bool IsHeavy => HeavyWeapon != null;
    PlayerAttackMotion CurrentMotion => IsSwing ? swingMotion : motion;
    float AttackDuration => IsHeavy ? HeavyWeapon.duration : turnBody != null ? turnBody.Duration(IsSwing) : CurrentMotion.duration;
    float AttackHitTime => IsHeavy ? TwoHandBody.HitTime(false) * AttackDuration / TwoHandBody.Duration(false)
        : turnBody != null ? turnBody.HitTime(IsSwing) : CurrentMotion.hitEvents[0].time;

    void HandleWeaponChanged(WeaponData equippedWeapon)
    {
        playbackVersion++;
        playing = false;
        if (effects != null) effects.Cancel();
        if (heavyEffects != null) heavyEffects.Cancel();
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
        bool heavy = IsHeavy;
        if (heavyRoot != null) heavyRoot.gameObject.SetActive(heavy && PoleWeapon == null);
        if (poleRoot != null) poleRoot.gameObject.SetActive(PoleWeapon != null);
        if (turnBody != null) turnBody.gameObject.SetActive(!heavy);
        if (skin != null) skin.gameObject.SetActive(!heavy);
        bool swing = IsSwing;
        weapon.sprite = heavy ? HeavyWeapon.sprite : OneHandWeapon != null ? OneHandWeapon.sprite : IsTorch ? torchSprite : weaponSprite;
        weapon.enabled = !IsTorch;
        if (heavyShaft != null)
        {
            heavyShaft.sprite = heavy ? HeavyWeapon.shaft : null;
            heavyShaft.enabled = heavy && heavyShaft.sprite != null;
        }
        if (torchMesh != null) torchMesh.gameObject.SetActive(!heavy && IsTorch);
        if (heavyFlames != null) heavyFlames.SetSprite(heavy ? HeavyWeapon.flames : OneHandWeapon != null ? OneHandWeapon.flames : null);
        if (heavyEffects != null) { heavyEffects.Configure(HeavyWeapon); if (!heavy) heavyEffects.Cancel(); }
        if (effects != null)
        {
            effects.ConfigureWeapon(IsTorch);
            effects.ConfigureTrail(OneHandWeapon != null ? OneHandWeapon.trailHeel : IsTorch ? new Vector2(0, 4.9f) : new Vector2(0, 1.5f),
                OneHandWeapon != null ? OneHandWeapon.trailTip : IsTorch ? new Vector2(.3f, 8.2f) : new Vector2(-.21f, 9.96f));
        }
        visualRoot.gameObject.SetActive(true);
        if (skin != null || turnBody != null || heavyBody != null || poleBody != null) ApplyIdle();
        else ApplyPose(0f);
    }

    void RestoreFallback()
    {
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        if (fallbackRenderer != null) fallbackRenderer.enabled = fallbackWasEnabled;
    }

    void ApplyPose(float time)
    {
        if (IsHeavy)
        {
            float advance = PoleWeapon != null ? PlayerPoleThrust.Advance(time, AttackDuration,
                TwoHandBody.ReleaseTime(false) * AttackDuration / TwoHandBody.Duration(false), AttackHitTime) : 0;
            ApplyHeavyWeapon(TwoHandBody.Apply(time, false, AttackDuration, AttackHitTime, idleClock, advance));
            heavyEffects.SetPoseTime(TwoHandBody.LastFrameTime);
            return;
        }
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

    void ApplyIdle()
    {
        if (IsHeavy) ApplyHeavyWeapon(TwoHandBody.ApplyIdle(idleClock));
        else if (turnBody != null || skin != null)
            ApplyWeapon(turnBody != null ? turnBody.ApplyIdle(idleClock) : skin.ApplyIdle(idleClock));
        else ApplyPose(0f);
    }

    void ApplyHeavyWeapon(PlayerThrustRig.Pose pose)
    {
        weapon.sortingOrder = body.sortingOrder + 1;
        weapon.transform.localPosition = new Vector3((pose.grip.x - PlayerThrustRig.RootX) / 100f,
            (PlayerThrustRig.RootY - pose.grip.y) / 100f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0f, 0f, pose.weaponAngle - 90f);
        if (TwoHandBody.LastWeaponReturning)
        {
            // A rigid sideways turn. The blade and crossguard keep their
            // original dimensions instead of uniformly shrinking together.
            var yaw = Quaternion.Euler(0f, TwoHandBody.LastWeaponYaw, 0f);
            weapon.transform.localRotation = yaw
                * Quaternion.Euler(0f, 0f, -90f - TwoHandBody.LastWeaponPitch) * yaw;
        }
        var mount = HeavyWeapon;
        weapon.transform.localScale = new Vector3(mount.scale * mount.FacingAt(TwoHandBody.LastFrameTime), mount.scale, mount.scale);
        if (heavyShaft != null && heavyShaft.enabled)
        {
            // A circular shaft keeps its width while the flat blade turns.
            heavyShaft.sortingOrder = weapon.sortingOrder;
            heavyShaft.transform.localPosition = weapon.transform.localPosition;
            heavyShaft.transform.localRotation = weapon.transform.localRotation;
            heavyShaft.transform.localScale = Vector3.one * mount.scale;
        }
    }

    void ApplyWeapon(PlayerThrustRig.Pose pose)
    {
        weapon.transform.localPosition = new Vector3((pose.grip.x - PlayerThrustRig.RootX) / 100f,
            (PlayerThrustRig.RootY - pose.grip.y) / 100f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0f, 0f, pose.weaponAngle - 90f);
        ApplyWeaponScale(OneHandWeapon != null ? OneHandWeapon.scale : CurrentMotion.frames[0].weaponScale);
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
        if (playing && target != null)
        {
            if (IsHeavy && heavyEffects != null) heavyEffects.PlayImpact(target);
            else if (effects != null) effects.PlayImpact(target);
        }
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
        bool heavy = IsHeavy;
        if (heavy) heavyEffects.BeginAttack(TwoHandBody.ReleaseTime(false), TwoHandBody.HitTime(false));
        else if (effects != null) effects.BeginAttack(duration, hitTime, release);
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
                if (heavyEffects != null) heavyEffects.EndAttack();
                RefreshIdle();
            }
        }
    }
}
