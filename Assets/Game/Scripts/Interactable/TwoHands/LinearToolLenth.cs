using Assets.Game.Scripts.Interactable;
using Assets.Game.Scripts.Interactable.Abstract;
using Assets.Game.Scripts.Select;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LinearToolLenth : BaseTwoHandInteractableTool
{
    [SerializeField] private Transform _targetAttachFirstPoint;
    [SerializeField] private Transform _targetAttachSecondPoint;

    [SerializeField] private float _maxDistanceToTargetPoint;

    [SerializeField] private ValueInteractableObject valueObject;
    [SerializeField] private float _sliderSmooth = 25f;

    [SerializeField] private Transform _movableElement;
    [SerializeField] private Transform _basePointh;
    [SerializeField] private Transform _fullPoint;

    [SerializeField] private float _length;

    protected bool IsGrapedMovableElement = false;
    protected AttachPointData CurrentIteractorMovableElement = new(null, null);

    private float _currentT;

    public float CurrentLength => (float)valueObject.Value;

    private void Start()
    {

    }

    private void Update()
    {
        if (!IsGrapedMovableElement)
            return;

        Vector3 localHand =
            transform.InverseTransformPoint(
                CurrentIteractorMovableElement.AttachTransform.position);

        Vector3 localA =
            transform.InverseTransformPoint(_basePointh.position);

        Vector3 localB =
            transform.InverseTransformPoint(_fullPoint.position);

        Vector3 ab = localB - localA;

        float t = Vector3.Dot(localHand - localA, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);

        // Плавно приближаемся к новой позиции
        _currentT = Mathf.Lerp(
            _currentT,
            t,
            Time.deltaTime * _sliderSmooth);

        _movableElement.localPosition = Vector3.Lerp(
        _basePointh.localPosition,
        _fullPoint.localPosition,
        _currentT);

        valueObject.Value = _currentT * _length;
    }

    protected override void WasDetachTwoHand()
    {
        IsGrapedMovableElement = false;
        CurrentIteractorMovableElement.Interactor = null;
        CurrentIteractorMovableElement.AttachTransform = null;
    }

    protected override void WasSelectTwoHand()
    {

        List<AttachTargetPointData> attachPoints = GetDistancesAttachToTargetPoint(_targetAttachSecondPoint);
        AttachTargetPointData attachPoint = attachPoints.OrderBy(x => x.DistanceToTarget).First();

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
