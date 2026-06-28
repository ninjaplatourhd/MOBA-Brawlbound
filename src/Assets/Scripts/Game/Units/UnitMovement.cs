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


    private bool _isPlayerMoveCommand;
    public bool IsExecutingPlayerMoveCommand => _isPlayerMoveCommand && _hasMoveTarget;

    private bool _hasCombatLookTarget;
    private Vector3 _combatLookTarget;

    [SerializeField] private float startMovingAngle = 36f;

    private bool _hasPatrol;
    private Vector3 _patrolPointA;
    private Vector3 _patrolPointB;
    private bool _goingToB;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _unit = GetComponent<Unit>();
        _data = GetComponent<UnitData>();

        _targetLocation = transform.position;

        if (_agent != null)
        {
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
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerUpdateMovement();
    }

    private void ServerUpdateMovement()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return;

        _updatePathTimer += Time.deltaTime;

        if (_hasMoveTarget && _updatePathTimer >= _updatePathDelay)
        {
            _updatePathTimer = 0f;
            _agent.SetDestination(_targetLocation);
        }

        Vector3 moveDirection = GetMovementDirection();

        Vector3 lookDirection;

        if (_hasCombatLookTarget && !_isPlayerMoveCommand)
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
            _isPlayerMoveCommand = false;
            _agent.speed = 0f;
        }

        HandlePatrolServer();
    }

    private void HandlePatrolServer()
    {
        if (!_hasPatrol)
            return;

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return;

        if (_agent.pathPending)
            return;

        if (_agent.remainingDistance > _agent.stoppingDistance + 0.2f)
            return;

        if (_goingToB)
        {
            _goingToB = false;
            _targetLocation = _patrolPointA;
        }
        else
        {
            _goingToB = true;
            _targetLocation = _patrolPointB;
        }

        _hasMoveTarget = true;
        _isPlayerMoveCommand = false;

        _agent.isStopped = false;
        _agent.SetDestination(_targetLocation);
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

        if (_unit == null)
            return;

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

        UnitCombat combat = GetComponent<UnitCombat>();
        if (combat != null)
        {
            combat.ServerClearGuardMode();
            combat.ServerClearAttackTarget();
        }

        _targetLocation = targetPosition;
        _hasMoveTarget = true;
        _isPlayerMoveCommand = true;

        _hasPatrol = false;
        _hasCombatLookTarget = false;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_targetLocation);
        }
    }

    public void ServerSetCombatLookTarget(Vector3 targetPosition)
    {
        if (!IsServer)
            return;

        if (_isPlayerMoveCommand)
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

    public void ServerClearPlayerMoveCommand()
    {
        if (!IsServer)
            return;

        _isPlayerMoveCommand = false;
    }

    public void RequestPatrol(Vector3 pointA, Vector3 pointB)
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();

        if (_unit == null)
            return;

        if (!_unit.BelongsToLocalPlayer())
            return;

        PatrolServerRpc(pointA, pointB);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PatrolServerRpc(Vector3 pointA, Vector3 pointB, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (_unit.PlayerClientId.Value != senderClientId)
            return;

        UnitCombat combat = GetComponent<UnitCombat>();
        if (combat != null)
        {
            combat.ServerClearGuardMode();
            combat.ServerClearAttackTarget();
        }

        _patrolPointA = pointA;
        _patrolPointB = pointB;
        _goingToB = true;
        _hasPatrol = true;

        _targetLocation = pointB;
        _hasMoveTarget = true;
        _isPlayerMoveCommand = false;

        _hasCombatLookTarget = false;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_targetLocation);
        }
    }

    public void ServerClearPatrol()
    {
        if (!IsServer)
            return;

        _hasPatrol = false;
        _goingToB = false;

        _patrolPointA = Vector3.zero;
        _patrolPointB = Vector3.zero;
    }

    public void RequestStop()
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();

        if (_unit == null)
            return;

        if (!_unit.BelongsToLocalPlayer())
            return;

        StopServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StopServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (_unit.PlayerClientId.Value != senderClientId)
            return;

        _hasMoveTarget = false;
        _isPlayerMoveCommand = false;
        _targetLocation = transform.position;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.speed = 0f;
        }

        UnitCombat combat = GetComponent<UnitCombat>();
        if (combat != null)
        {
            combat.ServerClearGuardMode();
            combat.ServerClearAttackTarget();
        }

        _hasCombatLookTarget = false;

        ServerClearPatrol();
    }

    public void ServerMoveToAttackRange(Vector3 targetPosition, float desiredRange)
    {
        if (!IsServer)
            return;

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return;

        _isPlayerMoveCommand = false;

        Vector3 fromTargetToUnit = transform.position - targetPosition;
        fromTargetToUnit.y = 0f;

        if (fromTargetToUnit.sqrMagnitude < 0.01f)
            fromTargetToUnit = -transform.forward;

        Vector3 chasePosition = targetPosition + fromTargetToUnit.normalized * desiredRange;

        _targetLocation = chasePosition;
        _hasMoveTarget = true;

        _hasPatrol = false;

        _agent.isStopped = false;
        _agent.stoppingDistance = 0.2f;

        if (!_agent.hasPath || Vector3.Distance(_agent.destination, chasePosition) > 1f)
        {
            _agent.SetDestination(chasePosition);
        }
    }

    public void ServerStopMovementOnly()
    {
        if (!IsServer)
            return;

        _hasMoveTarget = false;
        _isPlayerMoveCommand = false;
        _targetLocation = transform.position;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.speed = 0f;
        }
    }

    public void ServerMoveToGatherRange(Vector3 targetPosition, float desiredRange)
    {
        if (!IsServer)
            return;

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return;

        _isPlayerMoveCommand = false;

        Vector3 fromTargetToUnit = transform.position - targetPosition;
        fromTargetToUnit.y = 0f;

        if (fromTargetToUnit.sqrMagnitude < 0.01f)
            fromTargetToUnit = -transform.forward;

        Vector3 gatherPosition = targetPosition + fromTargetToUnit.normalized * desiredRange;

        if (NavMesh.SamplePosition(gatherPosition, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            gatherPosition = hit.position;
        }

        _targetLocation = gatherPosition;
        _hasMoveTarget = true;

        _hasPatrol = false;
        _hasCombatLookTarget = false;

        _agent.isStopped = false;
        _agent.stoppingDistance = 0.2f;

        if (!_agent.hasPath || Vector3.Distance(_agent.destination, gatherPosition) > 1f)
        {
            _agent.SetDestination(gatherPosition);
        }
    }
}