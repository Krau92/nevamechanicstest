using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Component References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerInputHandler input;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
        if (combat == null)
            combat = GetComponent<PlayerCombat>();
        if (input == null)
            input = GetComponent<PlayerInputHandler>();

        input.Initialize(movement, combat);
        movement.OnJumpExecuted += combat.CancelAttack;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        movement.UpdateTimers(dt);
        combat.UpdateTimers(dt, movement.IsGrounded);
    }

    void FixedUpdate()
    {
        CombatState combatState = combat.GetState();
        movement.MovementControl(combatState, Time.fixedDeltaTime, combat.IsInAttackDuration());
    }

    #endregion
}
