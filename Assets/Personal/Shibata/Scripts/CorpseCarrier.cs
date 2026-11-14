using UnityEngine;
using UnityEngine.InputSystem;

public class CorpseCarrier : MonoBehaviour
{
    public ICarryState EmptyState => _enmptyState;
    public ICarryState HoldingState => _holdingState;
    public ICarryState SpitOutState => _spitOutState;

    private ICarryState _enmptyState = new EmptyState();
    private ICarryState _holdingState = new HoldingState();
    private ICarryState _spitOutState = new SpitOutState();
    public bool IsMoving = true;
    public bool IsPickUp => _isPickUp;
    public bool IsHolding => _isHolding;
    public GameObject HoldCourpse { get; private set; }
    public CorpseShot CorpseShot{ get; private set; }

    /// <summary>
    /// 死体の当たり判定に当たっているか
    /// </summary>
    private bool _isPickUp = false;

    /// <summary>
    /// 死体を保持しているか
    /// </summary>
    private bool _isHolding = false;
    private ICarryState _current;

    private void Awake()
    {
        ResetState();
    }

    public void SetCourpseShot(GameObject obj) => CorpseShot = obj.GetComponent<CorpseShot>();

    //保持している死体を保存
    public void SetHoldCorpse(GameObject corpse) => HoldCourpse = corpse;

    /// <summary>
    /// PlayerInputで決めたキーを押したら呼ばれる
    /// </summary>
    /// <param name="context">入力アクションのコールバック情報</param>
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (_isPickUp)
        {
            ChangeState(_holdingState);
            _isHolding = true;
            _isPickUp = false;
        }
        
        if (_isHolding)
        {
            ChangeState(_spitOutState);
            _isHolding = false;
        }

        _current.OnInteract(this);
    }

    /// <summary>
    /// 状態の切替
    /// </summary>
    /// <param name="next">次の状態</param>
    public void ChangeState(ICarryState next)
    {
        _current = next;
    }

    /// <summary>
    /// 何も持っていない状態に戻す
    /// </summary>
    public void ResetState() => ChangeState(EmptyState);

    public void ChangePickUp(bool pickUp) => _isPickUp = pickUp;
}