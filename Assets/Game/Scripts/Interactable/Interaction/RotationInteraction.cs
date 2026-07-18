using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// Преобразует орбитальное движение управляющей точки
    /// вокруг Pivot в значение 0..1.
    ///
    /// Предназначен для ограниченного вращения,
    /// а не для бесконечного rotary encoder.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class RotationInteraction
        : BasePoseValueInteraction
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        private const float MinimumRadialSqrMagnitude =
            0.000001f;

        private const float MinimumAngleRange =
            0.001f;

        [Header("Rotation Frame")]

        [Tooltip(
            "Неподвижная точка вращения. Не должна быть " +
            "двигаемым Target RotationDriver.")]
        [SerializeField]
        private Transform _pivot;

        [Tooltip(
            "Система координат оси вращения.")]
        [SerializeField]
        private Transform _axisSpace;

        [SerializeField]
        private Vector3 _localAxis =
            Vector3.up;

        [Header("Rotation Range")]

        [SerializeField]
        private float _minimumAngle = -45f;

        [SerializeField]
        private float _maximumAngle = 45f;

        [SerializeField]
        private bool _invertInput;

        private Vector3 _startRadial;
        private Vector3 _startAxis;

        /*
         * Pivot и Axis механизма в координатах reference-руки.
         */
        private Vector3 _referenceLocalPivot;
        private Vector3 _referenceLocalAxis;

        private float _startAngle;
        private bool _baselineCaptured;

        private void LateUpdate()
        {
            if (!TryGetManipulationFrame(
                    out ManipulationFrame frame))
            {
                ResetBaseline();
                return;
            }

            if (!TryGetMeasurement(
                    frame,
                    out Vector3 radial,
                    out Vector3 axis))
            {
                ResetBaseline();
                return;
            }

            if (!_baselineCaptured)
            {
                CaptureBaseline(
                    radial,
                    axis);

                return;
            }

            Vector3 currentRadial =
                Vector3.ProjectOnPlane(
                    radial,
                    _startAxis);

            if (currentRadial.sqrMagnitude <
                MinimumRadialSqrMagnitude)
            {
                return;
            }

            currentRadial.Normalize();

            float deltaAngle =
                Vector3.SignedAngle(
                    _startRadial,
                    currentRadial,
                    _startAxis);

            float angle =
                _startAngle +
                deltaAngle;

            TrySetValueFromInteraction(
                AngleToValue(angle));
        }

        protected override void OnManipulationFrameInvalidated()
        {
            ResetBaseline();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                _localAxis = Vector3.up;
            }

            if (Mathf.Abs(
                    _maximumAngle -
                    _minimumAngle) <
                MinimumAngleRange)
            {
                _maximumAngle =
                    _minimumAngle + 1f;
            }
        }

        private bool TryGetMeasurement(
            ManipulationFrame frame,
            out Vector3 radial,
            out Vector3 axis)
        {
            radial = Vector3.zero;
            axis = Vector3.zero;

            if (!frame.HasReference)
            {
                axis = GetWorldAxis();

                radial =
                    frame.ControlPose.position -
                    GetPivotPosition();
            }
            else
            {
                Quaternion inverseReferenceRotation =
                    Quaternion.Inverse(
                        frame.ReferencePose.rotation);

                Vector3 controlInReference =
                    inverseReferenceRotation *
                    (frame.ControlPose.position -
                     frame.ReferencePose.position);

                /*
                 * При начале взаимодействия сохраняем положение
                 * Pivot и Axis относительно reference-руки.
                 */
                if (!_baselineCaptured)
                {
                    _referenceLocalPivot =
                        inverseReferenceRotation *
                        (GetPivotPosition() -
                         frame.ReferencePose.position);

                    _referenceLocalAxis =
                        inverseReferenceRotation *
                        GetWorldAxis();

                    if (_referenceLocalAxis.sqrMagnitude <
                        MinimumAxisSqrMagnitude)
                    {
                        return false;
                    }

                    _referenceLocalAxis.Normalize();
                }

                axis = _referenceLocalAxis;

                radial =
                    controlInReference -
                    _referenceLocalPivot;
            }

            if (axis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                return false;
            }

            axis.Normalize();

            radial =
                Vector3.ProjectOnPlane(
                    radial,
                    axis);

            if (radial.sqrMagnitude <
                MinimumRadialSqrMagnitude)
            {
                return false;
            }

            radial.Normalize();
            return true;
        }

        private void CaptureBaseline(
            Vector3 radial,
            Vector3 axis)
        {
            _startAxis =
                axis.normalized;

            _startRadial =
                Vector3.ProjectOnPlane(
                    radial,
                    _startAxis);

            if (_startRadial.sqrMagnitude <
                MinimumRadialSqrMagnitude)
            {
                ResetBaseline();
                return;
            }

            _startRadial.Normalize();

            _startAngle =
                ValueToAngle(Value);

            _baselineCaptured = true;
        }

        private void ResetBaseline()
        {
            _baselineCaptured = false;

            _startRadial = Vector3.zero;
            _startAxis = Vector3.zero;

            _referenceLocalPivot = Vector3.zero;
            _referenceLocalAxis = Vector3.zero;
        }

        private float AngleToValue(float angle)
        {
            float normalized =
                Mathf.InverseLerp(
                    _minimumAngle,
                    _maximumAngle,
                    angle);

            return _invertInput
                ? 1f - normalized
                : normalized;
        }

        private float ValueToAngle(float value)
        {
            float normalized =
                Mathf.Clamp01(value);

            if (_invertInput)
                normalized = 1f - normalized;

            return Mathf.Lerp(
                _minimumAngle,
                _maximumAngle,
                normalized);
        }

        private Vector3 GetPivotPosition()
        {
            return _pivot != null
                ? _pivot.position
                : transform.position;
        }

        private Vector3 GetWorldAxis()
        {
            Transform space = _axisSpace;

            if (space == null)
                space = _pivot;

            if (space == null)
                space = transform;

            Vector3 worldAxis =
                space.TransformDirection(
                    _localAxis);

            if (worldAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                return space.up;
            }

            return worldAxis.normalized;
        }
    }
}