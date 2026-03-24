using UnityEngine;
using UnityEngine.UI;                         // ✅ Slider 사용을 위한 네임스페이스
using UnityEngine.SceneManagement;            // ✅ 씬 이동(SceneManager) 사용을 위한 네임스페이스

[AddComponentMenu("Stats/Player Health System 2D")]
public class PlayerHealthSystem2D : MonoBehaviour
{
    [Header("체력 설정")]
    [SerializeField] private int maxHealth = 20;              // 최대 체력
    [SerializeField] private int currentHealth = 20;          // 현재 체력

    [Header("방어율 (0~100)")]
    [SerializeField, Range(0, 100)] private int defenseRate = 0; // 방어율

    [Header("피격용 Collider")]
    [SerializeField] private Collider2D hitCollider;          // 데미지 받는 콜라이더

    [Header("체력 UI - 메인 슬라이더")]
    [SerializeField] private Slider healthSlider;             // ✅ 현재 체력을 직접 표시하는 메인 체력 슬라이더

    [Header("체력 UI - 감소량 잔상 슬라이더")]
    [SerializeField] private Slider healthDamageSlider;       // ✅ 데미지 후 천천히 따라 내려오는 잔상용 체력 슬라이더
    [SerializeField] private float healthDamageFollowSpeed = 30f;   // ✅ 잔상 슬라이더가 내려가는 속도
    [SerializeField] private float healthInstantDropThreshold = 3f; // ✅ 한 번에 이 값 이상 떨어지면 '순간 감소'로 처리

    [Header("체력 자동 회복 설정")]
    [SerializeField] private float healthRegenPerSecond = 0f; // ✅ 초당 체력 회복량(0이면 회복 없음)

    [Header("사망 시 씬 이동 설정")]
    [SerializeField] private string sceneToLoadOnDeath;       // ✅ 현재 체력이 0이 되었을 때 이동할 씬 이름

    // 내부 상태
    private float previousHealthForDamageBar = 0f;            // ✅ 이전 프레임의 체력값(잔상 계산용)
    private float healthRegenAccumulator = 0f;                // ✅ 정수 체력에 소수 회복량을 누적하기 위한 버퍼
    private bool tookDamageThisFrame = false;                 // ✅ 이번 프레임에 데미지를 받았는지 표시(필요 시 활용 가능)

    private void Reset()                                      // ✅ 기본 콜라이더 자동 할당
    {
        hitCollider = GetComponentInChildren<Collider2D>();   // 자식에서 피격 콜라이더 찾기
    }

    private void OnEnable()                                   // ✅ 활성화 시 체력/슬라이더 초기화
    {
        // 체력 값 클램프
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // 잔상 계산용 이전 체력값 초기화
        previousHealthForDamageBar = currentHealth;

        // 메인 슬라이더, 잔상 슬라이더를 현재 체력에 맞게 초기화
        UpdateHealthSlider();
        UpdateHealthDamageSliderImmediate();

        // 회복 누적값 리셋
        healthRegenAccumulator = 0f;
        tookDamageThisFrame = false;
    }

    private void Update()                                     // ✅ 체력 자동 회복 + 슬라이더 갱신
    {
        float dt = Time.deltaTime;

        // 1) 체력 자동 회복 처리 (정수 체력에 부드럽게 적용)
        if (healthRegenPerSecond > 0f && currentHealth < maxHealth)
        {
            // 초당 회복량을 누적한 뒤, 1 이상 되면 정수 단위로 올려줌
            healthRegenAccumulator += healthRegenPerSecond * dt;

            if (healthRegenAccumulator >= 1f)
            {
                int healAmount = Mathf.FloorToInt(healthRegenAccumulator);
                healthRegenAccumulator -= healAmount;

                currentHealth += healAmount;
                if (currentHealth > maxHealth)
                    currentHealth = maxHealth;
            }
        }

        // 2) 메인 체력 슬라이더 갱신
        UpdateHealthSlider();

        // 3) 잔상 체력 슬라이더 갱신
        UpdateHealthDamageSlider(dt);

        // 4) 다음 프레임 비교용 이전 체력값 저장
        previousHealthForDamageBar = currentHealth;

        // 5) 프레임 끝에서 플래그 리셋
        tookDamageThisFrame = false;
    }

