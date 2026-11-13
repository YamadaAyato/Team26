using DG.Tweening;
using UnityEngine;

/// <summary>
/// 吐き出し
/// </summary>
public class SpitOutState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        var corpse = carrier.HoldCourpse;
        carrier.ChangePickUp(false);
        carrier.IsMoving = false;
        corpse.transform.rotation = carrier.transform.rotation;
        corpse.transform.position = carrier.transform.position;
        carrier.SetHoldCorpse(corpse);
        carrier.HoldCourpse.SetActive(true);
        carrier._courpseShot.Shot();
        carrier.transform.DOScale(Vector3.one, 1f).OnComplete(() => carrier.IsMoving = true);
    }
}
