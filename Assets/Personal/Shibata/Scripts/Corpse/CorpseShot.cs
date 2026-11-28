using DG.Tweening;
using UnityEngine;

public class CorpseShot : MonoBehaviour
{
    [Header("時間")]
    [SerializeField,Tooltip("ショット時の威力")]
    private float _shotSpeed = 2;
    private GridHelper _gridHelper;

    public void Shot(Transform player)
    {
        Vector3Int pos;
        pos = _gridHelper.GetCellAhead(player.position,player.right);
        transform.DOMove(pos, _shotSpeed);

        Vector3 dir = (pos - transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        player.transform.rotation = Quaternion.Euler(0, 0, angle);
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        _gridHelper = GetComponent<GridHelper>();
    }
}