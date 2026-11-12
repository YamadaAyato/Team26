using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerMove : MonoBehaviour
{
    public Vector2 MoveVec => _moveVec;

    [Header("移動速度")]
    [SerializeField,Tooltip("プレイヤーの秒速")]
    private float _moveSpeed = 3f;
    private const float _initialAngle = -90f;
    private CorpseCarrier _corpeCarrier;
    private Vector2 _moveVec;

    /// <summary>
    /// PlayerInputから移動方向を取得
    /// </summary>
    /// <param name="context">入力アクションのコールバック情報</param>
    public void Move(InputAction.CallbackContext context)
    {
        if (context.canceled || !_corpeCarrier.IsMoving)
        {
            _moveVec = Vector2.zero;
            return;
        }
        _moveVec = context.ReadValue<Vector2>().normalized;
    }

    /// <summary>
    /// 初期方向を設定
    /// </summary>
    private void Awake()
    {
        _corpeCarrier = GetComponent<CorpseCarrier>();
        GetComponent<SpriteRenderer>().sortingOrder = 10;
        transform.rotation = Quaternion.Euler(0, 0, _initialAngle);
    }

    private void Update()
    {
        MoveUpdate();
    }

    /// <summary>
    /// プレイヤーの移動と向きの計算、止まった時には最後移動した方向を向くようにしました。
    /// </summary>
    private void MoveUpdate()
    {
        if (_moveVec == Vector2.zero)
        {
            return;
        }

        Vector3 move = new Vector3(_moveVec.x, _moveVec.y, 0);
        transform.Translate(move * _moveSpeed * Time.deltaTime, Space.World);

        float angle = Mathf.Atan2(_moveVec.y, _moveVec.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Corpse"))
        {
            _corpeCarrier.ChangeState(_corpeCarrier.HoldingState);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Corpse"))
        {
            _corpeCarrier.ResetState();
        }
    }
}