    // ─────────────────────────────────────────────
    // 데미지 처리
    // ─────────────────────────────────────────────
    public void ApplyDamage(int rawDamage)                    // ✅ 외부에서 호출하는 데미지 처리 메서드
    {
        if (rawDamage <= 0 || currentHealth <= 0)
            return;

        int clamped = Mathf.Clamp(defenseRate, 0, 100);
        float multiplier = (100f - clamped) * 0.01f;

        int final = Mathf.FloorToInt(rawDamage * multiplier);

        if (final <= 0 && rawDamage > 0)
            final = 1; // 최소 데미지 1

        // 데미지 플래그 세팅(필요 시 회복 로직과 연동 가능)
        tookDamageThisFrame = true;

        currentHealth -= final;
        if (currentHealth < 0)
            currentHealth = 0;

        Debug.Log($"플레이어가 데미지 {final} 받음 (남은 체력 {currentHealth})");

        if (currentHealth == 0)
        {
            Debug.Log("플레이어 사망");

            HandleDeath();                                    // ✅ 체력 0일 때 사망 처리 + 씬 이동 호출
        }

        // 데미지 직후 슬라이더를 한 번 강제로 갱신해주면 UI 반응이 빠름
        UpdateHealthSlider();
    }

    // ─────────────────────────────────────────────
    // 사망 처리(씬 이동)
    // ─────────────────────────────────────────────
    private void HandleDeath()                                // ✅ 사망 처리 및 씬 이동 담당 메서드
    {
        // 씬 이름이 비어있지 않을 때만 씬 이동 시도
        if (!string.IsNullOrEmpty(sceneToLoadOnDeath))
        {
            SceneManager.LoadScene(sceneToLoadOnDeath);       // ✅ 지정된 씬 이름으로 즉시 이동
        }
        else
        {
            // 씬 이름이 비어 있으면 경고만 출력하고 아무 것도 하지 않음
            Debug.LogWarning("[PlayerHealthSystem2D] 사망 시 이동할 씬 이름이 설정되지 않았습니다.", this);
        }
    }

    // ─────────────────────────────────────────────
    // UI 슬라이더 갱신 관련 메서드
    // ─────────────────────────────────────────────

    private void UpdateHealthSlider()                         // ✅ 메인 체력 슬라이더에 현재 체력 반영
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
    }

    private void UpdateHealthDamageSliderImmediate()          // ✅ 잔상 슬라이더를 즉시 현재 체력으로 맞추기
    {
        if (healthDamageSlider == null)
            return;

        healthDamageSlider.maxValue = maxHealth;
        healthDamageSlider.value = currentHealth;
    }

    private void UpdateHealthDamageSlider(float deltaTime)    // ✅ 잔상 슬라이더(감소량 연출) 갱신
    {
        if (healthDamageSlider == null)
            return;

        healthDamageSlider.maxValue = maxHealth;

        float mainValue = currentHealth;                      // 메인 체력 값
        float damageValue = healthDamageSlider.value;         // 잔상 슬라이더 현재 값
        float prev = previousHealthForDamageBar;              // 이전 프레임 체력값

        // 1) 한 프레임 기준 체력 감소량 계산
        float drop = prev - mainValue;

        // 2) '순간 감소'로 판단될 정도로 많이 깎인 경우 처리
        if (drop > 0f && drop >= healthInstantDropThreshold)
        {
            // 잔상 슬라이더를 이전 값 이상으로 올려놓고, 그 위에서부터 내려오게 함
            if (damageValue < prev)
            {
                damageValue = prev;
            }
        }

        // 3) 잔상 슬라이더가 메인 값보다 위에 있을 때만 부드럽게 아래로 따라오게 함
        if (damageValue > mainValue)
        {
            damageValue = Mathf.MoveTowards(
                damageValue,
                mainValue,
                healthDamageFollowSpeed * deltaTime);
        }
        else
        {
            // 메인 값보다 아래로 내려가 있으면 바로 메인 값에 맞춰줌(회복 시 튀지 않게)
            damageValue = mainValue;
        }

        healthDamageSlider.value = damageValue;
    }

    // ─────────────────────────────────────────────
    // Getter
    // ─────────────────────────────────────────────

    public int GetCurrentHealth() => currentHealth;           // ✅ 현재 체력 반환
    public int GetMaxHealth() => maxHealth;                   // ✅ 최대 체력 반환
    public Collider2D GetHitCollider() => hitCollider;        // ✅ 피격 콜라이더 반환
}
