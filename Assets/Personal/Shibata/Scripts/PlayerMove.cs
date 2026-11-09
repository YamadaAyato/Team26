using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerMove : MonoBehaviour
{
    public Vector2 MoveVec { get => _moveVec; }
    private float _moveSpeed = 3f;
    private Vector2 _moveVec;
    private Quaternion _lastRotation;
    private bool _isMoving = false;

    public void Move(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _isMoving = false;
            _moveVec = Vector2.zero;
            return;
        }

        _moveVec = context.ReadValue<Vector2>().normalized;
        _isMoving = true;
    }

    private void Update()
    {
        MoveUpdata();
    }

    private void MoveUpdata()
    {
        if (_isMoving)
        {
            Vector3 move = new Vector3(_moveVec.x, _moveVec.y, 0);
            transform.Translate(move * _moveSpeed * Time.deltaTime, Space.World);

            float angle = Mathf.Atan2(_moveVec.y, _moveVec.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            _lastRotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            transform.rotation = _lastRotation;
        }
    }
}
