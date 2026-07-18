using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Drivers
{
    /// <summary>
    /// Применяет Value 0..1 как локальное линейное смещение.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LinearDriver : MonoBehaviour
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
            Vector3.right;

        [SerializeField]
        private float _minimumOffset;

        [SerializeField]
        private float _maximumOffset = 1f;

        private Vector3 _zeroValueLocalPosition;

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
                _localAxis = Vector3.right;
            }

            if (_source == null)
                TryAutoAssignSource();

            _referenceCaptured = false;
        }

        /// <summary>
        /// Сохраняет текущую сценную позицию как позу,
        /// соответствующую текущему Source.Value.
        ///
        /// После вызова ApplyValue(Source.Value)
        /// объект останется на месте.
        /// </summary>
        public void CaptureCurrentAsReference()
        {
            if (!ResolveReferences(logError: true))
                return;

            Vector3 axis =
                GetNormalizedAxis();

            float currentOffset =
                Mathf.Lerp(
                    _minimumOffset,
                    _maximumOffset,
                    _source.Value);

            _zeroValueLocalPosition =
                _target.localPosition -
                axis * currentOffset;

            _referenceCaptured = true;
        }

        /// <summary>
        /// Явно считает текущую позицию положением Value = 0.
        ///
        /// Этот метод оставлен для совместимости со старым API.
        /// </summary>
        public void CaptureCurrentPositionAsZero()
        {
            if (_target == null)
                return;

            _zeroValueLocalPosition =
                _target.localPosition;

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

            float offset =
                Mathf.Lerp(
                    _minimumOffset,
                    _maximumOffset,
                    Mathf.Clamp01(value));

            _target.localPosition =
                _zeroValueLocalPosition +
                axis * offset;
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
                    $"{nameof(LinearDriver)} on '{name}' requires an " +
                    $"{nameof(BaseValueInteraction)} Source. " +
                    $"When mechanics are chained, assign the final " +
                    $"mechanic that controls the visible position.",
                    this);
            }

            if (_target == null)
            {
                Debug.LogError(
                    $"{nameof(LinearDriver)} on '{name}' requires a Target.",
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
                return Vector3.right;
            }

            return _localAxis.normalized;
        }
    }
}