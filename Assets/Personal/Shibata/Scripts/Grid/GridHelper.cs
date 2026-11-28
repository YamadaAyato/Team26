using UnityEngine;

public class GridHelper : MonoBehaviour
{
    [SerializeField, Header("GridコンポーネントがあるPrefabの親オブジェクト")]
    private GameObject _gridParent;
    [SerializeField, Header("飛距離"), Tooltip("マス単位")]
    private int _distance;
    private Grid _grid;

    private void Awake()
    {
        _grid = _gridParent.GetComponentInChildren<Grid>();
    }

    /// <summary>
    /// プレイヤーから２マス先の座標取得
    /// </summary>
    /// <param name="pos">基準となる座標</param>
    /// <param name="dir">方向</param>
    /// <returns>飛距離分先の座標</returns>
    public Vector3Int GetCellAhead(Vector3 pos, Vector3 dir)
    {
        Vector3Int dirInt = Vector3Int.RoundToInt(dir);
        return _grid.WorldToCell(pos) + dirInt * _distance;
    }
}
