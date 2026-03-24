using UnityEngine;
using UnityEngine.UI; // ✅ Slider, Image 사용을 위한 네임스페이스

[AddComponentMenu("Stats/Stamina System (기력시스템)")]
[DisallowMultipleComponent]
public class StaminaSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────

    [Header("기력 기본값")]
    [SerializeField] private float maxStamina = 100f;               // ✅ 최대 기력
    [SerializeField] private float currentStamina = 100f;           // ✅ 현재 기력

    [Header("기력 회복 설정")]
    [SerializeField] private float regenPerSecond = 5f;             // ✅ 초당 기력 회복량

    [Header("행동불가 임계값 설정")]
    [SerializeField] private float exhaustedThreshold = 0f;         // ✅ 현재 기력이 이 값 이하가 되면 행동불가 상태 진입

    [Header("행동별 기력 소모량")]
    [SerializeField] private float normalJumpCost = 5f;             // ✅ 일반 점프 1회 소모량
    [SerializeField] private float chargedJumpCost = 10f;           // ✅ 특수(차지) 점프 1회 소모량
    [SerializeField] private float parryCost = 8f;                  // ✅ 패링 1회 소모량
    [SerializeField] private float shotCost = 4f;                   // ✅ 사격 1회 소모량
    [SerializeField] private float flurryCostPerSecond = 6f;        // ✅ 난타 공격 중 초당 소모량

    [Header("패링 성공 회복 설정")]
    [SerializeField] private float parrySuccessRestoreAmount = 5f;  // ✅ 패링으로 적을 패링 성공 시 회복할 기력량

    [Header("기력 고갈(Exhaust) 설정")]
    [SerializeField] private float exhaustedLockDuration = 1.5f;    // ✅ 행동불가 상태 유지 시간(초)
    [SerializeField] private bool isExhausted = false;              // ✅ 현재 행동불가 상태인지
    [SerializeField] private float exhaustedTimer = 0f;             // ✅ 행동불가 남은 시간

    [Header("연결된 행동 스크립트")]
    [SerializeField] private PlayerBasicMovement playerMovement;    // ✅ 플레이어 이동/점프 스크립트
    [SerializeField] private FlurryAttackSystem flurrySystem;       // ✅ 난타 공격 스크립트
    [SerializeField] private ParrySystem parrySystem;               // ✅ 패링 스크립트
    [SerializeField] private ShootingSystem shootingSystem;         // ✅ 사격 스크립트

    [Header("UI - 기본 기력 표시 슬라이더")]
    [SerializeField] private Slider staminaSlider;                  // ✅ 기력 표시용 메인 슬라이더

    [Header("UI - 감소량 표시용 슬라이더(잔상)")]
    [SerializeField] private Slider staminaDamageSlider;            // ✅ 감소량 연출용 슬라이더
    [SerializeField] private float damageFollowSpeed = 30f;         // ✅ 잔상 슬라이더가 내려갈 속도
    [SerializeField] private float instantDropThreshold = 3f;       // ✅ 순간 감소로 인식할 최소 감소량

    [Header("UI - 행동불가 상태 표시")]
    [SerializeField] private Image exhaustedStateImage;             // ✅ 행동불가 시 색이 바뀔 이미지
    [SerializeField] private Color exhaustedColor = Color.gray;     // ✅ 행동불가 상태에서 사용할 색상
    [SerializeField] private Slider exhaustedTimerSlider;           // ✅ 행동불가 남은 시간을 표시할 슬라이더(1→0)

    // 내부 상태
    private bool consumedThisFrame = false;                         // ✅ 이번 프레임에 실제 기력 소모가 있었는지 여부
    private float previousStaminaForDamageBar = 0f;                 // ✅ 이전 프레임의 기력값(잔상용)
    private Color originalExhaustedImageColor;                      // ✅ 행동불가 이미지의 원래 색
    private bool hasOriginalExhaustedImageColor = false;            // ✅ 원래 색을 한 번이라도 캐싱했는지 여부

    // 읽기 전용 프로퍼티
    public float CurrentStamina => currentStamina;                  // ✅ 현재 기력 확인용
    public float MaxStamina => maxStamina;                          // ✅ 최대 기력 확인용
    public bool IsExhausted => isExhausted;                         // ✅ 행동불가 상태 여부 확인용

    // ─────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────

    private void Reset()                                            // ✅ 기본 레퍼런스 자동 할당
    {
        if (playerMovement == null)
            playerMovement = GetComponentInChildren<PlayerBasicMovement>();   // ✅ 자식에서 이동 스크립트 찾기

        if (flurrySystem == null)
            flurrySystem = GetComponentInChildren<FlurryAttackSystem>();      // ✅ 자식에서 난타 스크립트 찾기

        if (parrySystem == null)
            parrySystem = GetComponentInChildren<ParrySystem>();             // ✅ 자식에서 패링 스크립트 찾기

        if (shootingSystem == null)
            shootingSystem = GetComponentInChildren<ShootingSystem>();       // ✅ 자식에서 사격 스크립트 찾기

        if (staminaSlider == null)
            staminaSlider = GetComponentInChildren<Slider>();                // ✅ 자식에서 메인 Slider 자동 검색

        // ✅ 감소량 슬라이더를 별도 할당하지 않았다면, 자식 슬라이더 중 메인이 아닌 것 하나를 자동 할당
        if (staminaDamageSlider == null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>();
            foreach (var s in sliders)
            {
                if (s != staminaSlider)
                {
                    staminaDamageSlider = s;
                    break;
                }
            }
        }

        // exhaustedStateImage, exhaustedTimerSlider 는 인스펙터에서 수동 할당을 전제로 함
    }

    private void OnEnable()                                         // ✅ 활성화 시 기력/상태 정리
    {
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        // ✅ 시작 시점에 현재 기력이 임계값 이하라면 곧바로 행동불가 상태 진입
        if (currentStamina <= exhaustedThreshold)
        {
            EnterExhaustedState();
        }
        else
        {
            ExitExhaustedStateImmediate();
        }

        // ✅ 데미지바용 이전 기력값 초기화
        previousStaminaForDamageBar = currentStamina;

        // ✅ 행동불가 이미지 원래 색 캐싱(한 번만)
        if (exhaustedStateImage != null && !hasOriginalExhaustedImageColor)
        {
            originalExhaustedImageColor = exhaustedStateImage.color;
            hasOriginalExhaustedImageColor = true;
        }

        // ✅ 시작 시 슬라이더들 초기값 반영
        UpdateSlider();
        UpdateDamageSliderImmediate();
        UpdateExhaustedTimerSliderImmediate();
    }

    private void Update()                                           // ✅ 기력 회복 + 행동불가 시간 + 슬라이더 갱신
    {
        float dt = Time.deltaTime;                                  // ✅ 프레임 경과 시간

        // 1) 행동불가 상태 타이머 처리 + 타이머 슬라이더 갱신
        if (isExhausted)
        {
            exhaustedTimer -= dt;
            if (exhaustedTimer < 0f)
                exhaustedTimer = 0f;

            // ✅ 행동불가 남은 시간을 1→0으로 표시
            UpdateExhaustedTimerSlider();

            if (exhaustedTimer <= 0f)
            {
                ExitExhaustedState();                               // ✅ 일정 시간 지나면 행동 가능 복구
            }
        }
        else
        {
            // ✅ 행동불가가 아닐 때는 타이머 슬라이더를 0으로 맞춰둠
            if (exhaustedTimerSlider != null)
            {
                exhaustedTimerSlider.value = 0f;
            }
        }

        // 2) 기력 회복 처리 (이번 프레임에 실제 소모가 없을 때만 회복)
        if (!consumedThisFrame && regenPerSecond > 0f && currentStamina < maxStamina)
        {
            currentStamina += regenPerSecond * dt;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        // 3) 메인 슬라이더 UI 갱신
        UpdateSlider();

        // 4) 감소량 표시 슬라이더 갱신(순간 감소 연출)
        UpdateDamageSlider(dt);

        // 5) 다음 프레임 비교를 위해 이전 기력값 저장
        previousStaminaForDamageBar = currentStamina;

        // 6) 프레임 끝에서 소모 플래그 리셋
        consumedThisFrame = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 외부에서 호출할 기력 소모 / 회복 메서드
    // ─────────────────────────────────────────────────────────────────────

    public bool TryConsumeForNormalJump()                           // ✅ 일반 점프용 기력 소모
    {
        return TryConsume(normalJumpCost);
    }

    public bool TryConsumeForChargedJump()                          // ✅ 특수(차지) 점프용 기력 소모
    {
        return TryConsume(chargedJumpCost);
    }

    public bool TryConsumeForParry()                                // ✅ 패링용 기력 소모
    {
        return TryConsume(parryCost);
    }

    public bool TryConsumeForShot()                                 // ✅ 사격용 기력 소모
    {
        return TryConsume(shotCost);
    }

    public bool TryConsumeForFlurry(float deltaTime)                // ✅ 난타 공격(초당 소모)용 기력 소모
    {
        if (deltaTime <= 0f)
            return true;

        float amount = flurryCostPerSecond * deltaTime;
        return TryConsume(amount);
    }

    public void RestoreStaminaOnParrySuccess()                      // ✅ 패링 성공 시 Stamina 회복용 메서드
    {
        if (parrySuccessRestoreAmount <= 0f)
            return;

        currentStamina += parrySuccessRestoreAmount;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        // ✅ 회복 즉시 UI 갱신(메인 슬라이더 + 잔상 슬라이더 싱크)
        UpdateSlider();
        UpdateDamageSliderImmediate();
    }

    // ─────────────────────────────────────────────────────────────────────
    // 내부 공통 소모/상태 처리 메서드
    // ─────────────────────────────────────────────────────────────────────

    private bool TryConsume(float amount)                           // ✅ 공통 기력 소모 처리
    {
        if (amount <= 0f)
            return true;

        // ✅ 이미 행동불가 상태면 소모 자체를 허용하지 않음
        if (isExhausted)
            return false;

        // ✅ 기력이 부족하면 소모 실패 (임계값 체크는 아래에서 처리)
        if (currentStamina < amount)
        {
            return false;
        }

        // ✅ 정상 소모
        currentStamina -= amount;
        consumedThisFrame = true;                                   // ✅ 이 프레임에 소모 발생 표시

        // ✅ 소모 후 현재 기력이 "임계값 이하"로 떨어졌다면 행동불가 상태 진입
        if (currentStamina <= exhaustedThreshold)
        {
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
            if (!isExhausted)
                EnterExhaustedState();
        }

        return true;
    }

    private void EnterExhaustedState()                              // ✅ 행동불가 상태 진입(타이머 시작 + 행동 차단)
    {
        isExhausted = true;
        exhaustedTimer = exhaustedLockDuration;
        SetAllActable(false);                                       // ✅ 연결된 스크립트 전부 행동 불가

        // ✅ 행동불가 상태 색상 적용
        if (exhaustedStateImage != null)
        {
            if (!hasOriginalExhaustedImageColor)
            {
                originalExhaustedImageColor = exhaustedStateImage.color;
                hasOriginalExhaustedImageColor = true;
            }
            exhaustedStateImage.color = exhaustedColor;
        }

        // ✅ 행동불가 시간 슬라이더 초기화(항상 1에서 시작)
        if (exhaustedTimerSlider != null)
        {
            exhaustedTimerSlider.maxValue = 1f;
            exhaustedTimerSlider.value = 1f;
        }
    }

    private void ExitExhaustedState()                               // ✅ 행동불가 시간 종료 후 행동 가능 복구
    {
        isExhausted = false;
        SetAllActable(true);

        // ✅ 원래 이미지 색상 복원
        if (exhaustedStateImage != null && hasOriginalExhaustedImageColor)
        {
            exhaustedStateImage.color = originalExhaustedImageColor;
        }

        // ✅ 행동불가 시간 슬라이더 0으로 초기화
        if (exhaustedTimerSlider != null)
        {
            exhaustedTimerSlider.value = 0f;
        }
    }

    private void ExitExhaustedStateImmediate()                      // ✅ 즉시 행동 가능 복구(초기화 등)
    {
        isExhausted = false;
        exhaustedTimer = 0f;
        SetAllActable(true);

        // ✅ 원래 이미지 색상 복원
        if (exhaustedStateImage != null && hasOriginalExhaustedImageColor)
        {
            exhaustedStateImage.color = originalExhaustedImageColor;
        }

        // ✅ 행동불가 시간 슬라이더 0으로 초기화
        if (exhaustedTimerSlider != null)
        {
            exhaustedTimerSlider.value = 0f;
        }
    }

    private void SetAllActable(bool canAct)                         // ✅ 연결된 행동 스크립트들의 행동 가능 여부 일괄 설정
    {
        if (playerMovement != null)
        {
            playerMovement.SetCanAct(canAct);                       // ✅ 이동/점프 제한
        }

        if (flurrySystem != null)
        {
            flurrySystem.SetCanAct(canAct);                         // ✅ 난타 공격 제한
        }

        if (parrySystem != null)
        {
            parrySystem.SetCanAct(canAct);                          // ✅ 패링 제한
        }

        if (shootingSystem != null)
        {
            shootingSystem.SetCanAct(canAct);                       // ✅ 사격 제한
        }
    }

    private void UpdateSlider()                                     // ✅ 메인 슬라이더에 현재 기력 반영
    {
        if (staminaSlider == null)
            return;

        staminaSlider.maxValue = maxStamina;
        staminaSlider.value = currentStamina;
    }

    private void UpdateDamageSliderImmediate()                      // ✅ 감소량 슬라이더를 즉시 현재 기력에 맞춤
    {
        if (staminaDamageSlider == null)
            return;

        staminaDamageSlider.maxValue = maxStamina;
        staminaDamageSlider.value = currentStamina;
    }

    private void UpdateDamageSlider(float deltaTime)                // ✅ 감소량 슬라이더(잔상) 갱신 로직
    {
        if (staminaDamageSlider == null)
            return;

        staminaDamageSlider.maxValue = maxStamina;

        float mainValue = currentStamina;                           // ✅ 메인 기력 값
        float damageValue = staminaDamageSlider.value;              // ✅ 현재 잔상 슬라이더 값
        float prev = previousStaminaForDamageBar;                   // ✅ 이전 프레임 기력값

        // 1) 한 프레임 기준 기력 감소량 계산
        float drop = prev - mainValue;

        // 2) "순간 감소"라고 판단되는 경우: 한 번에 일정량 이상 떨어졌을 때
        if (drop > 0f && drop >= instantDropThreshold)
        {
            // ✅ 감소량 슬라이더를 이전 값 이상으로 올려놓고, 그 위에서부터 내려오게 함
            if (damageValue < prev)
            {
                damageValue = prev;
            }
        }

        // 3) 감소량 슬라이더가 메인보다 위에 있을 때만 부드럽게 따라 내려오게 함
        if (damageValue > mainValue)
        {
            damageValue = Mathf.MoveTowards(
                damageValue,
                mainValue,
                damageFollowSpeed * deltaTime);                     // ✅ 설정한 속도로 아래로 따라가기
        }
        else
        {
            // ✅ 메인 값보다 아래로 내려가면 바로 메인 값에 맞춰줌(회복 시 튀지 않게)
            damageValue = mainValue;
        }

        staminaDamageSlider.value = damageValue;
    }

    private void UpdateExhaustedTimerSliderImmediate()              // ✅ 행동불가 타이머 슬라이더 즉시 초기화
    {
        if (exhaustedTimerSlider == null)
            return;

        exhaustedTimerSlider.maxValue = 1f;

        if (isExhausted && exhaustedLockDuration > 0f)
        {
            exhaustedTimerSlider.value = Mathf.Clamp01(exhaustedTimer / exhaustedLockDuration);
        }
        else
        {
            exhaustedTimerSlider.value = 0f;
        }
    }

    private void UpdateExhaustedTimerSlider()                       // ✅ 행동불가 타이머 슬라이더 갱신(1→0)
    {
        if (exhaustedTimerSlider == null)
            return;

        exhaustedTimerSlider.maxValue = 1f;

        if (isExhausted && exhaustedLockDuration > 0f)
        {
            float normalized = Mathf.Clamp01(exhaustedTimer / exhaustedLockDuration);
            exhaustedTimerSlider.value = normalized;                // ✅ 남은 비율(1~0) 표시
        }
        else
        {
            exhaustedTimerSlider.value = 0f;
        }
    }
}
