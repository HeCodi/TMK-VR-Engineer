using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// Вычисляет значение 0..1 из движения руки
    /// вокруг заданной оси.
    ///
    /// Компонент сам объект не вращает.
    /// Результат применяется через RotationDriver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotationInteraction
        : BaseValueInteraction
    {
        private const float MinimumDirectionSqrMagnitude =
            0.000001f;

        [Header("Rotation input")]

        [SerializeField]
        private Transform _pivot;

        [SerializeField]
        private Transform _axisSpace;

        [SerializeField]
        private Vector3 _localAxis =
            Vector3.up;

        [SerializeField]
        private float _minimumAngle;

        [SerializeField]
        private float _maximumAngle = 45f;

        [SerializeField]
        private bool _invertInput;

        private Vector3 _previousDirection;
        private float _currentAngle;
        private bool _tracking;

        public float CurrentAngle =>
            _currentAngle;

        protected override Vector3 InteractionPoint =>
            Pivot.position;

        private Transform Pivot =>
            _pivot != null
                ? _pivot
                : transform;

        private Transform AxisSpace =>
            _axisSpace != null
                ? _axisSpace
                : Pivot;

        protected override void Awake()
        {
            base.Awake();

            _currentAngle =
                ValueToAngle(Value);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_localAxis.sqrMagnitude <
                MinimumDirectionSqrMagnitude)
            {
                _localAxis = Vector3.up;
            }
        }

        private void Reset()
        {
            _pivot = transform;
            _axisSpace = transform;
            _localAxis = Vector3.up;

            _minimumAngle = 0f;
            _maximumAngle = 45f;
        }

        private void LateUpdate()
        {
            if (!TryGetActiveAttach(
                    out Transform attach))
            {
                _tracking = false;
                return;
            }

            Vector3 worldAxis =
                GetWorldAxis();

            Vector3 radialDirection =
                attach.position -
                Pivot.position;

            radialDirection =
                Vector3.ProjectOnPlane(
                    radialDirection,
                    worldAxis);

            if (radialDirection.sqrMagnitude <
                MinimumDirectionSqrMagnitude)
            {
                _tracking = false;
                return;
            }

            radialDirection.Normalize();

            if (!_tracking)
            {
                _previousDirection =
                    radialDirection;

                _currentAngle =
                    ValueToAngle(Value);

                _tracking = true;
                return;
            }

            float deltaAngle =
                Vector3.SignedAngle(
                    _previousDirection,
                    radialDirection,
                    worldAxis);

            _previousDirection =
                radialDirection;

            if (_invertInput)
                deltaAngle = -deltaAngle;

            GetOrderedLimits(
                out float minimum,
                out float maximum);

            _currentAngle =
                Mathf.Clamp(
                    _currentAngle + deltaAngle,
                    minimum,
                    maximum);

            TrySetValueFromInteraction(
                Mathf.InverseLerp(
                    minimum,
                    maximum,
                    _currentAngle));
        }

        protected override void OnValueUpdated(
            float previousValue,
            float currentValue)
        {
            _currentAngle =
                ValueToAngle(currentValue);
        }

        protected override void OnActiveManipulatorChanged(
            IXRSelectInteractor previous,
            IXRSelectInteractor current)
        {
            /*
             * При переходе на оставшуюся руку
             * сохраняем текущее значение,
             * но заново запоминаем направление руки.
             */
            _tracking = false;

            _currentAngle =
                ValueToAngle(Value);
        }

        private Vector3 GetWorldAxis()
        {
            Vector3 worldAxis =
                AxisSpace.TransformDirection(
                    _localAxis);

            if (worldAxis.sqrMagnitude <
                MinimumDirectionSqrMagnitude)
            {
                return Vector3.up;
            }

            return worldAxis.normalized;
        }

        private float ValueToAngle(float value)
        {
            GetOrderedLimits(
                out float minimum,
                out float maximum);

            return Mathf.Lerp(
                minimum,
                maximum,
                Mathf.Clamp01(value));
        }

        private void GetOrderedLimits(
            out float minimum,
            out float maximum)
        {
            minimum =
                Mathf.Min(
                    _minimumAngle,
                    _maximumAngle);

            maximum =
                Mathf.Max(
                    _minimumAngle,
                    _maximumAngle);

            if (Mathf.Approximately(
                    minimum,
                    maximum))
            {
                maximum = minimum + 0.001f;
            }
        }
    }
}