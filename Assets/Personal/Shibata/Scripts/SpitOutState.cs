using DG.Tweening;
using UnityEngine;

/// <summary>
/// 吐き出し
/// </summary>
public class SpitOutState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        carrier.ChangePickUp(false);
        carrier.IsMoving = false;
        carrier.transform.DOScale(Vector3.one, 1f).OnComplete(() => carrier.IsMoving = true);
    }
}
