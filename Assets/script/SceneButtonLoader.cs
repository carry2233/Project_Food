using UnityEngine;
using UnityEngine.UI;                    // Button 사용
using UnityEngine.SceneManagement;       // 씬 로드

[AddComponentMenu("UI/Scene Button Loader")] 
public class SceneButtonLoader : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Button targetButton;   // 클릭을 감지할 버튼

    [Header("설정")]
    [SerializeField] private string sceneName;      // 이동할 씬 이름(빌드 세팅에 포함되어야 함)
    [SerializeField] private bool additive = false; // 추가 로드 여부(false=싱글 로드)

    [Header("안전 옵션")]
    [SerializeField] private bool disableOnClick = true; // 클릭 직후 중복 클릭 방지(버튼 비활성)

    private void Reset() // 인스펙터 기본값 자동 셋업 시도
    {
        // 같은 게임오브젝트에 Button이 있으면 자동 할당
        if (!targetButton) targetButton = GetComponent<Button>();
    }

    private void Awake() // 버튼 리스너 바인딩
    {
        if (!targetButton)
        {
            // 같은 게임오브젝트에서 시도 (없으면 경고)
            targetButton = GetComponent<Button>();
            if (!targetButton)
            {
                Debug.LogWarning("[SceneButtonLoader] targetButton이 비어있습니다.");
                return;
            }
        }

        targetButton.onClick.RemoveListener(OnClickLoadScene);
        targetButton.onClick.AddListener(OnClickLoadScene);
    }

    private void OnDestroy() // 리스너 정리
    {
        if (targetButton)
            targetButton.onClick.RemoveListener(OnClickLoadScene);
    }

    private void OnClickLoadScene() // 버튼 클릭 핸들러 → 씬 로드
    {
        if (disableOnClick && targetButton) targetButton.interactable = false;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneButtonLoader] sceneName이 비어있습니다.");
            if (disableOnClick && targetButton) targetButton.interactable = true;
            return;
        }

        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogWarning($"[SceneButtonLoader] '{sceneName}' 씬이 Build Settings에 포함되어 있지 않습니다.");
            if (disableOnClick && targetButton) targetButton.interactable = true;
            return;
        }

        var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        SceneManager.LoadScene(sceneName, mode);
    }

    private bool IsSceneInBuildSettings(string name) // 빌드 세팅에 씬이 있는지 확인
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file == name) return true;
        }
        return false;
    }
}
