# Neva Movement Mechanics Test

Design analysis of the player controller, movement, and combat systems.

---

## Movement

### Walking, Running, and Air Movement

Movement is built on an acceleration-based model rather than instantaneous velocity changes. Ground acceleration is aggressive for responsive feel, while air acceleration is deliberately reduced to penalize airborne directional changes and reward planning. Deceleration is higher than acceleration, giving the player tight stopping control without drift.

A sprint multiplier linearly scales speed and max velocity, layered on top of the base acceleration curve. An apex boost injects a small horizontal velocity kick when the player nears the peak of a jump, removing the floaty dead zone and preserving air momentum through upward arcs.

```csharp
// Movement is constrained dynamically by combat state.
// During attacks, horizontal drag replaces normal acceleration,
// while the combo multiplier scales down both accel and decel
// to produce a heavier, committed feel.
public void MovementControl(CombatState combatState, float fixedDt, bool isInAttackDuration)
{
    if (isInAttackDuration || combatState.Phase == CombatPhase.SpikeAttack)
    {
        velocity.x = Mathf.MoveTowards(velocity.x, 0f, combatState.HorizontalDrag * fixedDt);
    }
    else if (!isDashing)
    {
        float accel = IsGrounded ? acceleration : airAcceleration;
        if (combatState.Phase != CombatPhase.NotAttacking)
        {
            accel *= comboAccelMultiplier;
        }
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * fixedDt);
    }
}
```

### Jumping Mechanics and Feeling

The jump system uses several forgiveness and feel techniques:

- **Coyote time**: a brief window after leaving a ledge during which jump is still accepted, preventing frustration from late inputs.
- **Input buffer**: if jump is pressed slightly before landing, it is queued and executed the moment the player touches ground.
- **Variable-height jump**: on release, vertical velocity is multiplied down rather than cut to zero, giving the player graduated control over jump height without binary on/off behavior.
- **Multi-jump**: a second (double) jump is available once airborne, with slightly reduced force to differentiate it visually and mechanically.
- **Gravity curve**: gravity ramps across three altitude phases — lightened at apex for a moment of hang-time suspension, intensified during the fall for fast descent, and further amplified if the player releases jump early to ensure the character drops quickly.

<div align="center">
<img src="ReadmeImages/Movement.gif" width="600">
<br> <br>
</div>

```csharp
// Variable-height uses a percentage cut, not a hard clamp.
// This preserves momentum feel while still giving low-jump control.
public void OnJumpCanceled()
{
    if (isJumping && rb.linearVelocity.y > 0f)
    {
        Vector2 v = rb.linearVelocity;
        v.y *= variableJumpCut;
        rb.linearVelocity = v;
        isJumping = false;
    }
}
```

Jumping also **cancels any active combat** — the player cannot attack while jumping, which creates a clear risk/reward separation between engagement and evasion.

### Dashing

Dash is a quick impulse that applies horizontal force and shrinks the collider vertically, letting the player slide under low obstacles. It is available once on the ground and once in the air. During the dash, normal acceleration is replaced by a gentler deceleration curve that provides a controlled slide-out feel rather than an abrupt stop. The dash cannot be used during an attack, and attacking during a dash is blocked — each system mutually excludes the other.

<div align="center">
<img src="ReadmeImages/Dash.gif" width="600">
<br> <br>
</div>

---

## Combat

### Combos

Attacks are organized as a three-hit melee chain. Each press transitions the phase forward if the previous attack's duration has elapsed and the combo window is still open. The third hit is a finisher with a longer duration and shorter follow-up window, and a cooldown prevents re-entering the chain immediately.

```csharp
// The combo is a simple phase machine: each attack press
// advances the phase as long as timing windows permit.
public void OnAttack(MovementState movementState, float verticalInput)
{
    if (movementState.IsDashing) return;

    switch (currentPhase)
    {
        case CombatPhase.NotAttacking:
            TransitionTo(CombatPhase.Combo1);
            SpawnMelee(weaponSpawnPoint);
            break;
        case CombatPhase.Combo1:
            if (attackDurationTimer <= 0f && (comboWindowTimer > 0f || !movementState.IsGrounded))
            {
                TransitionTo(CombatPhase.Combo2);
                SpawnMelee(weaponSpawnPoint);
            }
            break;
        case CombatPhase.Combo2:
            if (attackDurationTimer <= 0f && (comboWindowTimer > 0f || !movementState.IsGrounded))
            {
                TransitionTo(CombatPhase.Combo3);
                SpawnMelee(weaponSpawnPoint);
            }
            break;
    }
}
```

**How combos affect movement**: during any combo phase, horizontal and vertical drag are applied to the player's velocity, locking them in place for the attack duration. The combo movement multiplier further reduces acceleration and deceleration, meaning the player is intentionally sluggish during attacks. The third hit is the most committal — its drag values are highest and its window shortest, forcing the player to choose the finisher carefully. This creates a meaningful tradeoff: dealing damage costs positional control.

<div align="center">
<img src="ReadmeImages/Combos.gif" width="600">
<br> <br>
</div>

The linger mechanic (combo window remains open while airborne) allows the player to complete the chain even after being knocked or falling off a platform, chaining into a landing follow-up.

### Spike Attack

The spike is a ground-pounding downward strike performed by attacking while airborne with downward input. It freezes horizontal movement completely, zeroes gravity during the forward part of the animation, then applies extreme downward gravity to rocket the player toward the ground. The weapon spawns at the player's feet, rotated for a downward strike visual.

<div align="center">
<img src="ReadmeImages/Spike.gif" width="600">
<br> <br>
</div>

The spike is the ultimate committal action — the player surrenders all air control and cannot cancel once initiated. It trades safety for a powerful descending hit that can reach enemies or breakables below.

```csharp
// Spike attack overrides gravity entirely:
// zero during the attack window, then extreme multiplier
// to slam the player to the ground instantly.
private void DoSpikeAttack()
{
    TransitionTo(CombatPhase.SpikeAttack);
    GameObject spikeWeapon = Instantiate(meleePrefab, feetSpawnPoint.position, transform.rotation, transform);
    spikeWeapon.transform.Rotate(0f, 0f, -90f);
}
```

---

## Conclusions

The design ties movement and combat into a single interdependent system where every action has a meaningful opportunity cost:

| Action | What you lose |
|---|---|
| Attacking | Mobility (drag applies), air control |
| Spike attacking | All movement and gravity control |
| Jumping | Current combo chain |
| Dashing | Cannot attack during, cannot attack into |

This creates a deliberate rhythm: position yourself (movement), commit to damage (combat), escape or reposition (movement again). The player must decide when to fight and when to flee because doing both simultaneously is penalized.

The architecture separates concerns cleanly: `PlayerController` orchestrates, `PlayerMovement` owns physics, `PlayerCombat` owns phase logic, and `PlayerInputHandler` bridges input to both. Read-only state structs (`MovementState`, `CombatState`) flow from one system to another without coupling. All tuning lives in the prefab, making iteration rapid without recompilation.

The result is a controller that feels responsive and forgiving in movement (coyote time, input buffer, apex boost) but deliberate and committal in combat (drag, spike freeze, combo lock), giving each action distinct weight and consequence.
