using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public float movementSpeed = 5f;
    public float rotationSpeed = 10f;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private Camera mainCamera;

    void Start()
    {
        if (!IsOwner)
            return;

        mainCamera = Camera.main;
        targetPosition = transform.position; // Start at the current position
    }

    void Update()
    {
        if (!IsOwner)
            return;

        if (Input.GetMouseButtonDown(1)) // Left mouse button
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                targetPosition = hit.point; // Set new target position
                isMoving = true;
            }
        }

        if (isMoving)
        {
            // Move towards the target position
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, movementSpeed * Time.deltaTime);

            Vector3 direction = (targetPosition - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }


            // Stop moving when we reach the target position
            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                isMoving = false;
            }
        }
    }
}
