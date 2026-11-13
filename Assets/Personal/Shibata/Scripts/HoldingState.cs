using DG.Tweening;
using UnityEngine;

/// <summary>
/// 死体の吸収
/// </summary>
public class HoldingState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        var targetPos = carrier.HoldCourpse.transform.position;
        carrier.transform.DOMove(targetPos, 0.5f);

        float angle = Mathf.Atan2(targetPos.y, targetPos.x) * Mathf.Rad2Deg;
        carrier.transform.rotation = Quaternion.Euler(0, 0, angle);
        carrier.ChangePickUp(true);
        carrier.IsMoving = false;
        carrier.transform.DOScale(Vector3.one * 2, 1f).OnComplete(Complete);

        void Complete()
        {
            carrier.IsMoving = true;
            carrier.HoldCourpse.SetActive(false);
        }
    }
}
