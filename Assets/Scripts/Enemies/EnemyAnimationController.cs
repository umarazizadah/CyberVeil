using UnityEngine;

public class EnemyAnimationController : MonoBehaviour
{
    private Animator animator;
    public string currentAnimation = "";
    public string queuedAnimation = "";

    private int animIDIsAttacking;
    

    private bool isAttacking = false;
   
    void Start()
    {
        animator = GetComponent<Animator>();
        animIDIsAttacking = Animator.StringToHash("IsAttacking");
    }

    private void UpdateAnimation(Vector2 move, bool isSprinting, bool attackingState)
    {
        isAttacking = attackingState;
        CheckAnimation(move, isSprinting);

    }

    private void CheckAnimation(Vector2 move, bool isSprinting)
    {
        if (isAttacking)
        {
            return;
        }

        if (move.magnitude > 0.1f)
        {
            if (isSprinting)
            {
                changeAnimation("Sprint");
            }
            else
            {
                changeAnimation("Move");
            }
        }
        else
        {
            changeAnimation("Idle");
        }
    }

    public void changeAnimation(string animation, float crossFade = 0.2f)
    {
        if (isAttacking)
        {
            currentAnimation = animation;
        }

        if (currentAnimation != animation)
        {
            currentAnimation = animation;
            animator.CrossFade(animation, crossFade);
        }
    }

    public void StartAttack()
    {
        isAttacking = true;
        animator.SetBool(animIDIsAttacking, true);
        animator.Play("Attack", 0, 0f);
    }

    public void EndAttack()
    {
        isAttacking = false;
        animator.SetBool(animIDIsAttacking,false);
    }

}
