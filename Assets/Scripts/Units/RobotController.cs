﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Data;
using Model;
using UnityEngine;
using UnityEngine.AI;
using UniRx;

public class RobotController : UnitControllerBase
{
    private Transform _target;
    private Transform _nextTarget;
    [SerializeField] private NavMeshAgent _navAgent;

    public Model.Robot RobotModel;
    public Model.Game Game;

    private ProgramType[] _possiblePrograms;
    private Dictionary<ProgramType, Coroutine> _programCou = new Dictionary<ProgramType, Coroutine>();
    public Animator Animator;

    private string _walkStateName = "walk";
    private string _dragStateName = "drag";
    private string _cutStateName = "cut";

    public Joint Joint;
    public float RotationSpeed;

    private bool _inProgress;
    private ProgramType? _currentProgramType;
    private Program _currentProgram;

    private ProgramType? _nextProgram;

    private float _executeTime;
    private int _nextStep;

    public float SyncInterval = 1f;
    private float _syncTime;
    private bool _hasSyncState;

    public BotSpeaker Speaker;

    public override void Init()
    {
        base.Init();

        _navAgent.updatePosition = false;
        _navAgent.updateRotation = false;
        _target = null;
        _possiblePrograms = new[] { ProgramType.Cut, ProgramType.Gather, ProgramType.Walk };

        RobotModel.Programs.ObserveAdd().Subscribe(addEvent =>
        {
            var newType = addEvent.Value.Template.Type;
            if (newType == ProgramType.Sync)
            {
                if (!_hasSyncState)
                    _hasSyncState = true;
                return;
            }

            if (!_currentProgramType.HasValue)
                return;

            switch (newType)
            {
                case ProgramType.Protect:
                case ProgramType.Gather:
                case ProgramType.Cut:

                    if (_currentProgramType.Value == ProgramType.Walk)
                    {
                        Coroutine cou;
                        if (_inProgress && _programCou.TryGetValue(_currentProgramType.Value, out cou) && cou != null)
                        {

                            StopCoroutine(cou);
                            EndCoProgram();
                        }

                        _currentProgramType = null;
                    }
                    break;
            }
        });

        RobotModel.Programs.ObserveRemove().Subscribe(removeEvent =>
        {
            if (!_currentProgramType.HasValue)
                return;

            if (removeEvent.Value.Template.Type == ProgramType.Sync)
            {
                if (_hasSyncState && !RobotModel.Programs.Any(_ => _.Template.Type == ProgramType.Sync))
                    _hasSyncState = false;
            }
            else
            {
                Coroutine cou;
                if (_inProgress && _programCou.TryGetValue(_currentProgramType.Value, out cou) && cou != null)
                {

                    StopCoroutine(cou);
                    EndCoProgram();
                }
                _currentProgramType = null;
            }
        });
    }

    private void Steer(Vector2 move, bool backwards = false)
    {
        Move(move);
        if(move.sqrMagnitude > .00001f)
        transform.rotation = Quaternion.Lerp(transform.rotation,
            Quaternion.LookRotation(backwards ? new Vector3(-move.x, 0, -move.y) : new Vector3(move.x, 0, move.y), Vector3.up), Time.deltaTime * RotationSpeed);
    }

