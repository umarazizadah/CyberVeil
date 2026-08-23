using UnityEngine;
using CyberVeil.Core;
using CyberVeil.Systems;
using System.Collections;
using System.Collections.Generic;
using CyberVeil.Combat;

namespace CyberVeil.Enemies
{
    /// <summary>
    /// AI state controller (central brain) for enemy behavior, 
    /// controls high-level enemy AI behavior by managing movement, targeting, and state transitions
    /// Supports a modular, multi attack system via the EnemyAttackSelector component and ScriptableObject based attack data
    /// Delegates movement and animation control through CharacterStateMachine
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        [Header("Movement")]
        public float speed = 1f;

        [Header("Ranges")]
        public float detectionRange = 4f;

        [Header("Strafe Settings")]
        [Tooltip("Chance to attempt a strafe each evaluation (0-1)")]
        public float strafeChance = 0.03f;
        [Tooltip("Minimum seconds between strafes")]
        public float strafeCooldown = 3f;
        [Tooltip("Minimum distance to player to allow strafing")]
        public float strafeMinDistance = 1.5f;
        [Tooltip("Maximum distance to player to allow strafing")]
        public float strafeMaxDistance = 6f;
        private float lastStrafeTime = -999f;

        private float attackCooldown = 1.5f;
        public float lastAttackTime = -999f;
        private float waitStartTime = -1f;
        private float waitDuration = 0.7f;

