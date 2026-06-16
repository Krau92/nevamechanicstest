
using UnityEngine;

[System.Serializable]
public struct ComboStats
{
    public float AttackDuration;
    public float ComboWindow;
    public float HorizontalDrag;
    public float VerticalDrag;
    public float FallGravityMultiplier;
}

public class PlayerCombat : MonoBehaviour
{
    #region Serialized Fields

    [Header("Weapons")]
    [SerializeField] private Transform weaponSpawnPoint;
    [SerializeField] private Transform feetSpawnPoint;
    [SerializeField] private GameObject meleePrefab;
    [SerializeField] private GameObject rangedPrefab;

    [Header("Cooldowns")]
    [SerializeField] private float rangedCooldown = 0.5f;
    [SerializeField] private float comboCooldown = 0.75f;

    [Header("Combo Stats")]
    [SerializeField] private ComboStats combo1Stats = new ComboStats
    {
        AttackDuration = 0.25f,
        ComboWindow = 0.75f,
        HorizontalDrag = 40f,
        VerticalDrag = 50f,
        FallGravityMultiplier = 0.1f
    };
    [SerializeField] private ComboStats combo2Stats = new ComboStats
    {
        AttackDuration = 0.25f,
        ComboWindow = 0.75f,
        HorizontalDrag = 40f,
        VerticalDrag = 50f,
        FallGravityMultiplier = 0.1f
    };
    [SerializeField] private ComboStats combo3Stats = new ComboStats
    {
        AttackDuration = 0.25f,
        ComboWindow = 0.5f,
        HorizontalDrag = 80f,
        VerticalDrag = 80f,
        FallGravityMultiplier = 1f
    };
    [SerializeField] private ComboStats spikeStats = new ComboStats
    {
        AttackDuration = 0.5f,
        ComboWindow = 0f,
        HorizontalDrag = 0f,
        VerticalDrag = 0f,
        FallGravityMultiplier = 7f
    };

    #endregion

    #region Private Fields

    private float rangedCooldownTimer;

    private CombatPhase currentPhase = CombatPhase.NotAttacking;
    private float attackDurationTimer;
    private float comboWindowTimer;
    private float comboTimer;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (weaponSpawnPoint == null)
            weaponSpawnPoint = transform;
        if (feetSpawnPoint == null)
            feetSpawnPoint = transform;

        if (meleePrefab == null)
            Debug.LogWarning("meleePrefab is not assigned", this);
        if (rangedPrefab == null)
            Debug.LogWarning("rangedPrefab is not assigned", this);
    }

    #endregion

    #region Controller Callbacks

    public void UpdateTimers(float dt, bool isGrounded)
    {
        if (rangedCooldownTimer > 0f)
            rangedCooldownTimer -= dt;

        if (comboTimer > 0f)
            comboTimer -= dt;

        if (currentPhase == CombatPhase.NotAttacking)
            return;

        attackDurationTimer -= dt;
        comboWindowTimer -= dt;

        if (currentPhase == CombatPhase.SpikeAttack)
        {
            if (comboWindowTimer <= 0f && isGrounded)
                TransitionTo(CombatPhase.NotAttacking);

            return;
        }

        if (comboWindowTimer <= 0f && isGrounded || currentPhase == CombatPhase.Combo3 && comboTimer <= 0)
            TransitionTo(CombatPhase.NotAttacking);
    }

    public void OnAttack(MovementState movementState, float verticalInput)
    {
        if (movementState.IsDashing)
            return;

        if (!movementState.IsGrounded && verticalInput < 0f && currentPhase != CombatPhase.SpikeAttack)
        {
            DoSpikeAttack();
            return;
        }

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

            case CombatPhase.Combo3:
                break;

            case CombatPhase.SpikeAttack:
                break;
        }
    }

    public void OnShoot(MovementState movementState)
    {
        if (rangedCooldownTimer <= 0f && rangedPrefab != null && !movementState.IsDashing)
        {
            Instantiate(rangedPrefab, weaponSpawnPoint.position, transform.rotation);
            rangedCooldownTimer = rangedCooldown;
        }
    }

    public bool IsInAttackDuration()
    {
        return TryGetActiveStats(out _);
    }

    public void CancelAttack()
    {
        if (currentPhase != CombatPhase.NotAttacking)
            TransitionTo(CombatPhase.NotAttacking);
    }

    public CombatState GetState()
    {
        if (TryGetActiveStats(out var stats))
            return new CombatState(currentPhase, stats.HorizontalDrag, stats.VerticalDrag, stats.FallGravityMultiplier);

        return new CombatState(currentPhase, 0f, 0f, 1f);
    }

    #endregion

    #region Private Methods

    private void TransitionTo(CombatPhase phase)
    {
        currentPhase = phase;

        if (phase == CombatPhase.NotAttacking)
        {
            attackDurationTimer = 0f;
            comboWindowTimer = 0f;
            return;
        }

        var stats = GetPhaseStats(phase);
        attackDurationTimer = stats.AttackDuration;
        comboWindowTimer = stats.ComboWindow;
        comboTimer = comboCooldown;
    }

    private bool TryGetActiveStats(out ComboStats stats)
    {
        stats = default;

        switch (currentPhase)
        {
            case CombatPhase.NotAttacking:
                return false;

            case CombatPhase.SpikeAttack:
                stats = spikeStats;
                if (attackDurationTimer <= 0f)
                    return false;
                return true;

            default:
                if (comboWindowTimer >= 0f)
                {
                    stats = GetPhaseStats(currentPhase);
                    if (attackDurationTimer >= 0)
                    return true;
                } 
                return false;
        }
    }

    private ComboStats GetPhaseStats(CombatPhase phase)
    {
        switch (phase)
        {
            case CombatPhase.Combo1:
                return combo1Stats;
            case CombatPhase.Combo2:
                return combo2Stats;
            case CombatPhase.Combo3:
                return combo3Stats;
            case CombatPhase.SpikeAttack:
                return spikeStats;
            default:
                return default;
        }
    }

    private void DoSpikeAttack()
    {
        TransitionTo(CombatPhase.SpikeAttack);
        if (meleePrefab == null)
            return;

        GameObject spikeWeapon = Instantiate(meleePrefab, feetSpawnPoint.position, transform.rotation, transform);
        spikeWeapon.transform.localPosition = feetSpawnPoint.localPosition;
        spikeWeapon.transform.Rotate(0f, 0f, -90f);
    }

    private void SpawnMelee(Transform spawnPoint)
    {
        if (meleePrefab == null)
            return;

        GameObject melee = Instantiate(meleePrefab, spawnPoint.position, transform.rotation, transform);
        melee.transform.localPosition = spawnPoint.localPosition;
    }

    #endregion
}
