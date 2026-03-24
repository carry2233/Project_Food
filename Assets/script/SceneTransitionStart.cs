using UnityEngine;
using UnityEngine.UI;                     // ✅ UI Button, Image 사용
using UnityEngine.SceneManagement;        // ✅ 씬 전환용

[AddComponentMenu("UI/Scene Transition Start (씬전환시작)")]
[DisallowMultipleComponent]
public class SceneTransitionStart : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────

    [Header("버튼 / 씬 설정")]
    [SerializeField] private Button targetButton;              // ✅ 클릭을 감지할 버튼
    [SerializeField] private string sceneName;                 // ✅ 전환할 씬 이름 (Build Settings 에 등록 필요)
    [SerializeField, Min(0.01f)]
    private float transitionDuration = 1.5f;                   // ✅ 버튼 클릭 후 연출이 진행되는 총 시간(초)

    [Header("카메라 이동/크기 설정")]
    [SerializeField] private Camera targetCamera;              // ✅ 연출에 사용할 카메라
    [SerializeField] private float targetCameraSize = 3f;      // ✅ 연출 후 최종 카메라 크기(OrthoSize 또는 FOV)
    [SerializeField] private AnimationCurve cameraSizeCurve    // ✅ 카메라 크기 변화용 커브(0~1 → 0~1)
        = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("UI 이미지 페이드 설정")]
    [SerializeField] private Image overlayImage;               // ✅ 알파를 올릴 UI 이미지
    [SerializeField, Range(0f, 1f)]
    private float startAlpha = 0f;                             // ✅ 시작 알파 값
    [SerializeField, Range(0f, 1f)]
    private float endAlpha = 1f;                               // ✅ 마지막 알파 값
    [SerializeField] private AnimationCurve imageAlphaCurve    // ✅ 이미지 알파 변화용 커브(0~1 → 0~1)
        = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("BGM 볼륨 페이드 설정")]
    [SerializeField] private AudioSource bgmSource;            // ✅ 루프로 재생 중인 BGM AudioSource
    [SerializeField] private AnimationCurve bgmVolumeCurve     // ✅ BGM 볼륨 변화용 커브(0~1 → 0~1)
        = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // 내부 상태
    private bool isTransitionRunning = false;                  // ✅ 현재 전환 연출이 실행 중인지 여부

    // ─────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────

    private void Reset()                                       // ✅ 기본 참조 자동 할당
    {
        if (targetButton == null)
            targetButton = GetComponent<Button>();             // 같은 오브젝트에 Button 이 있으면 자동 할당

        if (targetCamera == null)
            targetCamera = Camera.main;                        // 기본 메인 카메라 참조

        // BGM, overlayImage 는 씬 구성이 다양해서 수동 할당을 권장 (필요하면 직접 드래그)
    }

    private void Awake()                                       // ✅ 버튼 클릭 리스너 바인딩
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OnButtonClicked);
            targetButton.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            Debug.LogWarning("[SceneTransitionStart] targetButton 이 비어 있습니다.", this);
        }
    }

    private void OnDestroy()                                   // ✅ 리스너 정리
    {
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OnButtonClicked);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────

    private void OnButtonClicked()                             // ✅ 버튼 클릭 시 호출되는 핸들러
    {
        if (isTransitionRunning)
            return;                                            // 이미 진행 중이면 중복 실행 방지

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneTransitionStart] sceneName 이 비어 있습니다.", this);
            return;
        }

        if (targetCamera == null)
        {
            Debug.LogWarning("[SceneTransitionStart] targetCamera 가 설정되지 않았습니다.", this);
            return;
        }

        // BGM, 이미지, 카메라 등이 없어도 연출은 가능한 부분만 진행하도록 코루틴에서 처리
        StartCoroutine(Co_TransitionAndLoad());
    }

    private System.Collections.IEnumerator Co_TransitionAndLoad() // ✅ 연출 진행 + 씬 전환 코루틴
    {
        isTransitionRunning = true;

        // 1) 시작 시점 값들 저장
        Vector3 camStartPos = targetCamera.transform.position;
        float camStartSize = targetCamera.orthographic
            ? targetCamera.orthographicSize
            : targetCamera.fieldOfView;

        // 버튼 위치(월드 공간 Canvas 라면 그대로 사용 가능)
        Vector3 camEndPos = camStartPos;
        if (targetButton != null)
        {
            camEndPos = targetButton.transform.position;
            camEndPos.z = camStartPos.z; // 카메라 z 는 유지 (2D, 3D 둘 다 안전용)
        }

        float startCamSize = camStartSize;
        float endCamSize = targetCameraSize;

        float imageStartA = startAlpha;
        float imageEndA = endAlpha;

        float bgmStartVolume = 1f;
        if (bgmSource != null)
        {
            bgmStartVolume = bgmSource.volume;
            // 루프 재생이 꺼져 있으면 자동으로 켜줄 수도 있음
            bgmSource.loop = true;
        }

        float elapsed = 0f;

        // 2) transitionDuration 동안 카메라/이미지/BGM 값 보간
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            t = Mathf.Clamp01(t);

            // 카메라 위치 (선형 보간)
            Vector3 camPos = Vector3.Lerp(camStartPos, camEndPos, t);
            targetCamera.transform.position = camPos;

            // 카메라 크기 (커브 기반 보간)
            float sizeCurve = cameraSizeCurve != null ? cameraSizeCurve.Evaluate(t) : t;
            float camSize = Mathf.Lerp(startCamSize, endCamSize, sizeCurve);

            if (targetCamera.orthographic)
                targetCamera.orthographicSize = camSize;
            else
                targetCamera.fieldOfView = camSize;

            // UI 이미지 알파 (커브 기반)
            if (overlayImage != null)
            {
                float alphaCurve = imageAlphaCurve != null ? imageAlphaCurve.Evaluate(t) : t;
                float a = Mathf.Lerp(imageStartA, imageEndA, alphaCurve);

                Color c = overlayImage.color;
                c.a = a;
                overlayImage.color = c;
            }

            // BGM 볼륨 (커브 기반)
            if (bgmSource != null)
            {
                float volCurve = bgmVolumeCurve != null ? bgmVolumeCurve.Evaluate(t) : (1f - t);
                float v = Mathf.Clamp01(bgmStartVolume * volCurve);
                bgmSource.volume = v;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3) 마지막 프레임에서 값 정리 (t=1 보장)
        // 카메라 위치/크기
        targetCamera.transform.position = new Vector3(camEndPos.x, camEndPos.y, camStartPos.z);

        float finalSizeCurve = cameraSizeCurve != null ? cameraSizeCurve.Evaluate(1f) : 1f;
        float finalCamSize = Mathf.Lerp(startCamSize, endCamSize, finalSizeCurve);
        if (targetCamera.orthographic)
            targetCamera.orthographicSize = finalCamSize;
        else
            targetCamera.fieldOfView = finalCamSize;

        // 이미지 알파
        if (overlayImage != null)
        {
            float finalAlphaCurve = imageAlphaCurve != null ? imageAlphaCurve.Evaluate(1f) : 1f;
            float finalA = Mathf.Lerp(imageStartA, imageEndA, finalAlphaCurve);

            Color c = overlayImage.color;
            c.a = finalA;
            overlayImage.color = c;
        }

        // BGM 볼륨
        if (bgmSource != null)
        {
            float finalVolCurve = bgmVolumeCurve != null ? bgmVolumeCurve.Evaluate(1f) : 0f;
            float finalV = Mathf.Clamp01(bgmStartVolume * finalVolCurve);
            bgmSource.volume = finalV;
        }

        // 4) 전환 종료 후 씬 로드
        SceneManager.LoadScene(sceneName);

        isTransitionRunning = false;
    }
}
