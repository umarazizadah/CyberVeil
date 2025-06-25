using UnityEngine;
using CyberVeil.Core;
using System.Collections;

namespace CyberVeil.Enemies
{
    /// <summary>
    /// AI state controller for enemy behavior
    /// Central brain that monitors player distance and updates EnemyAIState
    /// Delegates movement and animation control through CharacterStateMachine
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        [Header("Movement")]
        public float speed = 1f;

        [Header("Ranges")]
        public float attackRange = 2f;
        public float detectionRange = 4f;

        private float attackCooldown = 1.5f;
        public float lastAttackTime = -999f;

        [Header("Refereneces")]
        private CharacterStateMachine characterStateMachine;
        private EnemyPatrol patrolBehavior;
        private EnemyChase chaseBehavior;
        private Transform player;
        public EnemyAIState currentAIState = EnemyAIState.Idle;

        private void Start()
        {
            characterStateMachine = GetComponent<CharacterStateMachine>();
            patrolBehavior = GetComponent<EnemyPatrol>();
            chaseBehavior = GetComponent<EnemyChase>();
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        void Update()
        {
            float distance = Vector3.Distance(transform.position, player.position);

            switch(currentAIState)
            {
                case EnemyAIState.Idle:
                    // Start patrolling if patrol exists
                    if (patrolBehavior != null)
                        ChangeAIState(EnemyAIState.Patrol);
                    else if (distance < detectionRange)
                        ChangeAIState(EnemyAIState.Chase);
                    break;

                case EnemyAIState.Patrol:
                    characterStateMachine.ChangeState(CharacterState.Moving);
                    // Idle if waiting at patrol point
                    if (patrolBehavior.waiting)
                        characterStateMachine.ChangeState(CharacterState.Idle);
                    // Transition to Chase if player is nearby
                    if (distance < detectionRange)
                        ChangeAIState(EnemyAIState.Chase);
                    break;

                case EnemyAIState.Chase:
                    characterStateMachine.ChangeState(CharacterState.Moving);
                    // Enter Attack if within striking distance
                    if (distance <= attackRange)
                        ChangeAIState(EnemyAIState.Attack);
                    break;

                case EnemyAIState.Attack:
                    // Limit attacks using cooldown timer
                    if (Time.time - lastAttackTime > attackCooldown)
                    {
                        lastAttackTime = Time.time;
                        StartCoroutine(HandleAttack());
                    }
                    break;

                case EnemyAIState.Wait:
                    characterStateMachine.ChangeState(CharacterState.Idle);
                    // Resume chase if player has moved out of attack range
                    if (distance > attackRange + 1.5f)
                        ChangeAIState(EnemyAIState.Chase);
                    break;
            }
        }

        /// <summary>
        /// Coroutine to handle enemy attack logic
        /// Triggers attack animation, then transitions to Wait state
        /// </summary>
        private IEnumerator HandleAttack()
        {
            characterStateMachine.ChangeState(CharacterState.Attacking);

            // Wait for attack animation 
            yield return new WaitForSeconds(1f); 

            ChangeAIState(EnemyAIState.Wait);
        }

        /// <summary>
        /// Changes the enemy's current AI state
        /// Used to control behavior transitions in Update loop
        /// </summary>
        public void ChangeAIState(EnemyAIState newState)
        {
            currentAIState = newState;
        }
    }
}