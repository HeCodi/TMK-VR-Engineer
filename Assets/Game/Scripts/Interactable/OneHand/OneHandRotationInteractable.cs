using Assets.Game.Scripts.Interactable.Abstract;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable
{
    public class OneHandRotationInteractable : BaseOneHandInteractable
    {
        [Header("Rotation")]
        [SerializeField] private Transform _pivot;
        [SerializeField] private Transform _movable;

        [Tooltip("Локальная ось вращения")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;

        [SerializeField] private float _minAngle = -90f;
        [SerializeField] private float _maxAngle = 90f;

        [SerializeField] private float _currentAngle;

        [Header("Value")]
        [SerializeField] private ValueInteractableObject _value;

        private Vector3 _planeRight;
        private Vector3 _planeForward;

        private float _startHandAngle;
        private float _startObjectAngle;

        protected override void OnGrabStarted()
        {
            CalculatePlaneBasis();

            _startHandAngle = GetHandAngle();
            _startObjectAngle = _currentAngle;
        }

        protected override void ProcessInteraction()
        {
            float currentHandAngle = GetHandAngle();

            float delta =
                Mathf.DeltaAngle(_startHandAngle, currentHandAngle);

            _currentAngle = Mathf.Clamp(
                _startObjectAngle + delta,
                _minAngle,
                _maxAngle);

            _movable.localRotation =
                Quaternion.AngleAxis(_currentAngle, _rotationAxis);

            if (_value != null)
            {
                _value.Value = Mathf.InverseLerp(
                    _minAngle,
                    _maxAngle,
                    _currentAngle);
            }
        }

        protected override void OnGrabEnded()
        {

        }

        private void CalculatePlaneBasis()
        {
            Vector3 axis = _pivot.TransformDirection(_rotationAxis).normalized;

            _planeRight = Vector3.Cross(axis, Vector3.up);

            if (_planeRight.sqrMagnitude < 0.001f)
                _planeRight = Vector3.Cross(axis, Vector3.right);

            _planeRight.Normalize();

            _planeForward = Vector3.Cross(axis, _planeRight).normalized;
        }

        private float GetHandAngle()
        {
            Vector3 dir =
                CurrentHand.GetAttachTransform(ThisInteractable).position -
                _pivot.position;

            dir = Vector3.ProjectOnPlane(
                dir,
                _pivot.TransformDirection(_rotationAxis));

            dir.Normalize();

            float x = Vector3.Dot(dir, _planeRight);
            float y = Vector3.Dot(dir, _planeForward);

            return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        }

        public float CurrentAngle => _currentAngle;

        public float NormalizedValue =>
            Mathf.InverseLerp(_minAngle, _maxAngle, _currentAngle);
    }
}