using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    [Tooltip("追従するターゲット（ロボット）")]
    public Transform target;

    [Tooltip("ターゲットの真上、どのくらいの高さにカメラを設置するか")]
    public float height = 10f;

    [Tooltip("追従の滑らかさ。0に近いほど速く、1に近いほどゆっくり追従します。")]
    [Range(0.01f, 1.0f)]
    public float smoothSpeed = 0.05f;

    // Update処理の最後に呼び出される関数
　　void FixedUpdate() // ← ここを LateUpdate から FixedUpdate に変更
    {
    // ターゲットが設定されていなければ、何もしない
    	if (target == null)
    	{
    	    // Consoleへの警告はUpdateで十分なので、FixedUpdateからは削除してもOK
    	    return;
    	}

    	// --- 1. カメラの目標位置を計算 ---
    	Vector3 desiredPosition = new Vector3(target.position.x, target.position.y + height, 	target.position.z);
  	// --- 2. カメラを滑らかに移動させる ---
  	Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, 	smoothSpeed);
	transform.position = smoothedPosition;

        // --- 3. カメラの向きを常に真下に固定する ---
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
