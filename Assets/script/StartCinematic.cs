using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Cinematic/Start Cinematic (카메라+UI 페이드)")]
[DisallowMultipleComponent]
public class StartCinematic : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("대상 할당")]
    [SerializeField] private Camera targetCamera;                      // 🎥 연출 대상 카메라
    [SerializeField] private Image overlayImage;                       // 🖼️ 화면을 덮는 UI 이미지(알파 페이드)

    [Header("타임/강도 설정")]
    [SerializeField, Min(0f)] private float delay = 0f;               // ⏱️ 시작 지연(초)
    [SerializeField, Min(0.01f)] private float duration = 1.5f;       // ⏱️ 연출 시간(초)

    [Header("카메라 값(최소→최대)")]
    [SerializeField, Min(0.01f)] private float cameraMin = 4f;        // 🔎 최소 값(OrthoSize 또는 FOV)
    [SerializeField, Min(0.01f)] private float cameraMax = 7f;        // 🔍 최대 값(OrthoSize 또는 FOV)

    [Header("커브(0→1 구간)")]
    [SerializeField] private AnimationCurve fadeCurve =                // 📈 알파 1→0 곡선
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve zoomCurve =                // 📈 줌 0→1 곡선
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Reset() // 🔧 컴포넌트 추가 시 기본 참조/곡선 셋업
    {
        if (targetCamera == null) targetCamera = Camera.main;                  // 메인 카메라 자동 할당
        if (overlayImage == null)
        {
            // 씬에 Canvas/Image가 이미 있다면 수동 할당을 권장합니다.
            // 여기서는 자동 생성은 하지 않습니다(프로젝트 구조마다 달라서).
        }

        // 기본 EaseInOut으로 세팅(이미 직렬화 기본값 있지만 명시적 재설정)
        fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);                  // 알파 1→0
        zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);                  // 줌 0→1
    }

    private void OnValidate() // 🧹 인스펙터 값 안전 보정
    {
        if (duration < 0.01f) duration = 0.01f;
        if (cameraMin < 0.01f) cameraMin = 0.01f;
        if (cameraMax < 0.01f) cameraMax = 0.01f;
    }

    private void Start() // ▶️ 씬 시작 시 연출 실행
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (overlayImage != null)
        {
            // 시작 시 알파를 1(=255)로 보장
            SetImageAlpha01(1f);
        }
        StartCoroutine(Co_Play());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────────────

    private System.Collections.IEnumerator Co_Play() // ▶️ 연출 코루틴: 지연→커브 보간
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 시작 상태 강제 세팅
        float startTime = Time.time;
        ApplyCameraValue(0f);                 // 줌 t=0
        if (overlayImage != null) SetImageAlpha01(1f); // 알파=1

        // 0→1 구간으로 시간 정규화
        while (true)
        {
            float t = Mathf.InverseLerp(0f, duration, Time.time - startTime);
            if (t >= 1f) t = 1f;

            // 커브 적용
            float fade01 = Mathf.Clamp01(fadeCurve.Evaluate(t)); // 1→0 형태 기대
            float zoom01 = Mathf.Clamp01(zoomCurve.Evaluate(t)); // 0→1 형태 기대

            // 이미지 알파(255→0 == 1→0)
            if (overlayImage != null) SetImageAlpha01(fade01);

            // 카메라(최소→최대)
            ApplyCameraValue(zoom01);

            if (t >= 1f) break;
            yield return null;
        }
    }

    private void ApplyCameraValue(float norm01) // 🎥 카메라 값을 커브 결과에 맞춰 적용
    {
        if (targetCamera == null) return;

        float value = Mathf.Lerp(cameraMin, cameraMax, norm01);

        if (targetCamera.orthographic)
        {
            // 2D 카메라(Orthographic Size)
            targetCamera.orthographicSize = value;
        }
        else
        {
            // 3D 카메라(FOV)
            targetCamera.fieldOfView = value;
        }
    }

    private void SetImageAlpha01(float a01) // 🖼️ UI 이미지 알파를 0~1 범위로 설정
    {
        a01 = Mathf.Clamp01(a01);
        Color c = overlayImage.color;
        c.a = a01;
        overlayImage.color = c;
    }
}
