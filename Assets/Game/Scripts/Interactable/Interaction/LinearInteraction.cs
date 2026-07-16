using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// Вычисляет значение 0..1 из положения руки
    /// вдоль заданной оси.
    ///
    /// Компонент подходит для:
    /// - ползунков;
    /// - задвижек;
    /// - кнопок;
    /// - суппортов;
    /// - линейных частей инструмента.
    ///
    /// Сам объект не перемещает.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LinearInteraction
        : BaseValueInteraction
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        [Header("Linear input")]

        [SerializeField]
        private Transform _origin;

        [SerializeField]
        private Transform _axisSpace;

        [SerializeField]
        private Vector3 _localAxis =
            Vector3.forward;

        [SerializeField]
        private float _minimumDistance;

        [SerializeField]
        private float _maximumDistance = 0.1f;

        private float _grabOffset;
        private float _currentDistance;
        private bool _tracking;

        public float CurrentDistance =>
            _currentDistance;

        protected override Vector3 InteractionPoint =>
            Origin.position;

        private Transform Origin =>
            _origin != null
                ? _origin
                : transform;

        private Transform AxisSpace =>
            _axisSpace != null
                ? _axisSpace
                : Origin;

        protected override void Awake()
        {
            base.Awake();

            _currentDistance =
                ValueToDistance(Value);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                _localAxis = Vector3.forward;
            }
        }

        private void Reset()
        {
            _origin = transform;
            _axisSpace = transform;
            _localAxis = Vector3.forward;

            _minimumDistance = 0f;
            _maximumDistance = 0.1f;
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

            float handDistance =
                Vector3.Dot(
                    attach.position -
                    Origin.position,
                    worldAxis);

            if (!_tracking)
            {
                _currentDistance =
                    ValueToDistance(Value);

                /*
                 * Сохраняем разницу между рукой
                 * и текущим положением механизма.
                 * Поэтому ползунок не прыгает к руке.
                 */
                _grabOffset =
                    _currentDistance -
                    handDistance;

                _tracking = true;
                return;
            }

            float targetDistance =
                handDistance +
                _grabOffset;

            GetOrderedLimits(
                out float minimum,
                out float maximum);

            _currentDistance =
                Mathf.Clamp(
                    targetDistance,
                    minimum,
                    maximum);

            TrySetValueFromInteraction(
                Mathf.InverseLerp(
                    minimum,
                    maximum,
                    _currentDistance));
        }

        protected override void OnValueUpdated(
            float previousValue,
            float currentValue)
        {
            _currentDistance =
                ValueToDistance(currentValue);
        }

        protected override void OnActiveManipulatorChanged(
            IXRSelectInteractor previous,
            IXRSelectInteractor current)
        {
            /*
             * При смене руки новое смещение
             * рассчитывается от текущего значения.
             */
            _tracking = false;

            _currentDistance =
                ValueToDistance(Value);
        }

        private Vector3 GetWorldAxis()
        {
            Vector3 worldAxis =
                AxisSpace.TransformDirection(
                    _localAxis);

            if (worldAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                return Vector3.forward;
            }

            return worldAxis.normalized;
        }

        private float ValueToDistance(float value)
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
                    _minimumDistance,
                    _maximumDistance);

            maximum =
                Mathf.Max(
                    _minimumDistance,
                    _maximumDistance);

            if (Mathf.Approximately(
                    minimum,
                    maximum))
            {
                maximum = minimum + 0.0001f;
            }
        }
    }
}