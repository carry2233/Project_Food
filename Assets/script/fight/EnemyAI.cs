using UnityEngine;
using System.Collections;

[AddComponentMenu("AI/Enemy AI (적 AI)")]          // ✅ 인스펙터 메뉴 경로
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 1. 필수 컴포넌트
    // ─────────────────────────────────────────

    [Header("필수 컴포넌트")]
    [SerializeField] private Rigidbody2D rb;                     // ✅ 적 Rigidbody2D
    [SerializeField] private EnemyEntitySystem enemyEntity;      // ✅ 방향/넉백/체력 관리 EnemyEntitySystem

    // ─────────────────────────────────────────
    // 2. 추적 및 이동
    // ─────────────────────────────────────────

    [Header("이동 및 추적 설정")]
    [SerializeField] private float moveSpeed = 2.5f;             // ✅ 이동 속도
    [SerializeField] private float accel = 8f;                   // ✅ 가속도(부드러운 이동용)
    [SerializeField] private bool isTracking = false;            // ✅ 플레이어 추적 중 여부

    [Header("감지용 콜라이더 (Trigger)")]
    [SerializeField] private Collider2D aggroCollider;           // ✅ 어그로 범위 콜라이더
    [SerializeField] private Collider2D attackRangeCollider;     // ✅ 공격 범위 콜라이더

    private int moveDir = 0;                                     // ✅ 이동 방향(-1, 0, +1)
    private PlayerHealthSystem2D target;                         // ✅ 추적 대상 플레이어

    // ─────────────────────────────────────────
    // 3. 점프 / 지면 체크
    // ─────────────────────────────────────────

    [Header("점프 설정")]
    [SerializeField] private Transform groundCheck;              // ✅ 바닥 체크 위치
    [SerializeField] private float groundRadius = 0.1f;          // ✅ 바닥 체크 반경
    [SerializeField] private LayerMask groundMask;               // ✅ 바닥 레이어 마스크
    private bool isGrounded = false;                             // ✅ 현재 바닥에 있는지 여부

    [SerializeField] private Transform frontRayOrigin;           // ✅ 전방 장애물 감지 Ray 시작점
    [SerializeField] private float rayDistance = 1f;             // ✅ 장애물 감지 거리
    [SerializeField] private LayerMask obstacleMask;             // ✅ 장애물 레이어 마스크

    [SerializeField] private float jumpForceX = 0f;              // ✅ 점프 시 X축 힘
    [SerializeField] private float jumpForceY = 7f;              // ✅ 점프 시 Y축 힘

    // ─────────────────────────────────────────
    // 4. 공격 설정
    // ─────────────────────────────────────────

    [Header("공격 설정")]
    [SerializeField] private Collider2D attackHitBox;            // ✅ 실제 공격 판정용 콜라이더(Trigger)
    [SerializeField] private int attackDamage = 3;               // ✅ 공격 데미지
    [SerializeField] private float attackWindup = 0.25f;         // ✅ 공격 선딜레이 시간
    [SerializeField] private float attackActiveTime = 0.2f;      // ✅ 공격 판정 유지 시간
    [SerializeField] private float attackCooldown = 1.2f;        // ✅ 공격 쿨타임

    private bool isAttacking = false;                            // ✅ 현재 공격 중인지 여부
    private bool didHitThisAttack = false;                       // ✅ 이번 공격에서 이미 히트했는지 여부

    // ─────────────────────────────────────────
    // 5. 애니메이션 (기본 / 공격 / 패링 피격)
    // ─────────────────────────────────────────

    [Header("기본 애니메이션 (항상 재생될 기본 상태)")]
    [SerializeField] private SimpleAnimationPlayer baseAnimPlayer;   // ✅ 기본(대기/이동) 애니메이션 플레이어
    [SerializeField] private FlipbookAnimation baseAnimation;        // ✅ 기본 상태에서 돌릴 플립북 애니메이션

    [Header("공격 / 패링 애니메이션")]
    [SerializeField] private FlipbookAnimation attackAnimation;       // ✅ 공격 애니메이션 플립북
    [SerializeField] private SimpleAnimationPlayer attackAnimPlayer;  // ✅ 공격 애니 플레이어(스프라이트 렌더러 포함)

    [SerializeField] private FlipbookAnimation parryHitAnimation;     // ✅ 패링 피격 애니메이션 플립북
    [SerializeField] private SimpleAnimationPlayer parryHitAnimPlayer;// ✅ 패링 피격 애니 플레이어

    // ─────────────────────────────────────────
    // 6. 패링 경직(행동불가) 설정
    // ─────────────────────────────────────────

    [Header("패링 리액션(경직 시간 설정)")]
    [SerializeField] private float parryStunAnimDuration = 0.4f;      // ✅ 시간1: 패링피격 애니 재생 + 완전 행동불가 유지 시간
    [SerializeField] private float parryStunRecoveryDuration = 0.4f;  // ✅ 시간2: 기본 애니로 돌아온 뒤까지 계속 행동불가 유지 시간

    private bool isStunned = false;                                   // ✅ 현재 패링 경직 상태인지 여부
    private Coroutine stunCoroutine;                                  // ✅ 패링 경직 코루틴 핸들

    // ─────────────────────────────────────────
    // 초기화 및 유니티 콜백
    // ─────────────────────────────────────────

    private void Reset()                                             // ✅ Reset 시 기본 참조 자동 할당
    {
        rb = GetComponent<Rigidbody2D>();                            // ✅ 자기 Rigidbody2D
        enemyEntity = GetComponent<EnemyEntitySystem>();             // ✅ EnemyEntitySystem

        // 자식에서 SimpleAnimationPlayer 찾아서 기본 할당
        if (attackAnimPlayer == null || parryHitAnimPlayer == null || baseAnimPlayer == null)
        {
            var players = GetComponentsInChildren<SimpleAnimationPlayer>();
            if (players.Length > 0 && baseAnimPlayer == null)
                baseAnimPlayer = players[0];                         // ✅ 첫 번째를 기본 애니용으로 사용
            if (players.Length > 1 && attackAnimPlayer == null)
                attackAnimPlayer = players[1];                       // ✅ 두 번째를 공격용
            if (players.Length > 2 && parryHitAnimPlayer == null)
                parryHitAnimPlayer = players[2];                     // ✅ 세 번째를 패링용
        }

        // 기본 애니 플레이어가 없으면 공격용을 기본으로도 재사용
        if (baseAnimPlayer == null)
            baseAnimPlayer = attackAnimPlayer;                       // ✅ 최소 한 개는 기본으로 사용
    }

