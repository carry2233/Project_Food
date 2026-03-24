using UnityEngine;
using System.Collections;              // ✅ 코루틴(IEnumerator) 사용
using System.Collections.Generic;

[AddComponentMenu("Combat/Flurry Attack System (난타공격시스템)")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FlurryAttackSystem : MonoBehaviour
{
    [System.Serializable]
    public class TargetEntry
    {
        public SpriteRenderer renderer;          // ✅ 난타 시 바뀔 스프라이트 렌더러
        public FlipbookAnimation flipbook;       // ✅ 사용할 플립북 애니메이션 에셋
        public bool randomizeFrames;             // ✅ 프레임을 랜덤 재생할지 여부

        [HideInInspector] public int frameIndex; // ✅ 현재 프레임 인덱스
        [HideInInspector] public float timer;    // ✅ 프레임 경과 시간 누적
    }

    // ─────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────

    [Header("입력 설정")]
    [SerializeField] private KeyCode holdKey = KeyCode.M;            // ✅ 난타 공격에 사용할 키(누르고 있는 동안만 공격)

    [Header("기본 전환 주기(애니메이션)")]
    [SerializeField, Min(0.001f)]
    private float defaultInterval = 0.08f;                           // ✅ 애니메이션 프레임 기본 전환 주기

    [Header("대상 목록(렌더러 + 플립북 에셋)")]
    [SerializeField] private List<TargetEntry> targets               // ✅ 난타 시 켜지는 이펙트 렌더러 목록
        = new List<TargetEntry>();

    [Header("공격 설정")]
    [SerializeField] private Collider2D attackCollider;              // ✅ 난타 범위용 Trigger Collider2D
    [SerializeField] private int damagePerTick = 1;                  // ✅ 틱당 줄 피해량
    [SerializeField, Min(0.01f)]
    private float tickInterval = 0.25f;                              // ✅ 난타 공격 주기(초)
    [SerializeField]
    private bool requireHoldToDamage = true;                         // ✅ 키를 계속 누르고 있는 동안만 공격할지 여부

    [Header("쿨타임 설정")]
    [SerializeField, Min(0f)]
    private float cooldownDuration = 0.8f;                           // ✅ 키에서 손 뗀 후 다시 발동까지의 쿨타임

    [Header("사운드 설정")]
    [SerializeField] private AudioClip flurryLoopClip;               // ✅ 난타 상태일 때 주기적으로 재생할 효과음
    [SerializeField, Min(0.01f)]
    private float flurryLoopInterval = 0.3f;                         // ✅ 루프 효과음 주기
    [SerializeField] private AudioClip flurryHitClip;                // ✅ 피해를 줄 때마다 재생할 타격음

    [Header("히트 이펙트 설정")]
    [SerializeField] private GameObject hitEffectPrefab;             // ✅ 난타 타격 시 생성할 히트 이펙트 프리팹
    [SerializeField] private Transform hitEffectPoolParent;          // ✅ 히트 이펙트 풀 부모 Transform
    [SerializeField, Min(0.01f)]
    private float hitEffectLifeTime = 0.5f;                          // ✅ 히트 이펙트 유지 시간(초)

    [Header("행동 가능 여부")]
    [SerializeField] private bool canAct = true;                     // ✅ 난타 공격을 할 수 있는지 여부(기력 등)

    [Header("기력 시스템")]
    [SerializeField] private StaminaSystem staminaSystem;            // ✅ 기력 시스템(StaminaSystem) 참조

    // 내부 상태
    private bool isFlurryActive = false;                             // ✅ 현재 난타 공격이 활성화 상태인지
    private bool isOnCooldown = false;                               // ✅ 쿨타임 진행 중인지
    private float attackTimer = 0f;                                  // ✅ 공격 주기용 타이머
    private float cooldownTimer = 0f;                                // ✅ 쿨타임 타이머
    private float flurryLoopTimer = 0f;                              // ✅ 루프 SFX 주기 타이머

    private readonly HashSet<EnemyEntitySystem> enemiesInRange       // ✅ 범위 안에 들어온 Enemy 목록
        = new HashSet<EnemyEntitySystem>();

    private readonly List<GameObject> hitEffectPool                  // ✅ 난타 히트 이펙트 풀 리스트
        = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────

    private void Reset()                                            // ✅ 기본 컴포넌트 자동 할당
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (attackCollider == null)
            attackCollider = col;

        if (hitEffectPoolParent == null)
            hitEffectPoolParent = transform;                        // ✅ 기본 부모를 자기 자신으로 설정

        if (staminaSystem == null)
            staminaSystem = GetComponentInParent<StaminaSystem>();  // ✅ 기본으로 부모에서 기력시스템 찾기
    }

    private void Start()                                            // ✅ 초기 상태 정리(렌더러 비활성 등)
    {
        if (attackCollider != null)
            attackCollider.enabled = false;

        InitializeTargets();                                        // ✅ 타겟들 초기화(첫 프레임, 렌더러 끄기)
    }

    private void Update()                                           // ✅ 입력, 쿨타임, 공격, 애니메이션 처리
    {
        float dt = Time.deltaTime;                                  // ✅ 프레임 경과 시간

        // 1) 쿨타임 진행
        if (isOnCooldown)
        {
            cooldownTimer += dt;
            if (cooldownTimer >= cooldownDuration)
            {
                isOnCooldown = false;
            }
        }

        // 2) 입력(행동 가능 여부를 고려)
        bool held = canAct && Input.GetKey(holdKey);                // ✅ 행동 불가 상태면 입력도 무시

        bool canAttackNow = held && !isOnCooldown;                  // ✅ 지금 난타 발동 가능한 상태인지

        // 3) 난타 상태 전환(키 입력 + 쿨타임 반영)
        if (canAttackNow)
        {
            if (!isFlurryActive)
            {
                StartFlurry();                                      // ✅ 처음 눌렀을 때 난타 시작 + 즉시 1틱 피해
            }
        }
        else
        {
            if (isFlurryActive)
            {
                StopFlurryAndStartCooldown();                       // ✅ 키에서 손 떼면 난타 종료 + 쿨타임 시작
            }
        }

        // 4) 난타 중일 때 기력 소모 + 공격 주기/루프 사운드 처리
        if (isFlurryActive)
        {
            // ✅ 기력 소모: 초당 flurryCostPerSecond만큼 소모, 부족하면 즉시 종료
            if (staminaSystem != null && !staminaSystem.TryConsumeForFlurry(dt))
            {
                StopFlurryAndStartCooldown();                       // 기력 부족 → 난타 종료 + 쿨타임
                // 기력으로 끊긴 프레임에서는 이 아래 로직(공격/사운드/애니메이션)을 실행하지 않음
                UpdateTargetAnimations(false, dt);
                return;
            }

            attackTimer += dt;
            if (attackTimer >= tickInterval)
            {
                attackTimer -= tickInterval;
                if (!requireHoldToDamage || held)
                {
                    ApplyDamageToAllTargets();                      // ✅ 주기마다 범위 내 모든 적에게 피해
                }
            }

            flurryLoopTimer += dt;
            if (flurryLoopTimer >= flurryLoopInterval)
            {
                flurryLoopTimer -= flurryLoopInterval;
                if (SfxManager.Instance != null)
                {
                    SfxManager.Instance.PlaySfx(flurryLoopClip);    // ✅ 난타 상태 루프 효과음
                }
            }
        }

        // 5) 애니메이션 업데이트(발동 가능할 때만 보여주기)
        UpdateTargetAnimations(canAttackNow, dt);
    }

    private void OnTriggerEnter2D(Collider2D other)                 // ✅ 범위에 EnemyEntitySystem 들어왔을 때
    {
        EnemyEntitySystem enemy = other.GetComponentInParent<EnemyEntitySystem>();
        if (enemy == null) return;

        enemiesInRange.Add(enemy);

        // 이미 난타 중이면, 범위에 들어온 순간 즉시 1회 피해
        if (isFlurryActive)
        {
            ApplyDamageToSingleTarget(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D other)                  // ✅ 범위에서 EnemyEntitySystem 나갔을 때
    {
        EnemyEntitySystem enemy = other.GetComponentInParent<EnemyEntitySystem>();
        if (enemy == null) return;

        if (enemiesInRange.Contains(enemy))
            enemiesInRange.Remove(enemy);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────

    public void SetCanAct(bool value)                               // ✅ 외부에서 행동 가능 여부 설정(기력 고갈 등)
    {
        canAct = value;

        // 행동 불가가 되는 순간 난타 중이면 강제로 종료
        if (!canAct && isFlurryActive)
        {
            StopFlurryAndStartCooldown();
        }
    }

    private void StartFlurry()                                      // ✅ 난타 시작 처리(콜라이더/타이머 초기화+즉시 피해)
    {
        // 이미 쿨타임이거나 행동 불가면 시작하지 않음
        if (!canAct || isOnCooldown)
            return;

        isFlurryActive = true;
        attackTimer = 0f;
        flurryLoopTimer = 0f;

        if (attackCollider != null)
            attackCollider.enabled = true;

        // 키 누르고 있는 시점에 이미 범위 안에 있는 적들에게 즉시 1틱 피해
        ApplyDamageToAllTargets();
    }

    private void StopFlurryAndStartCooldown()                       // ✅ 난타 종료 + 쿨타임 시작
    {
        isFlurryActive = false;
        isOnCooldown = true;
        cooldownTimer = 0f;

        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    private void ApplyDamageToAllTargets()                          // ✅ 범위 내 모든 EnemyEntitySystem에 피해 적용
    {
        foreach (var enemy in enemiesInRange)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
                continue;

            // ❗ 체력 0(죽은) Enemy는 스킵
            if (!enemy.IsAlive())
                continue;

            ApplyDamageToSingleTarget(enemy);
        }
    }

    private void ApplyDamageToSingleTarget(EnemyEntitySystem enemy) // ✅ 단일 EnemyEntitySystem에 피해 + 타격음 + 이펙트
    {
        if (enemy == null)
            return;

        // ❗ 죽었거나 비활성화된 Enemy는 피격 판정/이펙트/사운드 모두 무시
        if (!enemy.gameObject.activeInHierarchy || !enemy.IsAlive())
            return;

        // ✅ 히트 위치 계산 (Enemy 콜라이더 기준으로 가장 가까운 점 사용 시도)
        Vector2 hitPos;
        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (enemyCol != null)
        {
            hitPos = enemyCol.ClosestPoint(transform.position);
        }
        else
        {
            hitPos = enemy.transform.position;
        }

        // ✅ 히트 이펙트 생성
        SpawnHitEffect(hitPos);

        // ✅ 피해 적용
        enemy.ApplyDamage(damagePerTick);

        // ✅ 타격음 재생
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlaySfx(flurryHitClip);             // ✅ 타격 시 효과음(겹쳐지는 것 허용)
        }
    }

    private void UpdateTargetAnimations(bool canShow, float dt)     // ✅ 렌더러 표시 여부 + 플립북 프레임 갱신
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || t.renderer == null || t.flipbook == null)
                continue;

            int frameCount = t.flipbook.FrameCount();
            if (frameCount <= 0)
            {
                t.renderer.enabled = false;
                continue;
            }

            if (!canShow)
            {
                t.renderer.enabled = false;
                continue;
            }
            else
            {
                t.renderer.enabled = true;
            }

            // 현재 프레임 정보 가져오기
            if (!t.flipbook.TryGetFrame(t.frameIndex, out var currentFrame))
                continue;

            float interval = (currentFrame.overrideInterval && currentFrame.customInterval > 0f)
                ? currentFrame.customInterval
                : defaultInterval;

            t.timer += dt;

            if (t.timer >= interval)
            {
                t.timer -= interval;

                if (!t.randomizeFrames)
                {
                    // 순차 모드: 0→1→2→...(frameCount-1)→0 반복
                    t.frameIndex = (t.frameIndex + 1) % frameCount;
                }
                else
                {
                    // 랜덤 모드: 직전 프레임과 다른 인덱스 뽑기
                    int newIndex = Random.Range(0, frameCount);
                    if (newIndex == t.frameIndex)
                    {
                        newIndex = (newIndex + 1) % frameCount;
                    }
                    t.frameIndex = newIndex;
                }

                if (t.flipbook.TryGetFrame(t.frameIndex, out var nextFrame))
                {
                    if (nextFrame.sprite != null)
                    {
                        t.renderer.sprite = nextFrame.sprite;
                    }
                }
            }
        }
    }

    public void InitializeTargets()                                 // ✅ TargetEntry 초기화(첫 프레임/렌더러 비활성)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null) continue;

            t.frameIndex = 0;
            t.timer = 0f;

            if (t.renderer != null && t.flipbook != null &&
                t.flipbook.TryGetFrame(0, out var f) && f.sprite != null)
            {
                t.renderer.sprite = f.sprite;
            }

            if (t.renderer != null)
                t.renderer.enabled = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 히트 이펙트 풀링 메서드
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnHitEffect(Vector2 position)                    // ✅ 난타 히트 이펙트 생성 및 풀링
    {
        if (hitEffectPrefab == null || hitEffectPoolParent == null)
            return;

        GameObject effect = null;

        // 1) 풀에서 비활성화된 이펙트 찾기
        for (int i = 0; i < hitEffectPool.Count; i++)
        {
            if (hitEffectPool[i] != null && !hitEffectPool[i].activeSelf)
            {
                effect = hitEffectPool[i];
                break;
            }
        }

        // 2) 없으면 새로 생성
        if (effect == null)
        {
            effect = Instantiate(hitEffectPrefab, hitEffectPoolParent);
            hitEffectPool.Add(effect);
            effect.SetActive(false);
        }

        // 3) 위치/활성화/수명 코루틴
        effect.transform.position = position;
        effect.SetActive(true);
        StartCoroutine(Co_DisableHitEffectAfter(effect, hitEffectLifeTime));
    }

    private IEnumerator Co_DisableHitEffectAfter(GameObject effect, float lifeTime) // ✅ 일정 시간 후 이펙트 비활성화 코루틴
    {
        yield return new WaitForSeconds(lifeTime);
        if (effect != null)
        {
            effect.SetActive(false);
        }
    }
}
