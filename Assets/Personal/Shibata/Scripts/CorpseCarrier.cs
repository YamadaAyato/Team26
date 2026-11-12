using UnityEngine;
using UnityEngine.InputSystem;

public class CorpseCarrier : MonoBehaviour
{
    public ICarryState EmptyState { get; } = new EmptyState();
    public ICarryState HoldingState { get; } = new HoldingState();
    public ICarryState SpitOutState { get; } = new SpitOutState();
    public bool IsMoving = true;
    public bool IsPickUp => _isPickUp;
    private bool _isPickUp = false;

    private ICarryState _current;

    private void Awake()
    {
        ResetState();
    }

    /// <summary>
    /// PlayerInputで決めたキーを押したら呼ばれる
    /// </summary>
    /// <param name="context">入力アクションのコールバック情報</param>
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (_isPickUp)
        {
            ChangeState(SpitOutState);
        }

        _current.OnInteract(this);
    }

    /// <summary>
    /// 状態の切替
    /// </summary>
    /// <param name="next">次の状態</param>
    public void ChangeState(ICarryState next)
    {
        _current?.OnExit(this);
        _current = next;
        _current.OnEnter(this);
    }

    /// <summary>
    /// 何も持っていない状態に戻す
    /// </summary>
    public void ResetState() => ChangeState(EmptyState);

    public void ChangePickUp(bool pickUp) => _isPickUp = pickUp;
}
