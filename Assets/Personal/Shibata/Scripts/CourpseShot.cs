using UnityEngine;

public class CourpseShot : MonoBehaviour
{
    [Header("威力")]
    [SerializeField,Tooltip("ショット時の威力")]
    private float _shotSpeed = 2;
    private Rigidbody2D _rb;

    public void Shot()
    {
        _rb.AddForce(transform.right * _shotSpeed, ForceMode2D.Impulse);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
    }
}