private void Start()                                             // ✅ 시작 시 한 번 호출
{
    if (attackHitBox != null)
        attackHitBox.enabled = false;                            // 공격 히트박스 기본 비활성

    // 🔹 기본 애니메이션 재생
    if (baseAnimPlayer != null && baseAnimation != null)
    {
        baseAnimPlayer.gameObject.SetActive(true);               // 기본 애니 오브젝트 활성화
        baseAnimPlayer.PlayOnce(baseAnimation);                  // 기본 애니 재생
    }

    // 🔹 패링 피격 애니는 렌더러만 끄도록 수정 (오브젝트 자체를 OFF 하지 않음)
    if (parryHitAnimPlayer != null)
    {
        var sr = parryHitAnimPlayer.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.enabled = false;                                   // ⬅️ 기존: parryHitAnimPlayer.gameObject.SetActive(false)
    }
}


    private void Update()                                            // ✅ 논리 업데이트
    {
        // 패링 경직 상태일 때는 이동/점프/공격 방향 계산 자체를 막고, 정지 상태 유지
        if (isStunned)
        {
            moveDir = 0;                                             // ✅ 움직임 방향 0으로 고정
            return;                                                  // ✅ 아래 로직(지면 체크/점프/추적) 생략
        }

        GroundCheck();                                               // ✅ 지면 체크
        RaycastJumpCheck();                                          // ✅ 전방 장애물 감지 후 점프 시도

        if (isTracking && target != null)                            // ✅ 추적 중이고 타겟이 있다면
        {
            UpdateMoveDirection();                                   // → 이동 방향 갱신
        }
        else
        {
            moveDir = 0;                                             // 추적 안 하면 정지
        }
    }

    private void FixedUpdate()                                       // ✅ 물리 업데이트
    {
        // 공격 중이거나 패링 경직 상태라면 이동 불가
        if (isAttacking || isStunned) return;                        // ✅ 공격/경직 중 이동 금지

        // 🔹 PlayerBasicMovement와 비슷하게: 지면에 있을 때만 이동
        if (!isGrounded)                                             // 공중이면 X속도 갱신 안 함
            return;

        float targetVel = moveDir * moveSpeed;                       // 목표 X 속도
        float newX = Mathf.Lerp(rb.linearVelocity.x, targetVel,
                                accel * Time.fixedDeltaTime);        // 부드러운 가감속
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);  // X만 갱신
    }

    // ─────────────────────────────────────────
    // 지면 체크 / 점프 / 이동 방향
    // ─────────────────────────────────────────

    private void GroundCheck()                                       // ✅ 지면 체크
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position, groundRadius, groundMask);
    }

    private void RaycastJumpCheck()                                  // ✅ 전방 장애물 감지 후 점프 시도
    {
        if (!isGrounded || frontRayOrigin == null) return;

        RaycastHit2D hit = Physics2D.Raycast(
            frontRayOrigin.position,
            Vector2.right * moveDir,
            rayDistance,
            obstacleMask);

        if (hit.collider != null)
        {
            TryJump();                                               // 장애물 있으면 점프 시도
        }
    }

    private void TryJump()                                           // ✅ 점프 실행
    {
        if (!isGrounded) return;
        if (isStunned) return;                                       // ✅ 경직 중에는 점프 금지

        Vector2 force = new Vector2(jumpForceX * moveDir, jumpForceY);
        rb.AddForce(force, ForceMode2D.Impulse);
    }

    private void UpdateMoveDirection()                               // ✅ 타겟 기준 이동 방향 결정
    {
        if (target == null) return;

        float dx = target.transform.position.x - transform.position.x;

        if (dx > 0.1f) moveDir = +1;
        else if (dx < -0.1f) moveDir = -1;
        else moveDir = 0;

        if (enemyEntity != null && moveDir != 0)
        {
            enemyEntity.SetFacingX(moveDir);                         // ✅ EnemyEntity에 바라보는 방향 전달
        }
    }

    // ─────────────────────────────────────────
    // Trigger 감지 (어그로 / 공격 시작 / 히트 판정)
    // ─────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)                  // ✅ Trigger 진입 시
    {
        // 1) 플레이어 찾기
        PlayerHealthSystem2D ph = other.GetComponentInParent<PlayerHealthSystem2D>();
        if (ph != null)
        {
            if (target == null)
                target = ph;                                         // ✅ 최초 타겟 설정
        }

        // 2) 어그로 범위 들어오면 추적 시작
        if (aggroCollider != null && aggroCollider.IsTouching(other))
        {
            isTracking = true;                                       // ✅ 추적 ON
        }

        // 3) 공격 범위 들어오면 공격 루프 시작
        if (attackRangeCollider != null && attackRangeCollider.IsTouching(other))
        {
            if (!isStunned)                                          // ✅ 경직 중이 아닐 때만 공격 루프 시작
            {
                StartCoroutine(AttackLoop());                        // ✅ 공격 루프 코루틴 시작
            }
        }

        // 4) 공격 히트박스와의 충돌이면 → 데미지/패링 처리
        if (isAttacking &&
            attackHitBox != null &&
            attackHitBox.enabled &&
            attackHitBox.IsTouching(other))
        {
            HandleHitOrParry(other);                                 // ✅ 실제 히트/패링 판정 처리
        }
    }

    private void OnTriggerStay2D(Collider2D other)                   // ✅ Trigger 유지 중에도 검사
    {
        if (isAttacking &&
            attackHitBox != null &&
            attackHitBox.enabled &&
            attackHitBox.IsTouching(other))
        {
            HandleHitOrParry(other);                                 // ✅ 빠른 연속 프레임에서의 충돌 보정
        }
    }

    private void OnTriggerExit2D(Collider2D other)                   // ✅ 필요 시 타겟 이탈 처리용(현재는 비워둠)
    {
        // 현재 구조에서는 AttackLoop 내부의 IsTouching 체크로 범위 이탈을 판정하므로
        // 여기서는 별도 처리를 하지 않음.
    }

    // ─────────────────────────────────────────
    // 공격 루프 / 1회 공격
    // ─────────────────────────────────────────

    private IEnumerator AttackLoop()                                 // ✅ 공격 반복 루프
    {
        // 타겟이 있고, 공격 범위에 있는 동안 반복
        while (!isStunned &&                                         // ✅ 경직 중이 아니어야 함
               target != null &&
               attackRangeCollider != null &&
               target.GetHitCollider() != null &&
               attackRangeCollider.IsTouching(target.GetHitCollider()))
        {
            if (!isAttacking)
            {
                yield return StartCoroutine(DoAttack());             // ✅ 공격 1회 수행
            }

            yield return null;
        }
    }

    private IEnumerator DoAttack()                                   // ✅ 공격 1회 수행
    {
        // 경직 상태면 공격 자체를 시작하지 않음
        if (isStunned)
            yield break;

        isAttacking = true;
        didHitThisAttack = false;                                    // ✅ 이번 공격에서 아직 히트 안 함

        // 1) 선딜레이
        yield return new WaitForSeconds(attackWindup);

        // 2) 공격 애니메이션 재생
        if (attackAnimPlayer != null && attackAnimation != null)
        {
            attackAnimPlayer.PlayOnce(attackAnimation);              // ✅ 공격 플립북 1회 재생
        }

        // 3) 공격 판정 활성화
        if (attackHitBox != null)
            attackHitBox.enabled = true;

        float timer = 0f;
        while (timer < attackActiveTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 4) 공격 판정 비활성화
        if (attackHitBox != null)
            attackHitBox.enabled = false;

        // 5) 쿨타임 대기
        yield return new WaitForSeconds(attackCooldown);

        isAttacking = false;
    }

    // ─────────────────────────────────────────
    // 히트 / 패링 판정 처리
    // ─────────────────────────────────────────

    private void HandleHitOrParry(Collider2D other)                  // ✅ 실제 데미지/패링 처리 메서드
    {
        // 이미 이 공격에서 한 번 처리했으면 무시
        if (!isAttacking || didHitThisAttack)
            return;

        // ① 패링 우선 처리
        ParrySystem parry = other.GetComponentInParent<ParrySystem>();
        if (parry != null && parry.IsParryActive())
        {
            HandleParry(parry);                                      // ✅ 패링 리액션 처리
            didHitThisAttack = true;                                 // ✅ 이번 공격 처리 완료
            return;
        }

        // ② 패링이 아니면 플레이어 데미지 처리
        PlayerHealthSystem2D ph = other.GetComponentInParent<PlayerHealthSystem2D>();
        if (ph != null)
        {
            ph.ApplyDamage(attackDamage);                            // ✅ 플레이어에게 데미지 적용
            didHitThisAttack = true;                                 // ✅ 중복 타격 방지
        }
    }

    private void HandleParry(ParrySystem parry)                      // ✅ 패링 성공 시 처리
    {
        // 1) EnemyEntitySystem에 넉백/기력회복 등 패링 리액션 위임
        if (enemyEntity != null)
        {
            enemyEntity.ExecuteParryReaction(parry);                 // ✅ 넉백/방향/기력 회복 처리
        }

        // 2) 공격 강제 종료
        if (attackHitBox != null)
            attackHitBox.enabled = false;

        isAttacking = false;

        // 3) 패링 경직(시간1 + 시간2) 시퀀스 시작
        StartParryStunSequence();                                   // ✅ 행동불가 + 애니메이션 전환 시퀀스 시작
    }

    // ─────────────────────────────────────────
    // 패링 경직 시퀀스 (시간1 + 시간2)
    // ─────────────────────────────────────────

    private void StartParryStunSequence()                            // ✅ 패링 경직 시퀀스 시작 메서드
    {
        // 이미 경직 중이면 기존 코루틴 정지 후 새로 시작
        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }

        stunCoroutine = StartCoroutine(Co_ParryStunSequence());      // ✅ 실제 경직 코루틴 시작
    }

