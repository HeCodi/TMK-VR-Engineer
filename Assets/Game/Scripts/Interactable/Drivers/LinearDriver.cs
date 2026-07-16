using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Drivers
{
    /// <summary>
    /// Применяет нормализованное значение 0..1
    /// как локальное линейное смещение Target.
    ///
    /// Положение Target в момент Awake считается
    /// положением с нулевым смещением.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LinearDriver : MonoBehaviour
    {
        private const float MinimumAxisSqrMagnitude = 0.000001f;

        [Header("Source")]

        [SerializeField]
        private BaseValueInteraction _source;

        [Header("Target")]

        [SerializeField]
        private Transform _target;

        [SerializeField]
        private Vector3 _localAxis = Vector3.right;

        [SerializeField]
        private float _minimumOffset;

        [SerializeField]
        private float _maximumOffset = 1f;

        private Vector3 _referenceLocalPosition;
        private bool _referenceCaptured;

        private void Awake()
        {
            ResolveReferences();

            if (_target != null)
                CaptureCurrentPositionAsZero();
        }

        private void OnEnable()
        {
            if (_source == null || _target == null)
                return;

            _source.ValueChanged += ApplyValue;

            if (!_referenceCaptured)
                CaptureCurrentPositionAsZero();

            ApplyValue(_source.Value);
        }

        private void OnDisable()
        {
            if (_source != null)
                _source.ValueChanged -= ApplyValue;
        }

        private void Reset()
        {
            _source = GetComponent<BaseValueInteraction>();
            _target = transform;

            _localAxis = Vector3.right;
            _minimumOffset = 0f;
            _maximumOffset = 1f;
        }

        private void OnValidate()
        {
            if (_localAxis.sqrMagnitude < MinimumAxisSqrMagnitude)
                _localAxis = Vector3.right;

            if (_target == null)
                _target = transform;
        }

        /// <summary>
        /// Текущая позиция Target становится точкой,
        /// относительно которой считаются смещения.
        /// </summary>
        [ContextMenu("Capture Current Position As Zero")]
        public void CaptureCurrentPositionAsZero()
        {
            if (_target == null)
                _target = transform;

            _referenceLocalPosition = _target.localPosition;
            _referenceCaptured = true;
        }

        public void ApplyValue(float value)
        {
            if (_target == null || !_referenceCaptured)
                return;

            float normalizedValue = Mathf.Clamp01(value);

            float offset = Mathf.Lerp(
                _minimumOffset,
                _maximumOffset,
                normalizedValue);

            _target.localPosition =
                _referenceLocalPosition +
                GetNormalizedAxis() * offset;
        }

        private void ResolveReferences()
        {
            if (_source == null)
                _source = GetComponent<BaseValueInteraction>();

            if (_source == null)
            {
                _source =
                    GetComponentInParent<BaseValueInteraction>(true);
            }

            if (_target == null)
                _target = transform;

            if (_source != null)
                return;

            Debug.LogError(
                $"{nameof(LinearDriver)} on '{name}' requires " +
                $"{nameof(BaseValueInteraction)} source.",
                this);

            enabled = false;
        }

        private Vector3 GetNormalizedAxis()
        {
            if (_localAxis.sqrMagnitude < MinimumAxisSqrMagnitude)
                return Vector3.right;

            return _localAxis.normalized;
        }
    }
}