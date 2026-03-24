using UnityEngine;
using System.Collections;

[AddComponentMenu("Combat/Parry System (패링시스템)")]
[DisallowMultipleComponent]
public class ParrySystem : MonoBehaviour
{
    [Header("기본 설정")]
    [SerializeField] private KeyCode parryKey = KeyCode.C;          // ✅ 패링 입력 키
    [SerializeField] private float activeDuration = 0.3f;           // ✅ 패링 유효시간
    [SerializeField] private Collider2D parryCollider;              // ✅ 패링용 히트박스(Trigger Collider2D)

    [Header("쿨타임 설정")]
    [SerializeField] private float cooldownDuration = 0.7f;         // ✅ 패링 종료 후 다시 시도 가능까지 시간
    [SerializeField] private bool isOnCooldown = false;             // ✅ 쿨타임 진행 중인지 여부
    [SerializeField] private float cooldownTimer = 0f;              // ✅ 쿨타임 타이머

    [Header("상태 확인용")]
    [SerializeField] private bool isActive = false;                 // ✅ 현재 패링 활성 상태인지
    [SerializeField] private bool hasParrySuccessThisWindow = false; // ✅ 이번 패링 시도 동안 이미 성공했는지 여부

    [Header("사운드 설정")]
    [SerializeField] private AudioClip parryTryClip;                // ✅ 패링 시도 시 재생할 효과음

    [Header("행동 가능 여부")]
    [SerializeField] private bool canAct = true;                    // ✅ 패링 동작이 가능한지 여부(기력 고갈 등)

    [Header("기력 시스템")]
    [SerializeField] private StaminaSystem staminaSystem;           // ✅ 기력 시스템(StaminaSystem) 참조

    [Header("패링 이펙트 애니메이션")]
    [SerializeField] private SimpleAnimationPlayer parryEffectPlayer; // ✅ 패링 시 1회 재생할 Flipbook 애니메이션 플레이어

    private Coroutine parryCoroutine;                               // ✅ 중복 방지용 코루틴 핸들

    // ─────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────

    private void Reset()                                            // ✅ Trigger 콜라이더/기력시스템/이펙트 자동 탐색
    {
        Collider2D col = GetComponent<Collider2D>();               // ✅ 자기 콜라이더 가져오기
        if (col != null && col.isTrigger)
            parryCollider = col;                                   // ✅ 트리거 콜라이더면 패링 콜라이더로 사용

        if (staminaSystem == null)
            staminaSystem = GetComponentInParent<StaminaSystem>(); // ✅ 부모에서 StaminaSystem 자동 검색

        if (parryEffectPlayer == null)
            parryEffectPlayer = GetComponentInChildren<SimpleAnimationPlayer>(); // ✅ 자식에서 패링 이펙트 검색
    }

    private void Start()                                           // ✅ 시작 시 항상 패링 히트박스 비활성
    {
        if (parryCollider != null)
            parryCollider.enabled = false;                         // ✅ 처음엔 패링 히트박스 꺼두기
    }

    private void OnEnable()                                        // ✅ 활성화 시 상태 플래그 초기화
    {
        hasParrySuccessThisWindow = false;                         // ✅ 새로 활성화될 때는 항상 1회 성공 플래그 리셋
    }

