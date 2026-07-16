using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Drivers
{
    /// <summary>
    /// Применяет значение 0..1 как локальное вращение.
    ///
    /// Текущая поза объекта в Awake сохраняется.
    /// Поэтому объект не прыгает при запуске сцены,
    /// даже если начальное Value не равно нулю.
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

        private Quaternion _zeroValueLocalRotation;
        private bool _referenceCaptured;

        private void Awake()
        {
            ResolveReferences();

            if (_source != null &&
                _target != null)
            {
                CaptureCurrentAsReference();
            }
        }

        private void OnEnable()
        {
            if (_source == null ||
                _target == null)
            {
                return;
            }

            _source.ValueChanged += ApplyValue;

            if (!_referenceCaptured)
                CaptureCurrentAsReference();

            ApplyValue(_source.Value);
        }

        private void OnDisable()
        {
            if (_source != null)
                _source.ValueChanged -= ApplyValue;
        }

        private void Reset()
        {
            _source =
                GetComponent<BaseValueInteraction>();

            _target = transform;
            _localAxis = Vector3.up;

            _minimumAngle = 0f;
            _maximumAngle = 45f;
        }

        private void OnValidate()
        {
            if (_localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                _localAxis = Vector3.up;
            }

            if (_target == null)
                _target = transform;
        }

        [ContextMenu("Capture Current As Reference")]
        public void CaptureCurrentAsReference()
        {
            if (_source == null ||
                _target == null)
            {
                return;
            }

            Vector3 axis =
                GetNormalizedAxis();

            float currentAngle =
                Mathf.Lerp(
                    _minimumAngle,
                    _maximumAngle,
                    _source.Value);

            Quaternion currentValueRotation =
                Quaternion.AngleAxis(
                    currentAngle,
                    axis);

            /*
             * Вычисляем rotation при Value = 0,
             * не двигая объект в момент захвата reference.
             */
            _zeroValueLocalRotation =
                _target.localRotation *
                Quaternion.Inverse(
                    currentValueRotation);

            _referenceCaptured = true;
        }

        public void ApplyValue(float value)
        {
            if (_target == null ||
                !_referenceCaptured)
            {
                return;
            }

            float angle =
                Mathf.Lerp(
                    _minimumAngle,
                    _maximumAngle,
                    Mathf.Clamp01(value));

            Quaternion valueRotation =
                Quaternion.AngleAxis(
                    angle,
                    GetNormalizedAxis());

            _target.localRotation =
                _zeroValueLocalRotation *
                valueRotation;
        }

        private void ResolveReferences()
        {
            if (_source == null)
            {
                _source =
                    GetComponent<BaseValueInteraction>();
            }

            if (_source == null)
            {
                _source =
                    GetComponentInParent
                        <BaseValueInteraction>(true);
            }

            if (_target == null)
                _target = transform;

            if (_source != null)
                return;

            Debug.LogError(
                $"{nameof(RotationDriver)} on '{name}' " +
                $"requires a {nameof(BaseValueInteraction)} source.",
                this);

            enabled = false;
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