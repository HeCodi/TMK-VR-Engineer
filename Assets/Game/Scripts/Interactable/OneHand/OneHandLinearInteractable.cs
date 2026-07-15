using Assets.Game.Scripts.Interactable.Abstract;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable
{
    public class OneHandLinearInteractable : BaseOneHandInteractable
    {
        [Header("Linear")]
        [SerializeField] private Transform _movable;

        [SerializeField] private Transform _startPoint;
        [SerializeField] private Transform _endPoint;

        [SerializeField] private float _length = 1f;

        [Header("Value")]
        [SerializeField] private ValueInteractableObject _value;

        private Vector3 _lineDirection;
        private float _lineLength;

        protected override void OnGrabStarted()
        {
            _lineDirection =
                (_endPoint.position - _startPoint.position).normalized;

            _lineLength =
                Vector3.Distance(_startPoint.position, _endPoint.position);
        }

        protected override void ProcessInteraction()
        {
            Vector3 handPosition =
                CurrentHand.GetAttachTransform(ThisInteractable).position;

            Vector3 start =
                _startPoint.position;

            float distance =
                Vector3.Dot(handPosition - start, _lineDirection);

            distance = Mathf.Clamp(distance, 0f, _lineLength);

            _movable.position =
                start + _lineDirection * distance;

            if (_value != null)
            {
                _value.Value =
                    Mathf.Lerp(
                        0,
                        _length,
                        distance / _lineLength);
            }
        }

        protected override void OnGrabEnded()
        {

        }

        public float CurrentLength
        {
            get
            {
                return Mathf.Lerp(
                    0,
                    _length,
                    Vector3.Distance(
                        _startPoint.position,
                        _movable.position) / _lineLength);
            }
        }

        public float NormalizedValue
        {
            get
            {
                return Vector3.Distance(
                    _startPoint.position,
                    _movable.position) / _lineLength;
            }
        }
    }
}