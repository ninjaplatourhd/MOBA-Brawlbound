using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class UnitMovement : NetworkBehaviour
{
    private NavMeshAgent _agent;
    private Unit _unit;

    private Vector3 _targetLocation;
    private float _updatePathTimer;
    private const float _updatePathDelay = 0.5f;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _unit = GetComponent<Unit>();
        _targetLocation = transform.position;

        // Only server should actually move the unit.
        // Clients receive position through NetworkTransform.
        if (!IsServer)
        {
            _agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        _updatePathTimer += Time.deltaTime;

        if (_updatePathTimer >= _updatePathDelay)
        {
            _updatePathTimer = 0;

            if (_agent.enabled)
            {
                _agent.SetDestination(_targetLocation);
            }
        }
    }

    public void RequestMove(Vector3 targetPosition)
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();

        // This is the important part.
        // Do NOT use IsOwner here because the server owns all RTS units.
        if (!_unit.BelongsToLocalPlayer())
            return;

        MoveServerRpc(targetPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoveServerRpc(Vector3 targetPosition, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // Server validates that this player is allowed to command this unit.
        if (_unit.PlayerClientId.Value != senderClientId)
            return;

        _targetLocation = targetPosition;

        if (_agent.enabled)
        {
            _agent.SetDestination(_targetLocation);
        }
    }
}