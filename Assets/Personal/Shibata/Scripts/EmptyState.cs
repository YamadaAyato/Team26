/// <summary>
/// なにも持っていない状態の処理
/// </summary>
public class EmptyState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        if (carrier.IsPickUp)
        {
            carrier.ChangeState(carrier.HoldingState);
        }
    }
}
