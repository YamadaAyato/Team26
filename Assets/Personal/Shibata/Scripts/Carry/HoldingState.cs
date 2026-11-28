using DG.Tweening;
using UnityEngine;

/// <summary>
/// 死体の吸収
/// </summary>
public class HoldingState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        //isHoldingをture
        carrier.ChangeHolding(true);

        //プレイヤーが死体の座標に移動
        Vector3 targetPos = carrier.HoldCourpse.transform.position;

        //死体の方に向く
        Vector2 dir = targetPos - carrier.transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        carrier.transform.rotation = Quaternion.Euler(0, 0, angle);

        //移動入力の受付拒否
        carrier.IsMoving = false;
        carrier.transform.DOMove(targetPos, 0.5f).OnComplete(Complete);
        carrier.transform.DOScale(Vector3.one * 2, 0.5f);

        //プレイヤーのScaleを２×２にした時の処理
        void Complete()
        {
            carrier.IsMoving = true;
            carrier.HoldCourpse.SetActive(false);
            carrier.ChangeState(carrier.EmptyState);
        }
    }
}
