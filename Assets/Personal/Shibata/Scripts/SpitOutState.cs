using DG.Tweening;
using UnityEngine;

/// <summary>
/// 吐き出し
/// </summary>
public class SpitOutState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        //プレイヤーの入力の受付を拒否
        var corpse = carrier.HoldCourpse;
        carrier.IsMoving = false;
        carrier.ChangePickUp(false);

        //死体をプレイヤーの座標に移動
        corpse.transform.rotation = carrier.transform.rotation;
        corpse.transform.position = carrier.transform.position;
        carrier.SetHoldCorpse(corpse);
        carrier.HoldCourpse.SetActive(true);

        //死体の吐き出し処理
        carrier.CorpseShot.Shot();
        carrier.transform.DOScale(Vector3.one, 1f).OnComplete(() => carrier.IsMoving = true);

        //何もない状態にする
        carrier.ResetState();
    }
}
