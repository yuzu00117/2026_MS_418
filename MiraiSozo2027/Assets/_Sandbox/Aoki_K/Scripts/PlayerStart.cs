using UnityEngine;

public class PlayerStart : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1f,
                            new Vector3(0.6f, 2f, 0.6f));
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 1f, transform.forward * 1.5f);
    }
}
