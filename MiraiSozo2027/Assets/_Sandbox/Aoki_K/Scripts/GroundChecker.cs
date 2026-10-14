using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    [SerializeField] private LayerMask _groundLayer;

    public bool IsGrounded { get; private set;}
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {
        if((_groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            IsGrounded = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if((_groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            IsGrounded = false;
        }
    }
}