private IEnumerator Co_ParryStunSequence()                       // 패링 경직 전체 흐름
{
    isStunned = true;                                            // 행동불가 시작
    moveDir = 0;
    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

    // ─────────────────────────────────────────────
    // 🔹 1단계: 기본 애니 정지 + 패링 피격 재생
    // ─────────────────────────────────────────────

    // 기본 애니의 SpriteRenderer만 끄기 (오브젝트 자체 OFF 금지)
    if (baseAnimPlayer != null)
    {
        var srBase = baseAnimPlayer.GetComponent<SpriteRenderer>();
        if (srBase != null)
            srBase.enabled = false;                              // ⬅️ 기존: baseAnimPlayer.gameObject.SetActive(false)
    }

    // 패링 피격 애니의 렌더러 켜기 + 애니 재생
    if (parryHitAnimPlayer != null && parryHitAnimation != null)
    {
        var srParry = parryHitAnimPlayer.GetComponent<SpriteRenderer>();
        if (srParry != null)
            srParry.enabled = true;                              // ⬅️ 기존: parryHitAnimPlayer.gameObject.SetActive(true)

        parryHitAnimPlayer.PlayOnce(parryHitAnimation);
    }

    yield return new WaitForSeconds(parryStunAnimDuration);

    // ─────────────────────────────────────────────
    // 🔹 2단계: 기본 애니 복구
    // ─────────────────────────────────────────────

    // 패링 피격 렌더러 끄기
    if (parryHitAnimPlayer != null)
    {
        var srParry = parryHitAnimPlayer.GetComponent<SpriteRenderer>();
        if (srParry != null)
            srParry.enabled = false;                             // ⬅️ 기존: parryHitAnimPlayer.gameObject.SetActive(false)
    }

    // 기본 애니의 렌더러 다시 켜기 + 기본 애니 재생
    if (baseAnimPlayer != null)
    {
        var srBase = baseAnimPlayer.GetComponent<SpriteRenderer>();
        if (srBase != null)
            srBase.enabled = true;                               // ⬅️ 기존: baseAnimPlayer.gameObject.SetActive(true)

        if (baseAnimation != null)
            baseAnimPlayer.PlayOnce(baseAnimation);
    }

    yield return new WaitForSeconds(parryStunRecoveryDuration);

    isStunned = false;                                           // 행동불가 종료
    stunCoroutine = null;
}

}
