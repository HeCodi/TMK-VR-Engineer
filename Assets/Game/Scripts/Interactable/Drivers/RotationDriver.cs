using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Drivers
{
    /// <summary>
    /// Применяет Value 0..1 как локальное вращение Target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotationDriver : MonoBehaviour
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        [Header("Source")]

        [SerializeField]
        private BaseValueInteraction _source;

        [Header("Target")]

        [SerializeField]
        private Transform _target;

        [SerializeField]
        private Vector3 _localAxis =
            Vector3.up;

        [SerializeField]
        private float _minimumAngle;

        [SerializeField]
        private float _maximumAngle = 45f;

        private Quaternion _zeroValueLocalRotation =
            Quaternion.identity;

        private bool _referenceCaptured;
        private bool _subscribed;

        public BaseValueInteraction Source =>
            _source;

        public Transform Target =>
            _target;

        private void Awake()
        {
            if (!ResolveReferences(logError: true))
            {
                enabled = false;
                return;
            }

            CaptureCurrentAsReference();
        }

        private void OnEnable()
        {
            if (!ResolveReferences(logError: true))
            {
                enabled = false;
                return;
            }

            if (!_referenceCaptured)
                CaptureCurrentAsReference();

            Subscribe();
            ApplyValue(_source.Value);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            _target = transform;
            TryAutoAssignSource();
        }

        private void OnValidate()
        {
            if (_target == null)
                _target = transform;

            if (_localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                _localAxis = Vector3.up;
            }

            if (_source == null)
                TryAutoAssignSource();

            _referenceCaptured = false;
        }

        /// <summary>
        /// Сохраняет текущую сценную ориентацию как позу,
        /// соответствующую текущему Source.Value.
        /// </summary>
        public void CaptureCurrentAsReference()
        {
            if (!ResolveReferences(logError: true))
                return;

            Vector3 axis =
                GetNormalizedAxis();

            float currentAngle =
                Mathf.Lerp(
                    _minimumAngle,
                    _maximumAngle,
                    _source.Value);

            Quaternion currentOffset =
                Quaternion.AngleAxis(
                    currentAngle,
                    axis);

            _zeroValueLocalRotation =
                _target.localRotation *
                Quaternion.Inverse(currentOffset);

            _referenceCaptured = true;
        }

        /// <summary>
        /// Явно считает текущую ориентацию положением Value = 0.
        /// </summary>
        public void CaptureCurrentRotationAsZero()
        {
            if (_target == null)
                return;

            _zeroValueLocalRotation =
                _target.localRotation;

            _referenceCaptured = true;
        }

        public void ApplyValue(float value)
        {
            if (_target == null)
                return;

            if (!_referenceCaptured)
                CaptureCurrentAsReference();

            Vector3 axis =
                GetNormalizedAxis();

            float angle =
                Mathf.Lerp(
                    _minimumAngle,
                    _maximumAngle,
                    Mathf.Clamp01(value));

            _target.localRotation =
                _zeroValueLocalRotation *
                Quaternion.AngleAxis(
                    angle,
                    axis);
        }

        private void Subscribe()
        {
            if (_subscribed ||
                _source == null)
            {
                return;
            }

            _source.ValueChanged +=
                ApplyValue;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                _source == null)
            {
                return;
            }

            _source.ValueChanged -=
                ApplyValue;

            _subscribed = false;
        }

        private bool ResolveReferences(bool logError)
        {
            if (_target == null)
                _target = transform;

            if (_source == null)
                TryAutoAssignSource();

            if (_source != null &&
                _target != null)
            {
                return true;
            }

            if (!logError)
                return false;

            if (_source == null)
            {
                Debug.LogError(
                    $"{nameof(RotationDriver)} on '{name}' requires an " +
                    $"{nameof(BaseValueInteraction)} Source. " +
                    $"When mechanics are chained, assign the final " +
                    $"mechanic that controls the visible rotation.",
                    this);
            }

            if (_target == null)
            {
                Debug.LogError(
                    $"{nameof(RotationDriver)} on '{name}' requires a Target.",
                    this);
            }

            return false;
        }

        private void TryAutoAssignSource()
        {
            BaseValueInteraction[] localSources =
                GetComponents<BaseValueInteraction>();

            if (localSources.Length == 1)
            {
                _source = localSources[0];
                return;
            }

            if (localSources.Length > 1)
                return;

            Transform current = transform.parent;

            while (current != null)
            {
                BaseValueInteraction[] parentSources =
                    current.GetComponents<BaseValueInteraction>();

                if (parentSources.Length == 1)
                {
                    _source = parentSources[0];
                    return;
                }

                if (parentSources.Length > 1)
                    return;

                current = current.parent;
            }
        }

        private Vector3 GetNormalizedAxis()
        {
            if (_localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                return Vector3.up;
            }

            return _localAxis.normalized;
        }
    }
}