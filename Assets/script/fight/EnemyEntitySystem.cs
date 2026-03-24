using UnityEngine;

[AddComponentMenu("Combat/Enemy Entity System (Modified)")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyEntitySystem : MonoBehaviour
{
    // ───────────────────────────────────────────────────────
    // 기본 구성
    // ───────────────────────────────────────────────────────
    [Header("기본 구성")]
    [SerializeField] private Rigidbody2D rb;                 // ✅ 물리 처리를 위한 Rigidbody
    private Collider2D cachedCol;                            // ✅ 본체 콜라이더 캐시

    // ───────────────────────────────────────────────────────
    // 방향/회전 설정
    // ───────────────────────────────────────────────────────
    [Header("방향/회전 설정")]
    [SerializeField] private bool autoReadFacingFromScale = true;     // ✅ 스케일 기반 자동 방향 감지
    [SerializeField] private int facingDir = +1;                      // ✅ 현재 바라보는 방향 (+1: 우, -1: 좌)
    [SerializeField] private bool useDirectionRotationOffset = true;  // ✅ 회전 오프셋 기능 사용 여부
    [SerializeField] private Transform rotationOffsetTarget;          // ✅ 회전을 적용할 대상 (보통 그래픽 자식)
    [SerializeField] private Vector3 rotationOffsetLeft;              // ✅ 왼쪽 볼 때의 회전값
    [SerializeField] private Vector3 rotationOffsetRight;             // ✅ 오른쪽 볼 때의 회전값
    private Vector3 baseLocalEuler;                                   // ✅ 초기 회전값 저장용

    // ───────────────────────────────────────────────────────
    // 패링 넉백 설정
    // ───────────────────────────────────────────────────────
    [Header("패링 넉백 설정")]
    [SerializeField] private bool useParryRelativeDirection = true;     // ✅ 패링 시 공격자 쪽을 바라보게 할지
    [SerializeField] private Vector2 baseKnockback = new Vector2(2, 2); // ✅ 적 본인 넉백 힘 벡터(기본)
    [SerializeField] private float knockbackForce = 6f;                 // ✅ 적 본인 넉백 강도
    [SerializeField] private Vector2 basePlayerKnockback = new(2, 2);   // ✅ 플레이어 넉백 힘 벡터(기본)
    [SerializeField] private float playerKnockbackForce = 6f;           // ✅ 플레이어 넉백 강도
    [SerializeField] private float horizontalEpsilon = 0.001f;          // ✅ 방향 계산 오차 허용값

    // ───────────────────────────────────────────────────────
    // 체력/방어
    // ───────────────────────────────────────────────────────
    [Header("체력 / 방어 설정")]
    [SerializeField] private int maxHealth = 10;            // ✅ 최대 체력
    [SerializeField] private int currentHealth = 10;        // ✅ 현재 체력
    [SerializeField, Range(0, 100)]
    private int defenseRate = 0;                            // ✅ 방어율 (퍼센트)
    [SerializeField] private bool minimumDamageOne = true;  // ✅ 최소 데미지 1 보장 여부
    [SerializeField] private bool disableOnZeroHealth = true; // ✅ 체력 0일 때 오브젝트 비활성화 여부
    [SerializeField] private int stoppingPower = 1;         // ✅ 관통 공격 저지력

    [Header("사운드")]
    [SerializeField] private AudioClip parryHitClip;        // ✅ 패링 당했을 때 효과음

    // ───────────────────────────────────────────────────────
    // 초기화 및 업데이트
    // ───────────────────────────────────────────────────────
    private void Reset()                                    // ✅ 기본 컴포넌트 자동 할당
    {
        rb = GetComponent<Rigidbody2D>();
        cachedCol = GetComponent<Collider2D>();
        currentHealth = maxHealth;
    }

    private void Awake()                                    // ✅ 컴포넌트 참조 보정
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (cachedCol == null) cachedCol = GetComponent<Collider2D>();
    }

    private void Start()                                    // ✅ 초기 회전값 캐싱
    {
        baseLocalEuler = rotationOffsetTarget != null
            ? rotationOffsetTarget.localEulerAngles
            : Vector3.zero;

        if (currentHealth <= 0) currentHealth = maxHealth;
    }

    private void Update()                                   // ✅ 스케일 기반 방향 감지(필요 시)
    {
        if (autoReadFacingFromScale)
        {
            float sx = transform.localScale.x;
            if (sx > 0.001f) facingDir = +1;
            else if (sx < -0.001f) facingDir = -1;
        }
    }

    private void LateUpdate()                               // ✅ 방향에 따른 회전 적용
    {
        ApplyDirectionRotationOffset();
    }

    // ───────────────────────────────────────────────────────
    // 방향 제어 메서드
    // ───────────────────────────────────────────────────────
    private void ApplyDirectionRotationOffset()             // ✅ 현재 facingDir에 따라 로컬 회전 적용
    {
        if (!useDirectionRotationOffset || rotationOffsetTarget == null) return;

        rotationOffsetTarget.localEulerAngles = (facingDir < 0)
            ? baseLocalEuler + rotationOffsetLeft
            : baseLocalEuler + rotationOffsetRight;
    }

    public void SetFacingX(int dir)                         // ✅ EnemyAI가 호출하는 방향 설정 메서드
    {
        if (dir == 0) return;

        autoReadFacingFromScale = false;                    // ✅ 명시적 방향 설정 시 자동 감지 끔
        facingDir = (dir > 0) ? +1 : -1;                    // ✅ +1 / -1로 정규화
    }

    // ───────────────────────────────────────────────────────
    // 패링 리액션 및 전투 로직
    // ───────────────────────────────────────────────────────
    public void ExecuteParryReaction(ParrySystem parry)     // ✅ 패링 성공 시 적/플레이어 넉백 처리
    {
        if (!IsAlive() || parry == null)
            return;

        // ✅ 이번 패링 시도에서 "첫 성공"이 아니라면 아무 것도 하지 않음
        if (!parry.TryRegisterParrySuccess())
        {
            return;                                         // 이미 이 윈도우에서 패링 성공 처리됨
        }

        // 효과음 재생
        if (SfxManager.Instance != null)
            SfxManager.Instance.PlaySfx(parryHitClip);

        // 1. 패링 위치 기준으로 좌/우 방향 계산 (시선 전환용)
        int parrySide = GetHorizontalDirectionFromSelfTo(parry.transform);
        if (parrySide == 0) parrySide = (facingDir == 0 ? +1 : facingDir);

        // 2. 패링 쪽을 바라보게 처리(옵션)
        if (useParryRelativeDirection)
        {
            SetFacingX(parrySide);                          // ✅ 적이 패링한 쪽을 바라보게
        }

        // 3. 플레이어/적 넉백 방향 계산
        PlayerBasicMovement player = parry.GetComponentInParent<PlayerBasicMovement>(); // ✅ 플레이어 찾기
        if (player != null && player.TryGetComponent(out Rigidbody2D playerRb))
        {
            int playerFacing = GetPlayerFacingDir(player);  // ✅ 플레이어가 바라보는 방향(+1/-1)
            if (playerFacing == 0)
            {
                // 플레이어 방향을 알 수 없으면, 적 기준 반대 방향 사용
                playerFacing = -parrySide;
            }

            int playerKnockSign = -playerFacing;            // ✅ "현재 바라보는 방향의 반대"로 넉백
            Vector2 pDir = new Vector2(
                playerKnockSign * Mathf.Abs(basePlayerKnockback.x),
                basePlayerKnockback.y);

            playerRb.AddForce(pDir.normalized * playerKnockbackForce,
                              ForceMode2D.Impulse);        // ✅ 플레이어 넉백 적용
        }

        // 적(본인) 넉백 방향
        int enemyFacing = (facingDir == 0) ? parrySide : facingDir; // ✅ 적이 바라보는 방향(없으면 패링 방향 사용)
        int enemyKnockSign = -enemyFacing;               // ✅ 적도 "자기가 보는 방향의 반대"로 밀림
        Vector2 eDir = new Vector2(
            enemyKnockSign * Mathf.Abs(baseKnockback.x),
            baseKnockback.y);

        rb.AddForce(eDir.normalized * knockbackForce,
                    ForceMode2D.Impulse);                // ✅ 적 넉백 적용

        // 4. 패링 성공 시 기력 회복
        StaminaSystem stamina = parry.GetStaminaSystem();
        if (stamina != null)
            stamina.RestoreStaminaOnParrySuccess();
    }

    // 유틸리티 메서드
    private int GetHorizontalDirectionFromSelfTo(Transform target) // ✅ 자기 기준 타겟의 좌/우 방향 계산
    {
        float dx = target.position.x - transform.position.x;
        if (dx > horizontalEpsilon) return +1;
        if (dx < -horizontalEpsilon) return -1;
        return 0;
    }

    private int GetPlayerFacingDir(PlayerBasicMovement player)     // ✅ PlayerBasicMovement의 facingDir 읽기
    {
        try
        {
            var f = player.GetType().GetField(
                "facingDir",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (f != null)
                return (int)f.GetValue(player);
        }
        catch { }

        return 0;                                                 // 알 수 없으면 0
    }

    // 체력 관리
    public bool IsAlive() => currentHealth > 0;                   // ✅ 살아있는지 여부
    public int GetStoppingPower() => stoppingPower;               // ✅ 관통 저지력 반환

    public void ApplyDamage(int rawDamage)                        // ✅ 데미지 적용
    {
        if (rawDamage <= 0 || currentHealth <= 0) return;

        int dr = Mathf.Clamp(defenseRate, 0, 100);
        int final = Mathf.FloorToInt(rawDamage * (100 - dr) * 0.01f);
        if (minimumDamageOne && final <= 0 && rawDamage > 0) final = 1;

        currentHealth -= final;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (disableOnZeroHealth) gameObject.SetActive(false);
        }
    }
}
