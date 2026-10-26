using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// End 키       : 선택한 오브젝트를 바로 아래 바닥에 붙입니다.
/// Shift + End  : 씬의 모든 오브젝트를 바닥에 붙입니다.
/// 바닥에는 Collider가 있어야 합니다.
/// </summary>
public static class SnapToGround
{
    private const float MAX_DISTANCE = 1000f;
    private const string UNDO_NAME = "Snap To Ground";

    // "_END" = End 키
    [MenuItem("Tools/Snap To Ground/Selected _END")]
    private static void SnapSelected()
    {
        Physics.SyncTransforms();

        foreach (Transform target in Selection.transforms)
        {
            Snap(target);
        }
    }

    [MenuItem("Tools/Snap To Ground/Selected _END", true)]
    private static bool ValidateSnapSelected()
    {
        return Selection.transforms.Length > 0;
    }

    // "#END" = Shift + End 키
    [MenuItem("Tools/Snap To Ground/All Objects In Scene #END")]
    private static void SnapAll()
    {
        Physics.SyncTransforms();

        Scene scene = SceneManager.GetActiveScene();
        int count = 0;

        // 최상위 오브젝트만 옮김 (자식은 부모를 따라 같이 움직임)
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (Snap(root.transform))
            {
                count++;
            }
        }

        Debug.Log($"[SnapToGround] {count}개 오브젝트를 바닥에 붙였습니다.");
    }

    private static bool Snap(Transform target)
    {
        // 카메라, 빈 매니저 오브젝트처럼 크기가 없는 오브젝트는 건너뜀
        if (!TryGetBounds(target, out Bounds bounds))
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(bounds.center, Vector3.down, MAX_DISTANCE);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // 자기 자신(자식 포함)의 콜라이더는 무시
            if (hit.transform.IsChildOf(target)) continue;

            Undo.RecordObject(target, UNDO_NAME);
            float offset = hit.point.y - bounds.min.y;
            target.position += Vector3.up * offset;
            return true;
        }

        // 아래에 바닥이 없으면 (바닥 자체 등) 움직이지 않음
        return false;
    }

    // 오브젝트의 실제 크기(가장 아래 지점)를 구함
    private static bool TryGetBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds) return true;

        foreach (Collider collider in target.GetComponentsInChildren<Collider>())
        {
            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }
}
