using UnityEngine;

[AddComponentMenu("Visual/Simple Animation Player (단순애니메이션재생)")]
[DisallowMultipleComponent]
public class SimpleAnimationPlayer : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("대상 설정")]
    [SerializeField] private SpriteRenderer targetRenderer;   // ✅ 애니메이션 이미지를 표시할 SpriteRenderer
    [SerializeField] private FlipbookAnimation flipbook;      // ✅ 기본으로 재생할 FlipbookAnimation 에셋

    [Header("재생 옵션")]
    [SerializeField, Min(0.001f)]
    private float defaultInterval = 0.08f;                    // ✅ 프레임 변경 기본 주기(초)
    [SerializeField] private bool playOnce = false;           // ✅ true면 한 번만 재생, false면 반복 재생
    [SerializeField] private bool playOnSignalOnly = false;   // ✅ true면 신호(메서드 호출)로만 재생, false면 OnEnable에서 자동 재생

    // 내부 상태
    private Coroutine playRoutine;                            // ✅ 재생 코루틴 핸들 (중복 방지용)
    private int currentFrameIndex = 0;                        // ✅ 현재 재생 중인 프레임 인덱스

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Reset()                                      // ✅ 컴포넌트 추가 시 기본 참조 설정
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();  // 같은 오브젝트에 있는 SpriteRenderer 자동 참조
    }

    private void OnEnable()                                   // ✅ 오브젝트가 활성화될 때 호출
    {
        // 기본 안전 체크
        if (flipbook == null)
            return;

        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (targetRenderer == null)
            return;

        // 프레임/코루틴 초기화
        currentFrameIndex = 0;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        // 🔸 playOnSignalOnly가 켜져 있으면 자동 재생하지 않고 대기
        if (playOnSignalOnly)
            return;

        // 자동 재생 모드일 때만 코루틴 시작
        playRoutine = StartCoroutine(Co_PlayFromStart());
    }

    private void OnDisable()                                  // ✅ 오브젝트가 비활성화될 때 호출
    {
        // 비활성화 시 코루틴 중단
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 외부에서 호출할 메서드 (신호 기반 재생)
    // ─────────────────────────────────────────────────────────────────────────────

    public void PlayOnce()                                   // ✅ 현재 flipbook을 1회 재생하는 신호
    {
        if (flipbook == null)
            return;

        InternalPlay(flipbook, true);
    }

    public void PlayOnce(FlipbookAnimation anim)             // ✅ 전달받은 flipbook을 1회 재생하는 신호
    {
        if (anim == null)
            return;

        InternalPlay(anim, true);
    }

    public void PlayLoop(FlipbookAnimation anim)             // ✅ 전달받은 flipbook을 반복 재생하는 신호
    {
        if (anim == null)
            return;

        InternalPlay(anim, false);
    }

    private void InternalPlay(FlipbookAnimation anim, bool playOnceFlag) // ✅ 공통 재생 처리
    {
        flipbook = anim;                     // 재생할 flipbook 교체
        playOnce = playOnceFlag;             // 1회/반복 모드 설정

        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null || flipbook == null)
            return;

        // 코루틴 초기화 후 새로 시작
        currentFrameIndex = 0;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        playRoutine = StartCoroutine(Co_PlayFromStart());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 코루틴 (실제 프레임 재생)
    // ─────────────────────────────────────────────────────────────────────────────

    private System.Collections.IEnumerator Co_PlayFromStart() // ✅ 처음부터 애니메이션을 재생하는 코루틴
    {
        int frameCount = flipbook.FrameCount();
        if (frameCount <= 0)
            yield break;

        // 첫 프레임 강제 적용
        ApplyFrame(currentFrameIndex);

        if (playOnce)
        {
            // ─────────────────────────────────────────────
            // 1회 재생 모드
            // ─────────────────────────────────────────────
            while (currentFrameIndex < frameCount)
            {
                if (!flipbook.TryGetFrame(currentFrameIndex, out var frame))
                    yield break;

                float interval = (frame.overrideInterval && frame.customInterval > 0f)
                    ? frame.customInterval
                    : defaultInterval;

                yield return new WaitForSeconds(interval);

                currentFrameIndex++;

                if (currentFrameIndex >= frameCount)
                    break; // 마지막 프레임까지 재생 후 종료

                ApplyFrame(currentFrameIndex);
            }
        }
        else
        {
            // ─────────────────────────────────────────────
            // 반복 재생 모드
            // ─────────────────────────────────────────────
            while (true)
            {
                if (!flipbook.TryGetFrame(currentFrameIndex, out var frame))
                    yield break;

                float interval = (frame.overrideInterval && frame.customInterval > 0f)
                    ? frame.customInterval
                    : defaultInterval;

                yield return new WaitForSeconds(interval);

                currentFrameIndex = (currentFrameIndex + 1) % frameCount;

                ApplyFrame(currentFrameIndex);
            }
        }
    }

    private void ApplyFrame(int index)                         // ✅ 지정 인덱스 프레임을 SpriteRenderer에 적용
    {
        if (flipbook == null || targetRenderer == null)
            return;

        if (!flipbook.TryGetFrame(index, out var frame))
            return;

        if (frame.sprite != null)
            targetRenderer.sprite = frame.sprite;
    }
}
