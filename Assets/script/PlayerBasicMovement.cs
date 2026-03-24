using UnityEngine;

[AddComponentMenu("Gameplay/Player Basic Movement (Force 2D)")]
[DisallowMultipleComponent]
public class PlayerBasicMovement : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // enum (점프 타입 구분)
    // ─────────────────────────────────────────────────────────────────────────────

    private enum JumpType                                     // ✅ 점프 종류 구분용 enum
    {
        None,                                                 // ✅ 대기(점프 없음)
        Normal,                                               // ✅ 기본 점프
        Charged                                               // ✅ 홀드(차지) 점프
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("필수 구성")]
    [SerializeField] private Rigidbody2D rb;                  // ✅ 이동에 사용할 Rigidbody2D
    [SerializeField] private LayerMask groundMask;            // ✅ 지면 판정용 레이어 마스크

    [Header("지면 판정")]
    [SerializeField] private Transform groundCheck;           // ✅ 발밑 체크 기준 Transform
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f); // ✅ 발밑 박스 크기
    [SerializeField] private float groundCheckDistance = 0.1f;// ✅ 발밑 체크 오프셋 거리

    [Header("이동 설정")]
    [SerializeField] private float maxMoveSpeed = 5f;         // ✅ 최대 이동 속도
    [SerializeField] private float minMoveForce = 5f;         // ✅ 이동 시작 시 최소 힘
    [SerializeField] private float maxMoveForce = 20f;        // ✅ 가속 시 최대 힘
    [SerializeField] private float moveForceRampUpTime = 0.5f;// ✅ 최대로 가속되는 데 걸리는 시간

    [Header("점프 설정")]
    [SerializeField] private float jumpForce = 10f;           // ✅ 기본 점프(위로) 힘
    [SerializeField] private float coyoteTime = 0.1f;         // ✅ 코요테 타임(지면 떠난 후 여유 시간)
    [SerializeField] private float jumpBufferTime = 0.1f;     // ✅ 점프 버퍼(미리 누른 점프 허용 시간)

    [Header("특수 점프(홀드) 설정")]
    [SerializeField] private float chargedJumpHoldTime = 0.3f;// ✅ 홀드 점프로 인식할 최소 키 유지 시간
    [SerializeField] private float chargedJumpForce = 12f;    // ✅ 홀드 점프 힘(임펄스 크기)
    [SerializeField] private Vector2 chargedJumpDirection = new Vector2(1f, 1f); // ✅ 바라보는 방향 기준 점프 방향(오른쪽 기준)

    [Header("드래그 설정")]
    [SerializeField] private float groundLinearDrag = 5f;     // ✅ 지상일 때 선형 감속
    [SerializeField] private float airLinearDrag = 1f;        // ✅ 공중일 때 선형 감속

    [Header("입력 키 설정")]
    [SerializeField] private KeyCode leftKey = KeyCode.A;     // ✅ 왼쪽 이동 키
    [SerializeField] private KeyCode rightKey = KeyCode.D;    // ✅ 오른쪽 이동 키
    [SerializeField] private KeyCode jumpKey = KeyCode.Space; // ✅ 점프 키

    [Header("회전 오프셋(무기/자식 객체 회전용)")]
    [SerializeField] private Transform rotationOffsetTarget;  // ✅ 회전시킬 대상(예: 무기 루트)
    [SerializeField] private Vector3 rotationOffsetLeft;      // ✅ 왼쪽 볼 때 추가 회전값
    [SerializeField] private Vector3 rotationOffsetRight;     // ✅ 오른쪽 볼 때 추가 회전값

    [Header("입력 제어")]
    [SerializeField] private bool inputsEnabled = true;       // ✅ 현재 입력 허용 여부
    [SerializeField] private float inputEnableDelay = 0f;     // ✅ 시작 후 입력 허용까지 지연 시간

    [Header("점프 사운드 설정")]
    [SerializeField] private AudioClip normalJumpClip;        // ✅ 기본 점프 실행 시 재생할 사운드
    [SerializeField] private AudioClip chargedJumpClip;       // ✅ 홀드 점프 실행 시 재생할 사운드

    [Header("기력 시스템")]
    [SerializeField] private StaminaSystem staminaSystem;     // ✅ 기력 시스템(StaminaSystem) 참조

    [Header("행동 가능 여부")]
    [SerializeField] private bool canAct = true;              // ✅ 이동/점프 입력을 받을 수 있는지 여부

    // 내부 상태
    private bool isGrounded;                                  // ✅ 현재 지면 위인지 여부
    private float lastGroundedTime;                           // ✅ 마지막으로 지면에 있던 시각
    private float lastJumpPressedTime;                        // ✅ (탭) 점프 버퍼용 마지막 입력 시각

    private int facingDir = 1;                                // ✅ 바라보는 방향(-1: 왼쪽, +1: 오른쪽)
    private int currentMoveDir = 0;                           // ✅ 현재 이동 방향(-1, 0, +1)
    private float currentMoveForce = 0f;                      // ✅ 현재 적용 중인 이동 힘(가속 램프)

    private int lastPreferredDir = 1;                         // ✅ 좌우 둘 다 눌렸을 때 우선 순위
    private bool jumpPressing = false;                        // ✅ 점프 키가 눌려 있는지
    private float jumpPressedTime = 0f;                       // ✅ 점프 키 누른 후 경과 시간(홀드 판정용)
    private bool chargedJumpQueued = false;                   // ✅ 홀드 점프 준비(대기) 상태인지

    private JumpType pendingJumpType = JumpType.None;         // ✅ 다음 FixedUpdate에서 실행할 점프 타입

    private Vector3 baseLocalEuler;                           // ✅ rotationOffsetTarget의 기본 로컬 회전값

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Reset()                                      // ✅ 기본 구성 자동 할당
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (groundCheck == null)
            groundCheck = transform;

        if (rotationOffsetTarget == null)
            rotationOffsetTarget = transform;

        if (staminaSystem == null)
            staminaSystem = GetComponentInParent<StaminaSystem>(); // ✅ 기본으로 부모에서 기력시스템 찾기
    }

    private void Awake()                                      // ✅ 초기값 저장
    {
        if (rotationOffsetTarget != null)
        {
            baseLocalEuler = rotationOffsetTarget.localEulerAngles;
        }
    }

    private void OnEnable()                                   // ✅ 활성화 시 입력 딜레이 처리
    {
        pendingJumpType = JumpType.None;                      // ✅ 점프 대기 상태 초기화

        if (inputEnableDelay > 0f)
        {
            inputsEnabled = false;
            StartCoroutine(Co_EnableInputsAfterDelay());
        }
        else
        {
            inputsEnabled = true;
        }
    }

    private void Update()                                     // ✅ 입력/상태 업데이트
    {
        UpdateGroundedState();                                // ✅ 지면 체크
        HandleJumpInput();                                    // ✅ 점프 키(탭/홀드) 처리
        CheckAndQueueJumpInUpdate();                          // ✅ 점프 가능 조건 만족 시 "점프 예약 + 사운드" 처리
        HandleRecentDirectionInput();                         // ✅ 좌우 입력 처리
        UpdateFacingByMoveDir();                              // ✅ 바라보는 방향 갱신
        UpdateRotationOffset();                               // ✅ 회전 오프셋 적용
    }

    private void FixedUpdate()                                // ✅ 물리 업데이트(실제 힘/속도 적용)
    {
        ApplyMovementForce();                                 // ✅ 이동 힘 적용
        ApplyLinearDrag();                                    // ✅ 드래그 적용
        ApplyJumpIfBuffered();                                // ✅ 예약된 점프 임펄스 적용
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────────────

    private System.Collections.IEnumerator Co_EnableInputsAfterDelay() // ✅ 입력 지연 후 활성화 코루틴
    {
        if (inputEnableDelay > 0f)
            yield return new WaitForSeconds(inputEnableDelay);

        inputsEnabled = true;
    }

    public int GetFacingDir()                                 // ✅ 현재 바라보는 좌우 방향(-1/0/+1)
    {
        return facingDir;
    }

    public void SetInputsEnabled(bool enabled)                // ✅ 외부에서 입력 적용 On/Off
    {
        inputsEnabled = enabled;
        if (!enabled)
        {
            currentMoveDir = 0;
            currentMoveForce = 0f;
            jumpPressing = false;
            jumpPressedTime = 0f;
            chargedJumpQueued = false;
            pendingJumpType = JumpType.None;                  // ✅ 입력 비활성 시 점프 예약도 초기화
        }
    }

public void SetCanAct(bool value)                // ✅ 외부에서 행동 가능 여부 설정(기력 고갈 등)
{
    canAct = value;                              // ✅ 행동 가능 플래그만 변경

    if (!canAct)
    {
        // ❌ 이동 관련 값은 그대로 두고
        // ✅ 점프 관련 입력/대기 상태만 초기화해서 점프만 못 하게 막음
        jumpPressing = false;                    // ✅ 점프 키 눌림 상태 초기화
        jumpPressedTime = 0f;                    // ✅ 홀드 시간 초기화
        chargedJumpQueued = false;               // ✅ 차지 점프 대기 상태 해제
        pendingJumpType = JumpType.None;         // ✅ 예약된 점프 제거
    }
}


    private void UpdateGroundedState()                        // ✅ 지면 체크 및 상태 갱신
    {
        if (groundCheck == null)
            return;

        Vector2 origin = groundCheck.position;
        Vector2 size = groundCheckSize;
        Vector2 offset = Vector2.down * groundCheckDistance;

        Collider2D hit = Physics2D.OverlapBox(origin + offset, size, 0f, groundMask);
        bool wasGrounded = isGrounded;
        isGrounded = (hit != null);

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        if (!wasGrounded && isGrounded)
        {
            currentMoveForce = 0f;
        }
    }

    private void HandleJumpInput()                            // ✅ 점프 입력 처리(탭/홀드 구분)
    {
        if (!inputsEnabled || !canAct)
            return;

        // 점프 키를 누르기 시작한 순간
        if (Input.GetKeyDown(jumpKey))
        {
            jumpPressing = true;
            jumpPressedTime = 0f;
            chargedJumpQueued = false;
            // 👉 어떤 점프일지는 나중에 조건이 맞는 순간에 결정
        }

        // 점프 키를 뗐을 때
        if (Input.GetKeyUp(jumpKey))
        {
            if (jumpPressing)
            {
                // 아직 홀드 점프가 준비되지 않았다면 → 짧게 누른 탭 점프로 간주, 버퍼 기록
                if (!chargedJumpQueued)
                {
                    lastJumpPressedTime = Time.time;          // ✅ 기본 점프 버퍼 입력 시각
                }
            }

            jumpPressing = false;
            jumpPressedTime = 0f;
        }

        // 키를 누르고 있는 동안 홀드 시간 누적 + 홀드 준비 플래그 설정
        if (jumpPressing)
        {
            jumpPressedTime += Time.deltaTime;

            if (!chargedJumpQueued && jumpPressedTime >= chargedJumpHoldTime)
            {
                chargedJumpQueued = true;                     // ✅ 일정 시간 이상 누르면 홀드 점프 준비
            }
        }
    }

    private void CheckAndQueueJumpInUpdate()                  // ✅ 점프 가능 조건 만족 시 "점프 예약 + 사운드" 처리
    {
        if (!inputsEnabled || !canAct)
            return;

        // 이미 점프가 예약되어 있으면 중복 예약하지 않음
        if (pendingJumpType != JumpType.None)
            return;

        bool canUseCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool hasJumpBuffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        // 1) 홀드 점프(차지 점프) 우선 처리
        if (chargedJumpQueued && (isGrounded || canUseCoyote))
        {
            // ✅ 기력 체크: 특수 점프에 필요한 기력이 있는지 확인
            if (staminaSystem != null && !staminaSystem.TryConsumeForChargedJump())
            {
                // 기력이 부족하면 홀드 점프 자체를 취소
                chargedJumpQueued = false;
                jumpPressing = false;
                jumpPressedTime = 0f;
                return;
            }

            pendingJumpType = JumpType.Charged;              // ✅ 다음 FixedUpdate에서 홀드 점프 실행 예약

            // 홀드 점프 사운드 즉시 재생 (입력에 최대한 가깝게)
            if (SfxManager.Instance != null && chargedJumpClip != null)
            {
                SfxManager.Instance.PlaySfx(chargedJumpClip);
            }

            chargedJumpQueued = false;
            jumpPressing = false;
            jumpPressedTime = 0f;
            lastJumpPressedTime = -999f;                     // ✅ 기본 점프 버퍼 초기화

            return;
        }

        // 2) 일반 점프 처리(탭 입력 + 버퍼 + 코요테)
        if ((isGrounded || canUseCoyote) && hasJumpBuffered)
        {
            // ✅ 기력 체크: 일반 점프 기력 소모
            if (staminaSystem != null && !staminaSystem.TryConsumeForNormalJump())
            {
                // 기력이 부족하면 점프 자체를 하지 않음
                lastJumpPressedTime = -999f;
                jumpPressing = false;
                jumpPressedTime = 0f;
                return;
            }

            pendingJumpType = JumpType.Normal;               // ✅ 다음 FixedUpdate에서 기본 점프 실행 예약

            // 기본 점프 사운드 즉시 재생
            if (SfxManager.Instance != null && normalJumpClip != null)
            {
                SfxManager.Instance.PlaySfx(normalJumpClip);
            }

            lastJumpPressedTime = -999f;
            jumpPressing = false;
            jumpPressedTime = 0f;

            return;
        }
    }

private void HandleRecentDirectionInput()        // ✅ 가장 최근에 눌린 키 기반 이동 의도 계산
{
    if (!inputsEnabled)
    {
        currentMoveDir = 0;                      // ✅ 입력 자체가 꺼져 있을 때만 이동을 멈춤
        currentMoveForce = 0f;
        return;
    }

    // 🔴 여기서부터는 canAct와 상관없이 좌우 입력을 받을 수 있게 함
    bool leftHeld = Input.GetKey(leftKey);       // ✅ 왼쪽 키 눌림 여부
    bool rightHeld = Input.GetKey(rightKey);     // ✅ 오른쪽 키 눌림 여부

    if (Input.GetKeyDown(leftKey))  lastPreferredDir = -1; // ✅ 둘 다 눌릴 때 우선 방향 갱신
    if (Input.GetKeyDown(rightKey)) lastPreferredDir = +1;

    int intended =
        (leftHeld && rightHeld) ? lastPreferredDir :
        (leftHeld ? -1 : (rightHeld ? +1 : 0));  // ✅ 실제 의도된 이동 방향 계산

    if (intended != 0 && intended != currentMoveDir && isGrounded)
    {
        currentMoveDir = intended;               // ✅ 새 방향으로 이동 시작
        currentMoveForce = minMoveForce;         // ✅ 가속 시작값
        facingDir = intended;                    // ✅ 바라보는 방향도 함께 변경
    }
    else if (intended == 0)
    {
        currentMoveDir = 0;                      // ✅ 입력 없으면 멈춤
        currentMoveForce = 0f;
    }
    else if (currentMoveDir == 0 && intended != 0 && isGrounded)
    {
        currentMoveDir = intended;               // ✅ 정지 상태에서 다시 이동 시작
        currentMoveForce = minMoveForce;
    }
}

    private void UpdateFacingByMoveDir()                      // ✅ 이동 방향 기준 바라보는 방향 갱신
    {
        if (currentMoveDir != 0)
        {
            facingDir = currentMoveDir;
        }
    }

    private void ApplyMovementForce()                         // ✅ 좌우 이동 힘 적용(가속 램프 방식)
    {
        if (!inputsEnabled)
            return;

        if (currentMoveDir == 0 || !isGrounded)
        {
            float decel = maxMoveForce * Time.fixedDeltaTime;
            currentMoveForce = Mathf.MoveTowards(currentMoveForce, 0f, decel);
        }
        else
        {
            float target = maxMoveForce;
            float rampSpeed = (moveForceRampUpTime > 0f)
                ? (maxMoveForce - minMoveForce) / moveForceRampUpTime
                : Mathf.Infinity;

            currentMoveForce = Mathf.MoveTowards(
                currentMoveForce,
                target,
                rampSpeed * Time.fixedDeltaTime);

            if (currentMoveForce < minMoveForce)
            {
                currentMoveForce = minMoveForce;
            }
        }

        float currentSpeedX = rb.linearVelocity.x;

        if (Mathf.Abs(currentSpeedX) > maxMoveSpeed && Mathf.Sign(currentSpeedX) == Mathf.Sign(currentMoveDir))
        {
            currentSpeedX = Mathf.MoveTowards(
                currentSpeedX,
                maxMoveSpeed * Mathf.Sign(currentSpeedX),
                maxMoveSpeed * Time.fixedDeltaTime);

            Vector2 v = rb.linearVelocity;
            v.x = currentSpeedX;
            rb.linearVelocity = v;
        }
        else if (Mathf.Abs(currentSpeedX) < 0.01f && currentMoveDir == 0)
        {
            currentSpeedX = 0f;
        }

        if (Mathf.Abs(currentSpeedX) < maxMoveSpeed)
        {
            Vector2 force = new Vector2(currentMoveDir * currentMoveForce, 0f);
            rb.AddForce(force, ForceMode2D.Force);
        }

        if (!isGrounded)
        {
            currentMoveDir = 0;
            currentMoveForce = 0f;
        }
    }

    private void ApplyLinearDrag()                             // ✅ 지상/공중 선형 드래그 적용
    {
        rb.linearDamping = isGrounded ? groundLinearDrag : airLinearDrag;
    }

    private void ApplyJumpIfBuffered()                         // ✅ 예약된 점프 임펄스 실제 적용 (FixedUpdate)
    {
        if (!inputsEnabled || !canAct)
            return;

        if (pendingJumpType == JumpType.None)
            return;

        // 수직 속도 제거 후 점프 임펄스 적용
        Vector2 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        if (pendingJumpType == JumpType.Charged)
        {
            Vector2 dir = chargedJumpDirection;

            if (facingDir < 0)
                dir.x = -Mathf.Abs(dir.x);
            else if (facingDir > 0)
                dir.x = Mathf.Abs(dir.x);

            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;

            rb.AddForce(dir.normalized * chargedJumpForce, ForceMode2D.Impulse);
        }
        else if (pendingJumpType == JumpType.Normal)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        // 점프 1회 처리 후 예약 초기화
        pendingJumpType = JumpType.None;
    }

    private void UpdateRotationOffset()                        // ✅ 바라보는 방향에 따라 회전 오프셋 적용
    {
        if (rotationOffsetTarget == null)
            return;

        if (facingDir < 0)
            rotationOffsetTarget.localEulerAngles = baseLocalEuler + rotationOffsetLeft;
        else if (facingDir > 0)
            rotationOffsetTarget.localEulerAngles = baseLocalEuler + rotationOffsetRight;
        else
            rotationOffsetTarget.localEulerAngles = baseLocalEuler;
    }

    private void OnDrawGizmosSelected()                        // ✅ 디버그: 지면 체크 시각화
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            groundCheck.position + Vector3.down * groundCheckDistance,
            new Vector3(groundCheckSize.x, groundCheckSize.y, 0f));
    }
}
