using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Combat/Shooting System (사격시스템)")]
[DisallowMultipleComponent]
public class ShootingSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("입력 설정")]
    [SerializeField] private KeyCode fireKey = KeyCode.N;              // ✅ 발사 키(기본: N)

    [Header("발사 위치 / 프리팹 / 풀")]
    [SerializeField] private Transform spawnPoint;                      // ✅ 투사체 생성 기준 위치
    [SerializeField] private ProjectileSystem projectilePrefab;         // ✅ 투사체 프리팹(ProjectileSystem 포함)
    [SerializeField] private Transform poolParent;                      // ✅ 투사체 풀링용 부모 Transform

    [Header("투사체 파라미터")]
    [SerializeField] private float projectileSpeed = 5f;                // ✅ 투사체 이동 속도
    [SerializeField] private float projectileDistance = 10f;            // ✅ 투사체 사거리

    [Header("주시 방향 설정")]
    [SerializeField] private Vector2 initialLookDirection = Vector2.right; // ✅ 초기 주시 방향(WASD 입력 없을 때 기준)
    [SerializeField] private Vector2 currentLookDirection = Vector2.right; // ✅ 현재 주시 방향(발사 방향에 사용)

    [Header("히트 이펙트 설정")]
    [SerializeField] private GameObject hitEffectPrefab;                // ✅ 피격 시 생성할 이펙트 프리팹
    [SerializeField] private Transform hitEffectPoolParent;             // ✅ 히트 이펙트 풀링용 부모 Transform
    [SerializeField, Min(0.01f)]
    private float hitEffectLifeTime = 0.5f;                             // ✅ 이펙트가 유지될 시간(초)

    [Header("참조")]
    [SerializeField] private PlayerBasicMovement player;                // ✅ 플레이어 바라보는 방향 참조

    [Header("사운드 설정")]
    [SerializeField] private AudioClip shotClip;                        // ✅ 사격 효과음 클립

    [Header("행동 가능 여부")]
    [SerializeField] private bool canAct = true;                        // ✅ 사격/에임 조작이 가능한지 여부

    [Header("기력 시스템")]
    [SerializeField] private StaminaSystem staminaSystem;               // ✅ 기력 시스템(StaminaSystem) 참조

    [Header("사격 쿨타임 설정")]
    [SerializeField] private bool useShotCooldown = true;              // ✅ 사격 쿨타임을 사용할지 여부
    [SerializeField, Min(0f)]
    private float shotCooldown = 0.25f;                                // ✅ 한 번 발사 후 다시 발사 가능해지기까지 시간(초)
    [SerializeField] private bool isOnShotCooldown = false;            // ✅ 현재 사격 쿨타임 진행 중인지 여부(디버그용 표시)
    [SerializeField] private float shotCooldownTimer = 0f;             // ✅ 사격 쿨타임 경과 시간

    // 내부 상태(풀)
    private readonly List<ProjectileSystem> projectilePool              // ✅ 투사체 풀 리스트
        = new List<ProjectileSystem>();
    private readonly List<GameObject> hitEffectPool                     // ✅ 히트 이펙트 풀 리스트
        = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Reset()                                                // ✅ 기본 레퍼런스 자동 할당
    {
        if (spawnPoint == null)
            spawnPoint = transform;                                     // ✅ 기본적으로 자기 Transform 사용

        if (poolParent == null && spawnPoint != null)
            poolParent = spawnPoint;                                    // ✅ 기본 투사체 부모

        if (hitEffectPoolParent == null)
            hitEffectPoolParent = transform;                            // ✅ 기본 이펙트 부모

        if (initialLookDirection.sqrMagnitude < 0.0001f)
            initialLookDirection = Vector2.right;                       // ✅ 초기 방향 0이면 오른쪽으로 보정

        currentLookDirection = initialLookDirection.normalized;         // ✅ 현재 방향도 같이 초기화

        if (player == null)
            player = GetComponentInParent<PlayerBasicMovement>();       // ✅ 기본값: 부모에서 PlayerBasicMovement 찾기

        if (staminaSystem == null)
            staminaSystem = GetComponentInParent<StaminaSystem>();      // ✅ 부모에서 StaminaSystem 자동 검색
    }

    private void Start()                                                // ✅ 시작 시 주시 방향 초기화
    {
        if (initialLookDirection.sqrMagnitude < 0.0001f)
            initialLookDirection = Vector2.right;

        currentLookDirection = initialLookDirection.normalized;
    }

    private void Update()                                               // ✅ 입력 처리(방향 갱신 + 발사 + 쿨타임)
    {
        // 행동 불가 상태면 입력/발사 로직 전체를 막음
        if (!canAct)
            return;

        float dt = Time.deltaTime;                                      // ✅ 프레임 경과 시간

        // 0) 사격 쿨타임 진행
        if (useShotCooldown && isOnShotCooldown)
        {
            shotCooldownTimer += dt;

            if (shotCooldownTimer >= shotCooldown)
            {
                isOnShotCooldown = false;                               // ✅ 쿨타임 종료
            }
        }

        // 1) WASD 입력으로 "현재 주시 방향" 갱신 (키를 떼도 마지막 방향 유지)
        Vector2 dirInput = ReadWASDDirection();                         // ✅ 현재 눌린 WASD 방향 벡터
        if (dirInput.sqrMagnitude > 0.0001f)
        {
            int facing = 0;
            if (player != null)
                facing = player.GetFacingDir();                        // ✅ 플레이어 좌우 바라봄

            float x = dirInput.x;
            float y = dirInput.y;

            // 바라보는 방향에 따라 좌/우 조준 제한
            if (facing > 0 && x < 0f) x = 0f;                         // ▶ 오른쪽 보고 있을 때 왼쪽 성분 제거
            else if (facing < 0 && x > 0f) x = 0f;                    // ◀ 왼쪽 보고 있을 때 오른쪽 성분 제거

            dirInput = new Vector2(x, y);

            // 위/아래 혹은 허용된 방향만 남았으면 그 방향으로 주시 방향 갱신
            if (dirInput.sqrMagnitude > 0.0001f)
            {
                currentLookDirection = dirInput.normalized;           // ✅ 입력 있을 때만 방향 갱신
            }
        }

        // 2) 발사 키 입력 처리
        if (Input.GetKeyDown(fireKey))
        {
            // 쿨타임 사용 중이고, 아직 쿨타임이면 발사 불가
            if (useShotCooldown && isOnShotCooldown)
            {
                return;                                               // ✅ 쿨타임 중에는 입력 무시
            }

            // 현재 방향이 0이라면 안전하게 오른쪽으로 보정
            if (currentLookDirection.sqrMagnitude < 0.0001f)
            {
                currentLookDirection = Vector2.right;
            }

            // ✅ 기력 체크: 사격에 필요한 기력이 없으면 발사하지 않음
            if (staminaSystem != null && !staminaSystem.TryConsumeForShot())
            {
                return;
            }

            // ✅ 실제 발사 시도
            bool fired = FireProjectile(currentLookDirection.normalized); // ✅ 주시 방향 기준 발사

            // 발사가 성공했을 때만 쿨타임 시작
            if (fired && useShotCooldown)
            {
                isOnShotCooldown = true;                             // ✅ 쿨타임 시작
                shotCooldownTimer = 0f;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────────────

    private Vector2 ReadWASDDirection()                                // ✅ WASD 입력을 2D 방향 벡터로 변환
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;

        return new Vector2(x, y);                                      // ✅ 대각선 포함 방향 벡터
    }

    private bool FireProjectile(Vector2 dir)                            // ✅ 주어진 방향으로 투사체 발사(성공 여부 반환)
    {
        if (projectilePrefab == null || spawnPoint == null)
            return false;

        // 1) 풀에서 비활성화된 투사체 가져오기
        ProjectileSystem proj = GetProjectileFromPool();                // ✅ 풀에서 재사용 or 새 생성
        if (proj == null)
            return false;

        // 2) 위치/회전/부모 설정
        proj.transform.SetParent(poolParent, worldPositionStays: false);
        proj.transform.position = spawnPoint.position;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;        // ✅ 방향 → z 회전각
        proj.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // 3) 수명 타이머 계산 (사거리 ÷ 속도, 반올림 후 최소 1초)
        float rawLifeTime = (projectileSpeed <= 0.01f)
            ? 0f
            : (projectileDistance / projectileSpeed);

        int lifeTimeSeconds = Mathf.Max(1, Mathf.RoundToInt(rawLifeTime));

        // 4) 투사체 초기화 및 활성화 (owner로 자기 자신 전달)
        proj.gameObject.SetActive(true);
        proj.InitializeFromSpawner(projectileSpeed, lifeTimeSeconds, this); // ✅ 스피드 + 수명 + 소유자 전달

        // 5) 사운드 재생
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlaySfx(shotClip);                    // ✅ 사격 효과음 재생
        }

        return true;                                                  // ✅ 발사 성공
    }

    private ProjectileSystem GetProjectileFromPool()                    // ✅ 투사체 풀에서 하나 가져오기
    {
        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (projectilePool[i] != null && !projectilePool[i].gameObject.activeSelf)
            {
                return projectilePool[i];
            }
        }

        // 없으면 새로 생성
        if (projectilePrefab == null || poolParent == null)
            return null;

        ProjectileSystem newProj = Instantiate(projectilePrefab, poolParent);
        projectilePool.Add(newProj);
        newProj.gameObject.SetActive(false);
        return newProj;
    }

    public void SpawnHitEffect(Vector2 position)                        // ✅ 피격 위치에 히트 이펙트 생성(풀링)
    {
        if (hitEffectPrefab == null || hitEffectPoolParent == null)
            return;

        // 1) 풀에서 비활성화된 이펙트 찾기
        GameObject effect = null;
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

    public void SetCanAct(bool value)                                  // ✅ 외부(기력시스템)에서 사격 가능 여부 설정
    {
        canAct = value;
    }
}
