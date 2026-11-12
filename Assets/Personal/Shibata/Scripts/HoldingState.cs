using DG.Tweening;

/// <summary>
/// 死体の吸収
/// </summary>
public class HoldingState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        carrier.ChangePickUp(true);
        carrier.IsMoving = false;
        carrier.transform.DOScale(UnityEngine.Vector3.one * 2, 1f).OnComplete(() => carrier.IsMoving = true);
    }
}
