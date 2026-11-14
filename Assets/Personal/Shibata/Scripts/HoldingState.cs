using DG.Tweening;
using UnityEngine;

/// <summary>
/// 死体の吸収
/// </summary>
public class HoldingState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        //プレイヤーが死体の座標に移動
        var targetPos = carrier.HoldCourpse.transform.position;
        carrier.transform.DOMove(targetPos, 0.5f);

        //死体の方に向く
        float angle = Mathf.Atan2(targetPos.y, targetPos.x) * Mathf.Rad2Deg;
        carrier.transform.rotation = Quaternion.Euler(0, 0, angle);
        carrier.ChangePickUp(true);

        //移動入力の受付拒否
        carrier.IsMoving = false;
        carrier.transform.DOScale(Vector3.one * 2, 1f).OnComplete(Complete);

        //プレイヤーのScaleを２×２にした時の処理
        void Complete()
        {
            carrier.IsMoving = true;
            carrier.HoldCourpse.SetActive(false);
        }
    }
}