    private void Update()                                          // ✅ 입력 및 쿨타임 처리
    {
        float dt = Time.deltaTime;                                 // ✅ 프레임 경과 시간

        // 쿨타임 진행
        if (isOnCooldown)
        {
            cooldownTimer += dt;
            if (cooldownTimer >= cooldownDuration)
            {
                isOnCooldown = false;
            }
        }

        // 행동 불가 상태면 입력 자체를 받지 않음
        if (!canAct)
            return;

        // 패링 키 입력
        if (Input.GetKeyDown(parryKey))
        {
            TryActivateParry();                                    // ✅ 패링 시도
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 패링 시도 / 종료
    // ─────────────────────────────────────────────────────────────

    private void TryActivateParry()                                // ✅ 패링 시도(쿨타임/상태/기력 체크)
    {
        if (isActive || parryCollider == null)
            return;

        // 쿨타임 중이면 무시
        if (isOnCooldown)
            return;

        // 행동 불가 상태면 무시
        if (!canAct)
            return;

        // ✅ 기력 체크: 패링에 필요한 기력 없으면 시도 불가
        if (staminaSystem != null && !staminaSystem.TryConsumeForParry())
        {
            return;
        }

        // 새 패링 시도 시작 → 성공 플래그 리셋
        hasParrySuccessThisWindow = false;                         // ✅ 이번 패링 윈도우에서 아직 성공 없음 표시

        // 패링 윈도우 시작
        isActive = true;
        parryCollider.enabled = true;

        // 패링 시도 효과음 재생
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlaySfx(parryTryClip);             // ✅ 패링 시도 사운드
        }

        // 패링 이펙트 애니메이션 1회 재생 트리거
        PlayParryEffectOnce();                                     // ✅ FlipbookAnimation 1회 재생

        // 일정 시간 뒤 자동 비활성화
        if (parryCoroutine != null)
            StopCoroutine(parryCoroutine);

        parryCoroutine = StartCoroutine(DeactivateAfterDelay());  // ✅ 패링 종료 코루틴 시작
    }

    private IEnumerator DeactivateAfterDelay()                     // ✅ 패링 종료 + 쿨타임 시작
    {
        yield return new WaitForSeconds(activeDuration);          // ✅ 유효시간만큼 대기

        isActive = false;
        hasParrySuccessThisWindow = false;                        // ✅ 윈도우 종료 시 성공 플래그도 초기화

        if (parryCollider != null)
            parryCollider.enabled = false;                        // ✅ 히트박스 비활성화

        // 패링이 끝난 시점부터 쿨타임 시작
        isOnCooldown = true;
        cooldownTimer = 0f;
    }

    private void PlayParryEffectOnce()                            // ✅ 패링 시 FlipbookAnimation 1회 재생(직접 호출 방식)
    {
        if (parryEffectPlayer == null)
            return;

        GameObject go = parryEffectPlayer.gameObject;

        // 🔹 이펙트 오브젝트가 꺼져 있으면 먼저 활성화
        if (!go.activeSelf)
        {
            go.SetActive(true);
        }

        // 🔹 SimpleAnimationPlayer에 설정된 flipbook을 1회 재생
        parryEffectPlayer.PlayOnce();
    }

    // ─────────────────────────────────────────────────────────────
    // 외부에서 사용하는 메서드
    // ─────────────────────────────────────────────────────────────

    public bool IsParryActive()                                    // ✅ 외부에서 활성 상태 조회
    {
        return isActive;
    }

    public bool TryRegisterParrySuccess()                          // ✅ 이번 패링 시도에서 '첫 성공'만 허용하는 메서드
    {
        // 패링이 활성 상태가 아니거나 이미 한 번 성공했다면 실패
        if (!isActive || hasParrySuccessThisWindow)
            return false;

        hasParrySuccessThisWindow = true;                          // ✅ 이번 윈도우에서 성공 처리
        return true;
    }

    public void SetCanAct(bool value)                              // ✅ 외부(기력시스템)에서 패링 가능 여부 설정
    {
        canAct = value;

        // 행동 불가가 되는 순간, 패링이 진행 중이면 강제로 종료
        if (!canAct && isActive)
        {
            isActive = false;
            hasParrySuccessThisWindow = false;                    // ✅ 강제 종료 시에도 성공 플래그 리셋

            if (parryCollider != null)
                parryCollider.enabled = false;                    // ✅ 히트박스 끄기

            if (parryCoroutine != null)
            {
                StopCoroutine(parryCoroutine);                    // ✅ 코루틴 정지
                parryCoroutine = null;
            }

            // 강제 종료 시점부터 쿨타임 시작(필요 없으면 이 부분 제거 가능)
            isOnCooldown = true;
            cooldownTimer = 0f;
        }
    }

    public StaminaSystem GetStaminaSystem()                        // ✅ Enemy 쪽에서 사용할 StaminaSystem Getter
    {
        return staminaSystem;
    }
}
