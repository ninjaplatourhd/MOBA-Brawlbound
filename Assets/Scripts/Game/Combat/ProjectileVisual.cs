using UnityEngine;

public class ProjectileVisual : MonoBehaviour
{
    private int projectileId;
    private Vector3 direction;
    private float speed;

    private bool initialized;

    public void Initialize(int id, Vector3 moveDirection, float projectileSpeed)
    {
        projectileId = id;
        direction = moveDirection.normalized;
        speed = projectileSpeed;

        initialized = true;

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        transform.position += direction * speed * Time.deltaTime;
    }

    public void Impact(Vector3 impactPosition)
    {
        transform.position = impactPosition;

        // Kasnije ovde možeš spawnovati mali hit particle.
        Destroy(gameObject);
    }
}