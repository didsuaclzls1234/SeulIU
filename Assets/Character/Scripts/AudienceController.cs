using System.Collections;
using UnityEngine;

public class AudienceController : MonoBehaviour
{
    private Animator animator;
    private bool isFirstStart = true;

    void Start()
    {
        animator = GetComponent<Animator>();
        StartCoroutine(AudienceCycle());
    }

    IEnumerator AudienceCycle()
    {
        while (true)
        {
            bool useIdle2 = Random.value > 0.5f;
            if (useIdle2)
            {
                yield return StartCoroutine(PlayIdle2Sequence(isFirstStart));
            }
            else
            {
                isFirstStart = false;
                animator.SetTrigger("GoIdle");
                yield return new WaitUntil(() =>
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
                animator.ResetTrigger("PlayCheering");
                animator.ResetTrigger("GoIdle2Front");
                animator.ResetTrigger("GoIdle2");
                yield return new WaitForSeconds(Random.Range(20f, 50f));
            }

            isFirstStart = false;

            animator.ResetTrigger("PlayCheering");
            animator.ResetTrigger("GoIdle");
            animator.ResetTrigger("GoIdle2Front");
            animator.ResetTrigger("GoIdle2");
            animator.ResetTrigger("GoIdle2Back");

            yield return StartCoroutine(PlayCheeringTimed(Random.Range(15f, 30f)));

            yield return null;
            yield return null;

            animator.ResetTrigger("PlayCheering");
            animator.ResetTrigger("GoIdle");
            animator.ResetTrigger("GoIdle2Front");
            animator.ResetTrigger("GoIdle2");
            animator.ResetTrigger("GoIdle2Back");

            bool nextIdle2 = Random.value > 0.5f;
            if (nextIdle2)
                animator.SetTrigger("GoIdle2Front");
            else
                animator.SetTrigger("GoIdle");
        }
    }

    IEnumerator PlayCheeringTimed(float duration)
    {
        float endTime = Time.time + duration;

        while (true)
        {
            animator.ResetTrigger("PlayCheering");
            animator.SetTrigger("PlayCheering");

            yield return new WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).IsName("Cheering") &&
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.1f);

            yield return new WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.9f);

            yield return null;

            if (Time.time >= endTime)
                break;
        }
    }

    IEnumerator PlayIdle2Sequence(bool skipFront)
    {
        if (!skipFront)
        {
            animator.SetTrigger("GoIdle2Front");
            yield return new WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).IsName("Idle2_Front"));
            yield return new WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.9f);
        }

        animator.SetTrigger("GoIdle2");
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("Idle2"));
        yield return new WaitForSeconds(Random.Range(20f, 50f));

        animator.SetTrigger("GoIdle2Back");
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("Idle2_Back"));
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.9f);
    }
}