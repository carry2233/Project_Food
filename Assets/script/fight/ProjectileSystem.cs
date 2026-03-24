using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Combat/Projectile System (투사체시스템)")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ProjectileSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("기본 이동 설정")]
    [SerializeField] private float moveSpeed = 5f;                     // ✅ 기본 이동 속도(스포너에서 덮어씀)
    [SerializeField] private int lifeTimeSeconds = 3;                  // ✅ 비활성화까지 남은 시간(초)

    [Header("피해 / 관통 설정")]
    [SerializeField] private int damage = 1;                           // ✅ 적에게 줄 피해량
    [SerializeField] private int basePenetration = 1;                  // ✅ 기본 관통력(적을 맞출 때마다 감소)
    [SerializeField] private bool continuousHit = false;               // ✅ 연속 적중 판정 여부
    [SerializeField, Min(0.01f)]
    private float continuousInterval = 0.3f;                           // ✅ 연속 적중일 때 피해 주기(초)

    [Header("충돌 콜라이더")]
    [SerializeField] private Collider2D hitCollider;                   // ✅ Trigger로 사용할 2D 콜라이더

    [Header("사운드 설정")]
    [SerializeField] private AudioClip hitClip;                        // ✅ 적중 시 재생할 효과음

    [Header("첫 적중 후 속도 변경 설정")]
    [SerializeField] private bool useSpeedChangeOnFirstHit = false;   // ✅ 첫 적중 시 이동 속도 변경 기능 사용 여부
    [SerializeField] private float moveSpeedAfterFirstHit = 8f;       // ✅ 첫 적중 이후에 사용할 이동 속도값

    // 내부 상태
    private int currentPenetration;                                    // ✅ 현재 남은 관통력
    private float lifeTimer;                                           // ✅ 수명 카운트다운 타이머
    private float continuousTimer;                                     // ✅ 연속 적중용 타이머
    private EnemyEntitySystem continuousTarget;                        // ✅ 연속 적중 대상 Enemy
    private bool targetInContact;                                      // ✅ 현재 연속 대상과 겹쳐 있는지 여부
    private readonly HashSet<EnemyEntitySystem> hitOnceEnemies         // ✅ 단발 모드에서 이미 맞춘 적
        = new HashSet<EnemyEntitySystem>();

    private bool hasAppliedFirstHitSpeed = false;                      // ✅ 첫 적중 후 속도 변경이 이미 적용되었는지 여부
    private float initialMoveSpeed = 0f;                               // ✅ 스포너에서 받은 초기 이동 속도 캐싱

    // 소유자(사격 시스템)
    private ShootingSystem owner;                                      // ✅ 나를 발사한 ShootingSystem 참조

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Reset()                                               // ✅ 기본 컴포넌트 자동 할당
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();

        if (hitCollider != null)
            hitCollider.isTrigger = true;
    }

    private void OnEnable()                                            // ✅ 활성화 시 수명/상태 초기화
    {
        lifeTimer = lifeTimeSeconds;
        continuousTimer = 0f;
        targetInContact = false;
        continuousTarget = null;
        hitOnceEnemies.Clear();
        currentPenetration = basePenetration;

        hasAppliedFirstHitSpeed = false;                               // ✅ 첫 적중 속도 변경 플래그 초기화
        // moveSpeed 는 InitializeFromSpawner에서 다시 세팅됨
    }

    private void Update()                                              // ✅ 이동, 수명, 연속 적중 처리
    {
        float dt = Time.deltaTime;

        // 1) 전진 이동
        transform.Translate(Vector3.right * moveSpeed * dt, Space.Self);

        // 2) 수명 감소 및 종료
        lifeTimer -= dt;
        if (lifeTimer <= 0f)
        {
            DeactivateProjectile();
            return;
        }

        // 3) 연속 적중 처리
        if (continuousHit && continuousTarget != null && targetInContact)
        {
            // ❗ 연속 타겟이 죽었거나 비활성화되었으면 즉시 연속판정 해제
            if (!continuousTarget.gameObject.activeInHierarchy || !continuousTarget.IsAlive())
            {
                continuousTarget = null;
                targetInContact = false;
                continuousTimer = 0f;
                return;
            }

            continuousTimer += dt;
            if (continuousTimer >= continuousInterval)
            {
                continuousTimer -= continuousInterval;

                // ✅ 연속 타격 시에도 매 틱마다 히트 이펙트 + 피해 처리
                Vector2 hitPos = (Vector2)continuousTarget.transform.position;
                ApplyDamageAndPenetration(continuousTarget, hitPos);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────────────

    public void InitializeFromSpawner(float speed, int lifeSeconds, ShootingSystem ownerSystem) // ✅ 스포너에서 초기값 세팅
    {
        moveSpeed = speed;                                             // ✅ 발사 시점 기본 속도
        initialMoveSpeed = speed;                                      // ✅ 재사용 대비 초기 속도 캐싱

        lifeTimeSeconds = Mathf.Max(1, lifeSeconds);
        lifeTimer = lifeTimeSeconds;

        currentPenetration = basePenetration;
        continuousTimer = 0f;
        continuousTarget = null;
        targetInContact = false;
        hitOnceEnemies.Clear();

        hasAppliedFirstHitSpeed = false;                               // ✅ 첫 적중 속도 변경 플래그 리셋

        owner = ownerSystem;                                           // ✅ 나를 쏜 ShootingSystem 저장
    }

    private void OnTriggerEnter2D(Collider2D other)                    // ✅ 첫 충돌 처리
    {
        EnemyEntitySystem enemy = other.GetComponentInParent<EnemyEntitySystem>();
        if (enemy == null)
            return;

        // ❗ 이미 죽은 적(체력 0) 또는 비활성화된 적은 피격 판정 자체를 하지 않음
        if (!enemy.gameObject.activeInHierarchy || !enemy.IsAlive())
            return;

        if (!continuousHit)
        {
            if (hitOnceEnemies.Contains(enemy))
                return;

            hitOnceEnemies.Add(enemy);
        }
        else
        {
            continuousTarget = enemy;
            targetInContact = true;
            continuousTimer = 0f;
        }

        // ✅ 충돌 지점 계산
        Vector2 hitPos = other.ClosestPoint(transform.position);

        // ✅ 첫 적중 시에도 "피해 처리 함수"에서 이펙트까지 한 번에 처리
        ApplyDamageAndPenetration(enemy, hitPos);
    }

    private void OnTriggerExit2D(Collider2D other)                     // ✅ 연속 타겟이 범위를 벗어나면 처리
    {
        if (!continuousHit)
            return;

        EnemyEntitySystem enemy = other.GetComponentInParent<EnemyEntitySystem>();
        if (enemy == null)
            return;

        if (enemy == continuousTarget)
        {
            targetInContact = false;
            continuousTarget = null;
        }
    }

    private void ApplyDamageAndPenetration(EnemyEntitySystem enemy, Vector2 hitPos)    // ✅ 피해/관통력 + 히트 이펙트 처리
    {
        // ❗ 이미 죽었거나 비활성화된 적이면, 이펙트/사운드/피해 모두 처리하지 않음
        if (enemy == null || !enemy.gameObject.activeInHierarchy || !enemy.IsAlive())
            return;

        if (currentPenetration <= 0)
            return;

        // ✅ 첫 적중 시 속도 변경 옵션이 켜져 있으면, 이 타이밍에 이동 속도 변경
        if (useSpeedChangeOnFirstHit && !hasAppliedFirstHitSpeed)
        {
            moveSpeed = moveSpeedAfterFirstHit;                        // ✅ 이후부터는 이 속도로 이동
            hasAppliedFirstHitSpeed = true;
        }

        // ✅ 피격 이펙트 생성 (Projectile을 쏜 ShootingSystem에 위임)
        if (owner != null)
        {
            owner.SpawnHitEffect(hitPos);
        }

        // 적중 시 효과음 재생
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlaySfx(hitClip);                      // ✅ 투사체 적중 효과음
        }

        // ✅ 피해 적용
        enemy.ApplyDamage(damage);

        // ✅ 저지력 기반 관통력 감소
        int stop = enemy.GetStoppingPower();
        currentPenetration -= stop;

        if (!continuousHit)
        {
            hitOnceEnemies.Add(enemy);                                 // 단발 모드에서 재적중 방지
        }

        if (currentPenetration <= 0)
        {
            DeactivateProjectile();                                    // ✅ 관통력 소진 시 비활성화
        }
    }

    private void DeactivateProjectile()                                // 💥 투사체 비활성화 처리
    {
        gameObject.SetActive(false);
        continuousTarget = null;
        targetInContact = false;
        continuousTimer = 0f;

        hasAppliedFirstHitSpeed = false;                               // ✅ 비활성화 시 플래그 정리
        moveSpeed = initialMoveSpeed;                                  // ✅ 혹시 모를 다음 사용 대비 안전하게 원래 속도로 복귀
    }
}