        [Header("Retreat Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float retreatHealthThreshold = 0.25f;
        [SerializeField] private float retreatDuration = 1.5f;
        [SerializeField] private float retreatCooldownSeconds = 4f;
        [SerializeField] private float retreatCheckRadius = 6f;
        [SerializeField] private float retreatSpeed = 1.5f;
        [SerializeField] private float retreatTurnSpeed = 8f;
        private float retreatStartTime = -1f;
        private float lastRetreatTime = -999f;

        [Header("References")]
        private CharacterStateMachine characterStateMachine;
        private EnemyPatrol patrolBehavior;
        private EnemyChase chaseBehavior;
        private EnemyDamaged damagedBehavior;
        private HealthComponent health;
        private Transform player;
        public EnemyAIState currentAIState = EnemyAIState.Idle;
        private EnemyAttackSelector attackSelector;
        private EnemyAttackData currentAttack;
        private int strafeDirection = 1;

        /// <summary>
        /// Caches all required references and components at runtime
        /// </summary>
        private void Start()
        {
            characterStateMachine = GetComponent<CharacterStateMachine>();
            patrolBehavior = GetComponent<EnemyPatrol>();
            chaseBehavior = GetComponent<EnemyChase>();
            damagedBehavior = GetComponent<EnemyDamaged>();
            attackSelector = GetComponent<EnemyAttackSelector>();
            health = GetComponent<HealthComponent>();

            player = PlayerReference.PlayerTransform;

            // Apply active trial curse speed multiplier to newly spawned enemies
            if (TrialCurseModifier.EnemySpeedMultiplier > 1f)
            {
                speed *= TrialCurseModifier.EnemySpeedMultiplier;
            }
        }

        /// <summary>
        /// Main decision making loop for AI behavior
        /// Transitions states based on player distance and internal timers
        /// Integrated with attack selection logic
        /// </summary>
        void Update()
        {
            // Guard against null player reference
            if (player == null)
                return;

            // Use SqrMagnitude instead of Distance to avoid expensive sqrt calculation
            float distanceSqr = (transform.position - player.position).sqrMagnitude;
            float detectionRangeSqr = detectionRange * detectionRange;
            float strafeMinDistanceSqr = strafeMinDistance * strafeMinDistance;
            float strafeMaxDistanceSqr = strafeMaxDistance * strafeMaxDistance;

            if (currentAIState != EnemyAIState.Retreat && currentAIState != EnemyAIState.Damaged && ShouldRetreat())
            {
                ChangeAIState(EnemyAIState.Retreat);
            }

            switch (currentAIState)
            {
                case EnemyAIState.Idle:
                    // If patrol exists, begin patrolling
                    // Otherwise, enter chase state if player is within detection range
                    if (patrolBehavior != null)
                        ChangeAIState(EnemyAIState.Patrol);
                    else if (distanceSqr < detectionRangeSqr)
                        ChangeAIState(EnemyAIState.Chase);
                    return;

                case EnemyAIState.Patrol:
                    characterStateMachine.ChangeState(CharacterState.Moving);
                    // Waiting if waiting at patrol point
                    if (patrolBehavior.waiting)
                        characterStateMachine.ChangeState(CharacterState.Idle);
                    // Transition to Chase if player is nearby
                    if (distanceSqr < detectionRangeSqr)
                        ChangeAIState(EnemyAIState.Chase);
                    return;

                case EnemyAIState.Chase:
                    // Actively pursue the player while in detection range
                    // If an attack is available based on range and cooldown, transition to attack
                    characterStateMachine.ChangeState(CharacterState.Moving);
                    if (attackSelector.HasAttackReady())
                    {
                        ChangeAIState(EnemyAIState.Attack);
                        return;
                    }
                    // Conditional strafe: chance, cooldown, and distance window
                    if (Time.time - lastStrafeTime > strafeCooldown && Random.value < strafeChance && distanceSqr >= strafeMinDistanceSqr && distanceSqr <= strafeMaxDistanceSqr)
                    {
                        lastStrafeTime = Time.time;
                        waitStartTime = Time.time;
                        ChangeAIState(EnemyAIState.Strafe);
                    }
                    return;

                case EnemyAIState.Strafe:
                    characterStateMachine.ChangeState(CharacterState.Strafing);

                    // Picks a strafe direction (sideways or back)
                    Vector3 toPlayer = (player.position - transform.position).normalized;
                    Vector3 strafeDir = Vector3.Cross(Vector3.up, toPlayer) * strafeDirection; // Left/right

                    transform.position += strafeDir * 0.5f * Time.deltaTime;

                    // Face player while strafing
                    transform.rotation = Quaternion.LookRotation(toPlayer);

                    // Exit after short duration
                    if (Time.time - waitStartTime > 1.2f)
                        ChangeAIState(EnemyAIState.Chase);
                    return;

                case EnemyAIState.Retreat:
                    characterStateMachine.ChangeState(CharacterState.Moving);

                    Vector3 retreatDir = (transform.position - player.position).normalized;
                    Vector3 retreatMove = new Vector3(retreatDir.x, 0f, retreatDir.z);
                    transform.position += retreatMove * retreatSpeed * speed * Time.deltaTime;

                    if (retreatMove.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(retreatMove);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, retreatTurnSpeed * Time.deltaTime);
                    }

                    if (Time.time - retreatStartTime > retreatDuration)
                        ChangeAIState(EnemyAIState.Chase);
                    return;

                case EnemyAIState.Attack:
                    // Executes the selected attack if cooldown has elapsed
                    // The AttackSelector chooses the most appropriate attack (range + cooldown)
                    if (Time.time - lastAttackTime > attackCooldown * VeilRunManager.CurrentEnemyAttackRecoveryMultiplier)
                    {
                        lastAttackTime = Time.time;
                        currentAttack = attackSelector.GetSelectedAttack();
                        // Begins the attack and transitions to Wait once it completes (prevents spam attack)
                        StartCoroutine(HandleAttack(() => ChangeAIState(EnemyAIState.Wait)));
                    }
                    return;

                case EnemyAIState.Wait:
                    characterStateMachine.ChangeState(CharacterState.Idle);
                    // Brief delay after an attack before evaluating next action
                    // Returns to Chase if player has moved away
                    // Otherwise, rechecks cooldown availability before attacking again
                    if (distanceSqr > 2.25f) // 1.5f * 1.5f = 2.25f
                        ChangeAIState(EnemyAIState.Chase);
                    else if (Time.time - waitStartTime > waitDuration * VeilRunManager.CurrentEnemyAttackRecoveryMultiplier && attackSelector.HasAttackReady())
                        ChangeAIState(EnemyAIState.Attack);
                    return;

                case EnemyAIState.Damaged:
                    // Enemy is temporarily staggered from taking damage
                    // Once stagger ends, resume chasing the player
                    characterStateMachine.ChangeState(CharacterState.Damaged);
                    if (!damagedBehavior.isStaggered)
                    {
                        // Triggers the damage coroutine, when its done changes state to chase
                        damagedBehavior.TriggerDamage(() =>
                        {
                            ChangeAIState(EnemyAIState.Chase); // return to Chase after damage done
                        });
                    }
                    return;
            }
        }

        /// <summary>
        /// Executes the selected attack by instantiating a prefab with an IEnemyAttack implementation,
        /// or falls back to internal logic if no prefab is assigned
        /// </summary>
        private IEnumerator HandleAttack(System.Action onAttackComplete)
        {
            characterStateMachine.ChangeState(CharacterState.Attacking);

            if (currentAttack != null)
            {
                if (currentAttack.attackPrefab != null)
                {
                    // Prefab-based attack (used by most enemies)
                    GameObject attackInstance = Instantiate(currentAttack.attackPrefab, transform.position, Quaternion.identity, transform);
                    IEnemyAttack attackLogic = attackInstance.GetComponent<IEnemyAttack>();
                    
                    // Play attack-specific animation if assigned, otherwise generic Attack
                    Animator animator = characterStateMachine.GetComponent<Animator>();
                    if (!string.IsNullOrEmpty(currentAttack.attackAnimationStateName))
                    {
                        animator.CrossFade(Animator.StringToHash(currentAttack.attackAnimationStateName), 0.1f, 0);
                    }
                    else
                    {
                        // Fallback to generic Attack state
                        animator.CrossFade(Animator.StringToHash("Attack"), 0.1f, 0);
                    }

                    if (attackLogic != null)
                        yield return StartCoroutine(attackLogic.ExecuteAttack());

                    // Ensure the AI stays in Attacking state at least for the attack's duration (animation sync)
                    if (currentAttack.attackDuration > 0f)
                        yield return new WaitForSeconds(currentAttack.attackDuration);

                    Destroy(attackInstance);
                }
                else
                {
                    // Internal attack (used by Mushroom with EnemyBasicAttack + MushroomShieldAttack)
                    IEnemyAttack internalAttack = GetComponent<IEnemyAttack>();
                    if (internalAttack != null)
                    {
                        yield return StartCoroutine(internalAttack.ExecuteAttack());
                        // small default buffer to ensure animation completes for internal attacks
                        yield return new WaitForSeconds(0.5f);
                    }
                }
            }

            onAttackComplete?.Invoke();
        }

        /// <summary>
        /// Changes the enemy's current AI state
        /// Used to control behavior transitions in Update loop
        /// </summary>
        public void ChangeAIState(EnemyAIState newState)
        {
            if (newState == EnemyAIState.Wait)
                waitStartTime = Time.time;

            if (newState == EnemyAIState.Strafe)
                strafeDirection = Random.value < 0.5f ? -1 : 1;

            if (newState == EnemyAIState.Retreat)
                lastRetreatTime = Time.time;
            if (newState == EnemyAIState.Retreat)
                retreatStartTime = Time.time;

            currentAIState = newState;
        }

        private bool ShouldRetreat()
        {
            if (health == null)
                return false;

            if (Time.time - lastRetreatTime < retreatCooldownSeconds)
                return false;

            if (health.Normalized >= retreatHealthThreshold)
                return false;

            int aliveNearby = CountNearbyAllies();
            return aliveNearby < 1;
        }

        private int CountNearbyAllies()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, retreatCheckRadius);
            HashSet<EnemyAIController> allies = new HashSet<EnemyAIController>();

            for (int i = 0; i < hits.Length; i++)
            {
                EnemyAIController ai = hits[i].GetComponentInParent<EnemyAIController>();
                if (ai != null && ai != this)
                    allies.Add(ai);
            }

            return allies.Count;
        }

    }
}
