using DG.Tweening;
using UnityEngine;

/// <summary>
/// 吐き出し
/// </summary>
public class SpitOutState : ICarryState
{
    public void OnInteract(CorpseCarrier carrier)
    {
        carrier.ChangeHolding(false);
        //プレイヤーの入力の受付を拒否
        var corpse = carrier.HoldCourpse;
        var playerTra = carrier.transform;
        carrier.IsMoving = false;

        //死体をプレイヤーの座標に移動
        corpse.transform.rotation = playerTra.rotation;
        corpse.transform.position = playerTra.position;
        carrier.HoldCourpse.SetActive(true);

        //死体の吐き出し処理
        carrier.CorpseShot.Shot(playerTra);
        carrier.transform.DOScale(Vector3.one, 0.5f).OnComplete(() => carrier.IsMoving = true);

        //何もない状態にする
        carrier.ResetState();
    }
}
