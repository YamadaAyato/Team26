using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    private float _moveSpeed = 3f;
    private Vector2 _moveVec;

    public void Move(InputAction.CallbackContext context)
    {
        _moveVec = context.ReadValue<Vector2>() * _moveSpeed * Time.deltaTime;
    }

    private void Update()
    {
        transform.Translate(_moveVec.x,_moveVec.y,0);
    }
}
