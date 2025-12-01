using UnityEngine;
using UnityEditor;

public class RemoveComponentsTool
{
    // メニューに「Tools/Remove All MeshColliders」という項目を追加
    [MenuItem("Tools/Remove All MeshColliders")]
    private static void RemoveAllMeshColliders()
    {
        // 現在選択しているGameObjectを取得
        GameObject selectedObject = Selection.activeGameObject;

        // オブジェクトが選択されていなかったらエラー表示
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("エラー", "オブジェクトが選択されていません。", "OK");
            return;
        }

        // 選択したオブジェクトから全てのMeshColliderを取得
        MeshCollider[] colliders = selectedObject.GetComponents<MeshCollider>();
        
        if (colliders.Length == 0)
        {
            EditorUtility.DisplayDialog("情報", "選択されたオブジェクトにMeshColliderは見つかりませんでした。", "OK");
            return;
        }

        // 確認ダイアログを表示
        if (EditorUtility.DisplayDialog("確認",
            $"本当に {selectedObject.name} から {colliders.Length}個のMeshColliderを削除しますか？",
            "はい、削除します", "いいえ"))
        {
            // 全てのMeshColliderをループして削除
            foreach (var collider in colliders)
            {
                // エディタスクリプトではDestroyImmediateを使うのがお作法
                Object.DestroyImmediate(collider);
            }
            Debug.Log($"{colliders.Length}個のMeshColliderを{selectedObject.name}から削除しました。");
        }
    }
}