    private void Update()
    {
        if (RobotModel.Status.Value == Robot.RobotStatus.OutOfMemory || RobotModel.UploadIsRunning.Value)
        {
            Coroutine cou;
            if (_inProgress && _currentProgramType.HasValue && _programCou.TryGetValue(_currentProgramType.Value, out cou) && cou != null)
            {
                StopCoroutine(cou);
                EndCoProgram();

                Animator.SetBool("off", true);
            }

            Animator.SetBool("off", true);
            _currentProgramType = null;
            return;
        }

        if (_hasSyncState && _syncTime + SyncInterval < Time.time)
        {
            // TODO Sync

            _syncTime = Time.time;
        }

        if (_inProgress)
            return;


        _currentProgram = null;

        if (!_currentProgramType.HasValue)
        {
            foreach (var progType in new []{ProgramType.Cut, ProgramType.Gather, ProgramType.Walk})
            {
                _currentProgram = RobotModel.Programs.FirstOrDefault(x => x.Template.Type == progType);
                if (_currentProgram != null)
                {
                    _currentProgramType = progType;
                    break;
                }
            }
        }

        Animator.SetBool("off", !_currentProgramType.HasValue);

        if (_currentProgram == null && _currentProgramType.HasValue)
            _currentProgram = RobotModel.Programs.FirstOrDefault(x => x.Template.Type ==_currentProgramType);


        switch (_currentProgramType)
        {
            case ProgramType.Walk:

                var speed = _currentProgram.GetCurrentVersionIndex() + 1;
                Animator.SetFloat("level_walk", speed);
                _movable.LinearSpeed =
                    speed == 1 ? basicSpeed :
                    speed == 2 ? 0.05f : 0.08f;

                var cou = StartCoroutine(Co_Walk());
                _programCou[ProgramType.Walk] = cou;
                _inProgress = true;

                break;
            case ProgramType.Cut:

                var index = _currentProgram.GetCurrentVersionIndex();

                if (_target == null)
                {
                    var obj = index > 0
                        ? WorldObjects.Instance.GetClosestObject<Tree>(transform.position)
                        : WorldObjects.Instance.GetOneOfClosest<Tree>(transform.position, 3);

                    if (obj != null)
                    {
                        _target = obj.transform;
                    }
                    else
                    {
                        if (RobotModel.Programs.Any(x => x.Template.Type == ProgramType.Walk))
                        {
                            _currentProgramType = ProgramType.Walk;
                            _nextProgram = ProgramType.Cut;
                        }
                        else
                        {
                            StartCoroutine(Co_Wait(0.5f));
                            _inProgress = true;
                        }
                    }

                    return;
                }

                if (Vector3.Distance(_target.position, transform.position) > 5f)
                {
                    if (RobotModel.Programs.Any(x => x.Template.Type == ProgramType.Walk))
                    {
                        _currentProgramType = ProgramType.Walk;
                        _nextProgram = ProgramType.Cut;
                        _nextTarget = _target;
                        return;
                    }
                    else
                    {
                        StartCoroutine(Co_Wait(0.5f));
                        _inProgress = true;
                    }
                }
                else
                {
                    _str = CutStr;
                    _delay = CutDelay;

                    if (index > 1)
                        _delay /= 4f;
                    else if(index >0)
                        _delay /= 1.5f;

                    var couCut = StartCoroutine(Co_Cut(_target.GetComponent<Tree>()));
                    _programCou[ProgramType.Cut] = couCut;
                    _inProgress = true;
                }
                break;
            case ProgramType.Gather:

                var index2 = _currentProgram.GetCurrentVersionIndex();

                if (_target?.GetComponent<TreeTrunk>() == null)
                {
                    var obj = index2 > 0 ?
                        WorldObjects.Instance.GetClosestObject<TreeTrunk>(transform.position)
                        : WorldObjects.Instance.GetOneOfClosest<TreeTrunk>(transform.position, 3);

                    if (obj != null)
                    {
                        _target = obj.transform;
                    }
                    else
                    {
                        if (RobotModel.Programs.Any(x => x.Template.Type == ProgramType.Walk))
                        {
                            _currentProgramType = ProgramType.Walk;
                            _nextProgram = ProgramType.Gather;
                        }
                        else
                        {
                            StartCoroutine(Co_Wait(0.5f));
                            _inProgress = true;
                        }
                    }

                    return;
                }

                if (Vector3.Distance(_target.position, transform.position) > 3f)
                {
                    if (RobotModel.Programs.Any(x => x.Template.Type == ProgramType.Walk))
                    {
                        _nextTarget = _target;
                        _target = _target.GetComponent<TreeTrunk>().InteractionCollider.transform;
                        _currentProgramType = ProgramType.Walk;
                        _nextProgram = ProgramType.Gather;
                        return;
                    }
                    else
                    {
                        StartCoroutine(Co_Wait(0.5f));
                        _inProgress = true;
                    }
                }
                else
                {
                    Animator.SetFloat("level_gather", 1 + index2 * 0.5f);
                    _movable.LinearSpeed = basicSpeed + index2 * basicSpeed / 2f;
                    var couGather = StartCoroutine(Co_Gather(_target.GetComponent<TreeTrunk>()));
                    _programCou[ProgramType.Gather] = couGather;
                    _inProgress = true;
                }

                break;
            case ProgramType.Protect:
                break;
        }
    }

