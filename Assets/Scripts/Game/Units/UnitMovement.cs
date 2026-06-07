using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class UnitMovement : NetworkBehaviour
{
    private NavMeshAgent _agent;
    private Unit _unit;
    private UnitData _data;

    private Vector3 _targetLocation;
    private float _updatePathTimer;
    private const float _updatePathDelay = 0.5f;

    private bool _hasMoveTarget;

    private bool _hasCombatLookTarget;
    private Vector3 _combatLookTarget;

    // Ako je angle veći od ovoga, tenk neće ići napred.
    // 36 stepeni znači otprilike "80% okrenut".
    [SerializeField] private float startMovingAngle = 36f;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _unit = GetComponent<Unit>();
        _data = GetComponent<UnitData>();

        _targetLocation = transform.position;

        _agent.updateRotation = false;

        if (_data != null)
        {
            _agent.speed = _data.MaxSpeed;
            _agent.angularSpeed = _data.MaxAngularSpeed;
            _agent.acceleration = _data.MaxAcceleration;
        }

        if (!IsServer)
        {
            _agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerUpdateMovement();
    }

    private void ServerUpdateMovement()
    {
        if (_agent == null || !_agent.enabled)
            return;

        _updatePathTimer += Time.deltaTime;

        if (_hasMoveTarget && _updatePathTimer >= _updatePathDelay)
        {
            _updatePathTimer = 0;
            _agent.SetDestination(_targetLocation);
        }

        Vector3 moveDirection = GetMovementDirection();


        Vector3 lookDirection = Vector3.zero;

        if (_hasCombatLookTarget)
        {
            lookDirection = _combatLookTarget - transform.position;
            lookDirection.y = 0f;
        }
        else
        {
            lookDirection = moveDirection;
        }

        RotateBodyTowards(lookDirection);

        UpdateMovementSpeedBasedOnRotation(moveDirection);

        if (_hasMoveTarget && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
        {
            _hasMoveTarget = false;
            _agent.speed = 0f;
        }
    }

    private Vector3 GetMovementDirection()
    {
        if (!_hasMoveTarget)
            return Vector3.zero;

        Vector3 direction = _agent.steeringTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
            return direction.normalized;

        Vector3 desiredVelocity = _agent.desiredVelocity;
        desiredVelocity.y = 0f;

        if (desiredVelocity.sqrMagnitude > 0.01f)
            return desiredVelocity.normalized;

        return Vector3.zero;
    }

    private void RotateBodyTowards(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        float rotationSpeed = _data != null ? _data.MaxAngularSpeed : 120f;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateMovementSpeedBasedOnRotation(Vector3 moveDirection)
    {
        if (!_hasMoveTarget || moveDirection.sqrMagnitude < 0.01f)
        {
            _agent.speed = 0f;
            return;
        }

        float maxSpeed = _data != null ? _data.MaxSpeed : 5f;

        float angle = Vector3.Angle(transform.forward, moveDirection);

        // Ako nije dovoljno okrenut, ne ide napred.
        if (angle > startMovingAngle)
        {
            _agent.speed = 0f;
            return;
        }


        float rotationFactor = 1f - (angle / startMovingAngle);
        rotationFactor = Mathf.Clamp01(rotationFactor);

        _agent.speed = maxSpeed * rotationFactor;
    }

    public void RequestMove(Vector3 targetPosition)
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();

        if (!_unit.BelongsToLocalPlayer())
            return;

        MoveServerRpc(targetPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoveServerRpc(Vector3 targetPosition, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (_unit.PlayerClientId.Value != senderClientId)
            return;

        _targetLocation = targetPosition;
        _hasMoveTarget = true;


        _hasCombatLookTarget = false;

        if (_agent.enabled)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_targetLocation);
        }
    }

    public void ServerSetCombatLookTarget(Vector3 targetPosition)
    {
        if (!IsServer)
            return;

        _combatLookTarget = targetPosition;
        _hasCombatLookTarget = true;
    }

    public void ServerClearCombatLookTarget()
    {
        if (!IsServer)
            return;

        _hasCombatLookTarget = false;
    }
}