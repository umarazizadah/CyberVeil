using UnityEngine;
using CyberVeil.Core;

namespace CyberVeil.Enemies
{
    public class EnemyAI : MonoBehaviour
    {
        [Header("Movement")]
        public float speed = 1f;
        public bool follow = true;

        [Header("Ranges")]
        public float attackRange = 2f;
        public float detectionRange = 10f;

        private float attackCooldown = 1.5f;
        public float lastAttackTime = -999f;

        [Header("Refereneces")]
        private CharacterStateMachine characterStateMachine;
        public EnemyAIState currentAIState = EnemyAIState.Idle;
        [Header("Targeting")]
        public Transform player;

        private void Start()
        {
            characterStateMachine = GetComponent<CharacterStateMachine>();
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }

        void Update()
        {
            float distance = Vector3.Distance(transform.position, player.position);

            switch(currentAIState)
            {
                case EnemyAIState.Idle:
                    // Idle or patrol decision
                    if (distance < detectionRange)
                    {
                        ChangeAIState(EnemyAIState.Wait); 
                    }
                    break;
                case EnemyAIState.Wait:
                    characterStateMachine.ChangeState(CharacterState.Idle); 
                    // Wait until group coordinate lets this enemy attack
                    break;
                case EnemyAIState.Attack:
                    if (Time.time - lastAttackTime > attackCooldown)
                    {
                        lastAttackTime = Time.time;
                        characterStateMachine.ChangeState(CharacterState.Attacking);
                        // Apply damage here
                        ChangeAIState(EnemyAIState.Wait);
                    }
                    break;
            }
        }

        public void ChangeAIState(EnemyAIState newState)
        {
            currentAIState = newState;
        }
    }
}