using Assets.Game.Scripts.Interactable;
using Assets.Game.Scripts.Interactable.Abstract;
using NUnit;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static Assets.Game.Scripts.Interactable.Abstract.BaseGrabHandler;

public class LinearToolLenth : BaseTwoHandInteractableTool
{
    [SerializeField] private Transform _targetAttachFirstPoint;
    [SerializeField] private Transform _targetAttachSecondPoint;

    [SerializeField] private float _maxDistanceToTargetPoint;

    [SerializeField] private ValueInteractableObject valueObject;

    [SerializeField] private Transform _movableElement;
    [SerializeField] private Transform _basePointh;
    [SerializeField] private Transform _fullPoint;

    [SerializeField] private float _length;

    protected bool IsGrapedMovableElement = false;
    protected AttachPoint CurrentIteractorMovableElement = new(null, null);

    public float CurrentLength => (float)valueObject.Value;

    private void Start()
    {

    }

    private void Update()
    {
        if (!IsGrapedMovableElement)
            return;

        Vector3 direction =
        _targetAttachSecondPoint.position - _targetAttachFirstPoint.position;

        transform.rotation = Quaternion.LookRotation(
        direction,
        _targetAttachFirstPoint.up);

        Vector3 localHand = transform.InverseTransformPoint(CurrentIteractorMovableElement.AttachTransform.position);
        Vector3 localA = transform.InverseTransformPoint(_basePointh.position);
        Vector3 localB = transform.InverseTransformPoint(_fullPoint.position);

        Vector3 ab = localB - localA;

        float t = Vector3.Dot(localHand - localA, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);

        _movableElement.position = Vector3.Lerp(_basePointh.position, _fullPoint.position, t);

        print(t * _length);
    }

    protected override void WasDetachTwoHand()
    {
        IsGrapedMovableElement = false;
        CurrentIteractorMovableElement.Interactor = null;
        CurrentIteractorMovableElement.AttachTransform = null;
    }

    protected override void WasSelectTwoHand()
    {
        List<AttachTargetPoint> attachPoints = GetDistancesAttachToTargetPoint(_targetAttachSecondPoint);
        AttachTargetPoint attachPoint = attachPoints[1];

        if (attachPoint.DistanceToTarget > _maxDistanceToTargetPoint)
            return;

        IsGrapedMovableElement = true;
        CurrentIteractorMovableElement.Interactor = attachPoint.attachPoint.Interactor;
        CurrentIteractorMovableElement.AttachTransform = attachPoint.attachPoint.AttachTransform;
    }

    private float GetCurrentLength()
    {
        Vector3 a = _basePointh.position;
        Vector3 b = _fullPoint.position;
        Vector3 p = _movableElement.position;

        Vector3 ab = b - a;

        float t = Vector3.Dot(p - a, ab) / ab.sqrMagnitude;

        t = Mathf.Clamp01(t);

        return t * _length;
    }
}
