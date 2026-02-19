using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Hand : MonoBehaviour
{
    Animator animator;

    private float gripTarget;
    private float triggerTarget;
    private float gripCurrent;
    private float triggerCurrent;

    public float speed = 5f;

    private readonly string animatorGripParam = "Grip";
    private readonly string animatorTriggerParam = "Trigger";

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        AnimateHand();
    }

    public void SetGrip(float value)
    {
        gripTarget = value;
    }

    public void SetTrigger(float value)
    {
        triggerTarget = value;
    }

    void AnimateHand()
    {
        gripCurrent = Mathf.MoveTowards(gripCurrent, gripTarget, Time.deltaTime * speed);
        triggerCurrent = Mathf.MoveTowards(triggerCurrent, triggerTarget, Time.deltaTime * speed);

        animator.SetFloat(animatorGripParam, gripCurrent);
        animator.SetFloat(animatorTriggerParam, triggerCurrent);
    }
}