    private IEnumerator Co_Wait(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        EndCoProgram();
    }

    public IEnumerator CO_Spawn(Vector3 targetPosition)
    {
        Speaker.Speak();

        Animator.SetBool(_walkStateName, true);
        Animator.SetBool("off", true);

        _navAgent.nextPosition = transform.position;
        _navAgent.ResetPath();
        _navAgent.SetDestination(targetPosition);

        yield return null;
        ResetTime();

        while (Vector3.Distance(transform.position, targetPosition) > 2
               && _navAgent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            var direction = _navAgent.desiredVelocity.normalized;
            var move = new Vector2(direction.x, direction.z);
            Steer(move);
            _navAgent.velocity = _movable.Velocity;
            _navAgent.nextPosition = transform.position;

            ComputeTime(Time.deltaTime, ProgramType.Walk);

            yield return null;
        }

        Animator.SetBool(_walkStateName, false);
        Speaker.Speak();

    }

    private float basicSpeed = 0.03f;

    private IEnumerator Co_Walk()
    {

        var prog = RobotModel.Programs.FirstOrDefault(_ => _.Template.Type == ProgramType.Walk);

        if (prog == null)
        {
            EndCoProgram();
            yield break;
        }

        Animator.SetBool(_walkStateName, true);

        Speaker.Speak();

        Vector3 targetPosition;
        if (_target == null)
        {
            targetPosition = GetPointOnGround();
        }
        else
        {
            targetPosition = _target.position;
        }

        _navAgent.nextPosition = transform.position;
        _navAgent.ResetPath();
        _navAgent.SetDestination(targetPosition);

        yield return null;
        ResetTime();

        while (Vector3.Distance(transform.position, targetPosition) > 3 && _navAgent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            var direction = _navAgent.desiredVelocity.normalized;
            var move = new Vector2(direction.x, direction.z);
            Steer(move);
            _navAgent.velocity = _movable.Velocity;
            _navAgent.nextPosition = transform.position;

            ComputeTime(Time.deltaTime, ProgramType.Walk);

            yield return null;
        }


        EndCoProgram();
    }

    private Vector3 GetPointOnGround()
    {
        var targetPosition = MainApplication.Instance.PointToChillAround != null ? MainApplication.Instance.PointToChillAround.position : transform.position;
        targetPosition.x += Random.Range(-10, 10);
        targetPosition.z += Random.Range(-10, 10);
        targetPosition.y += 2;

        RaycastHit hit;
        if (Physics.Raycast(targetPosition, Vector3.down, out hit, 5f, LayerMask.GetMask("Ground")))
        {
            return hit.point;
        }

        return transform.position;
    }

    private IEnumerator Co_Cut(Tree tree)
    {

        var prog = RobotModel.Programs.FirstOrDefault(_ => _.Template.Type == ProgramType.Cut);
        if (prog == null)
        {
            EndCoProgram();
            yield break;
        }

        if (tree == null || !tree.IsAlive)
        {
            EndCoProgram();
            yield break;
        }

        Speaker.Speak();


        var direction = tree.transform.position - transform.position;
        direction.y = 0;
        ResetTime();

        while (tree != null && tree.IsAlive)
        {
            Animator.SetBool(_cutStateName, true);

            yield return new WaitForSeconds(CutHitTime);
            tree.Cut(_str, direction);

            Animator.SetBool(_cutStateName, false);
            yield return new WaitForSeconds(_delay);

            ComputeTime(CutHitTime + _delay, ProgramType.Cut);

            Speaker.Speak();
        }

        EndCoProgram();
    }

    private TreeTrunk _trunk;
    [SerializeField] private float CutDelay = 1;
    [SerializeField] private float CutHitTime = .1f;
    [SerializeField] private float CutStr = 10;

    private IEnumerator Co_Gather(TreeTrunk trunk)
    {

        Speaker.Speak();

        _isLoadPointSet = false;
        Animator.SetBool(_dragStateName, true);

        if (!RobotModel.Programs.Any(_ => _.Template.Type == ProgramType.Gather))
        {
            EndCoProgram();
            yield break;
        }

        _movable.LinearSpeed = basicSpeed;

        if (trunk == null || trunk.IsCarring)
        {
            EndCoProgram();
            yield break;
        }

        trunk.IsCarring = true;

        yield return null;

        trunk.Carry(Joint);
        _trunk = trunk;

        var ark = WorldObjects.Instance.GetFirstItem<Ark>();
        _navAgent.nextPosition = transform.position;
        _navAgent.ResetPath();
        _navAgent.SetDestination(ark.transform.position);

        yield return null;
        ResetTime();

        Animator.SetBool(_walkStateName, true);

        while (!trunk.IsRecycling && _navAgent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            var direction = _navAgent.desiredVelocity.normalized;
            Steer(new Vector2(direction.x, direction.z), true);
            _navAgent.velocity = _movable.Velocity;
            _navAgent.nextPosition = transform.position;

            ComputeTime(Time.deltaTime, ProgramType.Gather);

            yield return null;
        }

        EndCoProgram();
    }

    private bool _isLoadPointSet;
    private float _delay;
    private float _str;

    private void OnTriggerEnter(Collider other)
    {
        if (_isLoadPointSet)
            return;
        var ark = other.GetComponentInParent<Ark>();
        if (ark == null)
            return;

        _isLoadPointSet = true;
        var center = other.bounds.center;
        center.y = transform.position.y;
        var resultPoint = transform.position - 1.5f * (transform.position - center);
        _navAgent.SetDestination(resultPoint);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(_navAgent.destination, 0.1f);
    }

    private void EndCoProgram()
    {
        Speaker.Speak();

        Animator.SetBool(_walkStateName, false);
        Animator.SetBool(_cutStateName, false);
        Animator.SetBool(_dragStateName, false);
        _inProgress = false;
        _target = null;

        if (_trunk != null)
        {
            if(!_trunk.IsRecycling)
                _trunk.Drop();

            _trunk = null;
        }
        Joint.connectedBody = null;

        if (_nextProgram.HasValue)
        {
            _currentProgramType = _nextProgram;
            if (_nextTarget != null)
            {
                _target = _nextTarget;
                _nextTarget = null;
            }
        }
        else
            SelectNextProgram();

        _nextProgram = null;
    }

    private void SelectNextProgram()
    {

        Speaker.Speak();


        var oldProgram = _currentProgramType;
        _currentProgramType = null;
        var currentIndex = oldProgram.HasValue ? System.Array.IndexOf(_possiblePrograms, oldProgram) : 0;
        for (int i = 0; i < _possiblePrograms.Length; i++)
        {
            currentIndex = ++currentIndex % _possiblePrograms.Length;
            if (RobotModel.Programs.Any(_ => _.Template.Type == _possiblePrograms[currentIndex]))
            {
                if (_possiblePrograms[currentIndex] == ProgramType.Walk && RobotModel.Programs.Any(_ => _.Template.Type != ProgramType.Walk))
                    continue;
                _currentProgramType = _possiblePrograms[currentIndex];
                return;
            }
        }

        if (!_currentProgramType.HasValue)
            _currentProgramType = oldProgram;
    }

    private void ResetTime()
    {
        _executeTime = 0;
        _nextStep = 1;
    }

    private void ComputeTime(float deltaTime, ProgramType program)
    {
        _executeTime += deltaTime;
        int intTime = (int)_executeTime;
        intTime -= _nextStep;
        if (intTime > 0)
        {
            var currProgram = RobotModel.Programs.FirstOrDefault(_ => _.Template.Type == program);
            while (intTime-- > 0)
            {
                currProgram?.ExecuteOneSecond();
                _nextStep++;
            }
        }
    }

}
