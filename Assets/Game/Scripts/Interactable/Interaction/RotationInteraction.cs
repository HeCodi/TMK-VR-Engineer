using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// Преобразует движение управляющей Pose
    /// в нормализованное значение 0..1.
    ///
    /// Режимы:
    ///
    /// OrbitPosition:
    /// Положение руки вращается вокруг Pivot.
    /// Подходит для двери, вентиля, руля и колеса.
    ///
    /// TangentialDrag:
    /// Перемещение руки проецируется на касательную
    /// к траектории Tracking Point.
    /// Подходит для дверной ручки и коротких рычагов.
    ///
    /// ControlRotation:
    /// Используется поворот кисти или контроллера
    /// вокруг заданной оси.
    ///
    /// Поддерживает диапазоны больше 360 градусов.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class RotationInteraction
        : BasePoseValueInteraction
    {
        public enum RotationInputMode
        {
            OrbitPosition,
            TangentialDrag,
            ControlRotation
        }

        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        private const float MinimumDirectionSqrMagnitude =
            0.000001f;

        private const float MinimumQuaternionSqrMagnitude =
            0.00000001f;

        private const float MinimumAngleRange =
            0.001f;

        [Header("Input Mode")]

        [Tooltip(
            "Orbit Position — положение руки вокруг Pivot.\n" +
            "Tangential Drag — движение вдоль траектории ручки.\n" +
            "Control Rotation — поворот кисти вокруг оси.")]
        [SerializeField]
        private RotationInputMode _inputMode =
            RotationInputMode.OrbitPosition;

        [Header("Rotation Frame")]

        [Tooltip(
            "Центр вращения механизма.")]
        [SerializeField]
        private Transform _pivot;

        [Tooltip(
            "Стабильная система координат оси.\n" +
            "Она не должна быть вращаемым Target RotationDriver.\n" +
            "Для ручки назначь родителя HandlePivot.")]
        [SerializeField]
        private Transform _axisSpace;

        [Tooltip(
            "Ось вращения в локальной системе Axis Space.")]
        [SerializeField]
        private Vector3 _localAxis =
            Vector3.up;

        [Header("Tangential Drag")]

        [Tooltip(
            "Точка на вращаемом механизме, которая должна " +
            "следовать за рукой.\n" +
            "Для дверной ручки поставь её на конце ручки " +
            "в центре области захвата.")]
        [SerializeField]
        private Transform _trackingPoint;

        [Tooltip(
            "Чувствительность касательного перемещения.\n" +
            "1 означает физическое соответствие длине рычага.")]
        [SerializeField]
        private float _tangentialSensitivity =
            1f;

        [Header("Control Rotation")]

        [Tooltip(
            "Чувствительность поворота кисти.\n" +
            "1 означает один градус механизма " +
            "на один градус поворота кисти.")]
        [SerializeField]
        private float _controlRotationSensitivity =
            1f;

        [Header("Rotation Range")]

        [Tooltip(
            "Угол, соответствующий Value = 0.")]
        [SerializeField]
        private float _minimumAngle =
            -45f;

        [Tooltip(
            "Угол, соответствующий Value = 1.\n" +
            "Можно указывать значения больше 360 градусов.")]
        [SerializeField]
        private float _maximumAngle =
            45f;

        [Tooltip(
            "Разворачивает направление управления.")]
        [SerializeField]
        private bool _invertInput;

        [Header("Tracking Stability")]

        [Tooltip(
            "Минимальное расстояние от Pivot до управляющей " +
            "или Tracking Point.\n" +
            "Возле оси направление становится нестабильным.")]
        [SerializeField]
        [Min(0f)]
        private float _minimumTrackingRadius =
            0.005f;

        [Tooltip(
            "Расстояние, после которого отслеживание " +
            "возобновляется после приближения к оси.")]
        [SerializeField]
        [Min(0f)]
        private float _resumeTrackingRadius =
            0.01f;

        [Tooltip(
            "Максимальное изменение угла за один кадр.\n" +
            "Больший скачок считается ошибкой трекинга.\n" +
            "Ноль отключает фильтр.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float _maximumDeltaAnglePerFrame =
            120f;

        /*
         * Состояние общей манипуляции.
         */

        private float _accumulatedAngle;

        private Vector3 _trackingAxis;

        private bool _baselineCaptured;

        private bool _trackingSuspended;

        private RotationInputMode _capturedInputMode;

        /*
         * Orbit Position.
         */

        private Vector3 _previousOrbitDirection;

        /*
         * Tangential Drag.
         */

        private Vector3 _previousControlPosition;

        private Vector3 _tangentialBaselineRadial;

        private float _tangentialRadius;

        private float _tangentialBaselineAngle;

        /*
         * Control Rotation.
         */

        private Quaternion _previousControlRotation =
            Quaternion.identity;

        /*
         * Reference-hand frame.
         *
         * Pivot и Axis фиксируются относительно reference-руки
         * при начале взаимодействия.
         */

        private bool _referenceFrameCaptured;

        private Vector3 _referenceLocalPivot;

        private Vector3 _referenceLocalAxis;

        private readonly struct RotationMeasurement
        {
            public Vector3 ControlPosition { get; }

            public Quaternion ControlRotation { get; }

            public Vector3 PivotPosition { get; }

            public Vector3 Axis { get; }

            public Vector3 TrackingPointPosition { get; }

            public bool HasTrackingPoint { get; }

            public RotationMeasurement(
                Vector3 controlPosition,
                Quaternion controlRotation,
                Vector3 pivotPosition,
                Vector3 axis,
                Vector3 trackingPointPosition,
                bool hasTrackingPoint)
            {
                ControlPosition =
                    controlPosition;

                ControlRotation =
                    controlRotation;

                PivotPosition =
                    pivotPosition;

                Axis =
                    axis;

                TrackingPointPosition =
                    trackingPointPosition;

                HasTrackingPoint =
                    hasTrackingPoint;
            }
        }

        private void LateUpdate()
        {
            if (!TryGetManipulationFrame(
                    out ManipulationFrame frame))
            {
                /*
                 * Завершение input отдельно вызовет
                 * OnManipulationFrameInvalidated.
                 *
                 * Здесь может быть временно потерянная Pose,
                 * поэтому накопленный угол не сбрасываем.
                 */
                SuspendTracking();
                return;
            }

            if (!TryGetMeasurement(
                    frame,
                    out RotationMeasurement measurement))
            {
                SuspendTracking();
                return;
            }

            /*
             * Если режим был изменён во время Play Mode,
             * начинаем новую baseline без изменения Value.
             */
            if (_baselineCaptured &&
                _capturedInputMode != _inputMode)
            {
                ResetTracking();
            }

            if (!_baselineCaptured)
            {
                CaptureBaseline(
                    measurement);

                return;
            }

            /*
             * После временной потери Pose сначала сохраняем
             * новое текущее положение руки.
             *
             * В этом кадре угол не меняется,
             * поэтому скачка не возникает.
             */
            if (_trackingSuspended)
            {
                ReanchorTracking(
                    measurement);

                return;
            }

            float axisAlignment =
                Vector3.Dot(
                    _trackingAxis,
                    measurement.Axis);

            if (!IsFinite(axisAlignment) ||
                axisAlignment < 0.9999f)
            {
                ReanchorTracking(
                    measurement);

                return;
            }

            if (!TryCalculateDeltaAngle(
                    measurement,
                    out float deltaAngle))
            {
                SuspendTracking();
                return;
            }

            if (!IsFinite(deltaAngle))
                return;

            if (_invertInput)
            {
                deltaAngle =
                    -deltaAngle;
            }

            float maximumDelta =
                Mathf.Clamp(
                    _maximumDeltaAnglePerFrame,
                    0f,
                    180f);

            if (maximumDelta > 0f &&
                Mathf.Abs(deltaAngle) >
                maximumDelta)
            {
                /*
                 * Предыдущая Pose уже была обновлена внутри
                 * конкретного режима.
                 *
                 * Поэтому один плохой кадр не будет повторно
                 * применяться на следующем кадре.
                 */
                return;
            }

            _accumulatedAngle =
                ClampAngleToRange(
                    _accumulatedAngle +
                    deltaAngle);

            TrySetValueFromInteraction(
                AngleToValue(
                    _accumulatedAngle));
        }

        protected override void OnManipulationFrameInvalidated()
        {
            ResetTracking();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (!IsFinite(_localAxis) ||
                _localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                _localAxis =
                    Vector3.up;
            }

            if (!IsFinite(_minimumAngle))
            {
                _minimumAngle =
                    -45f;
            }

            if (!IsFinite(_maximumAngle))
            {
                _maximumAngle =
                    45f;
            }

            if (Mathf.Abs(
                    _maximumAngle -
                    _minimumAngle) <
                MinimumAngleRange)
            {
                _maximumAngle =
                    _minimumAngle + 1f;
            }

            if (!IsFinite(
                    _tangentialSensitivity))
            {
                _tangentialSensitivity =
                    1f;
            }

            if (!IsFinite(
                    _controlRotationSensitivity))
            {
                _controlRotationSensitivity =
                    1f;
            }

            if (!IsFinite(
                    _minimumTrackingRadius))
            {
                _minimumTrackingRadius =
                    0.005f;
            }

            if (!IsFinite(
                    _resumeTrackingRadius))
            {
                _resumeTrackingRadius =
                    0.01f;
            }

            _minimumTrackingRadius =
                Mathf.Max(
                    0f,
                    _minimumTrackingRadius);

            _resumeTrackingRadius =
                Mathf.Max(
                    _minimumTrackingRadius,
                    _resumeTrackingRadius);

            if (!IsFinite(
                    _maximumDeltaAnglePerFrame))
            {
                _maximumDeltaAnglePerFrame =
                    120f;
            }

            _maximumDeltaAnglePerFrame =
                Mathf.Clamp(
                    _maximumDeltaAnglePerFrame,
                    0f,
                    180f);
        }

        private bool TryCalculateDeltaAngle(
            RotationMeasurement measurement,
            out float deltaAngle)
        {
            deltaAngle =
                0f;

            switch (_inputMode)
            {
                case RotationInputMode.OrbitPosition:
                    return TryCalculateOrbitDelta(
                        measurement,
                        out deltaAngle);

                case RotationInputMode.TangentialDrag:
                    return TryCalculateTangentialDelta(
                        measurement,
                        out deltaAngle);

                case RotationInputMode.ControlRotation:
                    return TryCalculateControlRotationDelta(
                        measurement,
                        out deltaAngle);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Использует положение руки относительно Pivot.
        ///
        /// Хорошо подходит для большой двери или колеса,
        /// когда рука действительно движется по окружности.
        /// </summary>
        private bool TryCalculateOrbitDelta(
            RotationMeasurement measurement,
            out float deltaAngle)
        {
            deltaAngle =
                0f;

            if (!TryGetOrbitDirection(
                    measurement,
                    _minimumTrackingRadius,
                    out Vector3 currentDirection))
            {
                return false;
            }

            deltaAngle =
                Vector3.SignedAngle(
                    _previousOrbitDirection,
                    currentDirection,
                    _trackingAxis);

            /*
             * Обновляем даже до проверки максимального delta,
             * чтобы ошибочный кадр не повторился.
             */
            _previousOrbitDirection =
                currentDirection;

            return IsFinite(deltaAngle);
        }

        /// <summary>
        /// Использует только движение руки вдоль касательной
        /// к траектории Tracking Point.
        ///
        /// Радиальное движение к Pivot или от Pivot
        /// почти полностью игнорируется.
        /// </summary>
        private bool TryCalculateTangentialDelta(
            RotationMeasurement measurement,
            out float deltaAngle)
        {
            deltaAngle =
                0f;

            Vector3 currentPosition =
                measurement.ControlPosition;

            Vector3 displacement =
                currentPosition -
                _previousControlPosition;

            /*
             * Предыдущую позицию обновляем всегда.
             */
            _previousControlPosition =
                currentPosition;

            if (!IsFinite(displacement))
                return false;

            if (_tangentialRadius <
                Mathf.Max(
                    MinimumDirectionSqrMagnitude,
                    _minimumTrackingRadius))
            {
                return false;
            }

            /*
             * Baseline radial соответствует положению
             * Tracking Point в момент захвата.
             *
             * Поворачиваем её на изменение угла,
             * которое уже произошло после захвата.
             */
            float angleFromBaseline =
                _accumulatedAngle -
                _tangentialBaselineAngle;

            Vector3 currentRadial =
                Quaternion.AngleAxis(
                    angleFromBaseline,
                    _trackingAxis) *
                _tangentialBaselineRadial;

            if (!TryNormalizeDirection(
                    currentRadial,
                    out currentRadial))
            {
                return false;
            }

            /*
             * Производная вращения вокруг Axis:
             *
             * tangent = axis × radial
             */
            Vector3 tangent =
                Vector3.Cross(
                    _trackingAxis,
                    currentRadial);

            if (!TryNormalizeDirection(
                    tangent,
                    out tangent))
            {
                return false;
            }

            float tangentialDistance =
                Vector3.Dot(
                    displacement,
                    tangent);

            if (!IsFinite(tangentialDistance))
                return false;

            /*
             * Длина дуги:
             *
             * distance = radius * angleRadians
             *
             * Поэтому:
             *
             * angleRadians = distance / radius
             */
            deltaAngle =
                tangentialDistance /
                _tangentialRadius *
                Mathf.Rad2Deg *
                _tangentialSensitivity;

            return IsFinite(deltaAngle);
        }

        /// <summary>
        /// Использует вращение управляющей кисти
        /// вокруг оси механизма.
        ///
        /// Swing-компонента вращения отбрасывается,
        /// остаётся только Twist вокруг Axis.
        /// </summary>
        private bool TryCalculateControlRotationDelta(
            RotationMeasurement measurement,
            out float deltaAngle)
        {
            Quaternion currentRotation =
                measurement.ControlRotation;

            if (!TryGetSignedTwistDelta(
                    _previousControlRotation,
                    currentRotation,
                    _trackingAxis,
                    out deltaAngle))
            {
                return false;
            }

            _previousControlRotation =
                currentRotation;

            deltaAngle *=
                _controlRotationSensitivity;

            return IsFinite(deltaAngle);
        }

        private void CaptureBaseline(
            RotationMeasurement measurement)
        {
            if (!TryNormalizeDirection(
                    measurement.Axis,
                    out _trackingAxis))
            {
                ResetTracking();
                return;
            }

            _accumulatedAngle =
                ClampAngleToRange(
                    ValueToAngle(
                        Value));

            _capturedInputMode =
                _inputMode;

            bool captured;

            switch (_inputMode)
            {
                case RotationInputMode.OrbitPosition:
                    captured =
                        CaptureOrbitBaseline(
                            measurement);
                    break;

                case RotationInputMode.TangentialDrag:
                    captured =
                        CaptureTangentialBaseline(
                            measurement);
                    break;

                case RotationInputMode.ControlRotation:
                    captured =
                        CaptureControlRotationBaseline(
                            measurement);
                    break;

                default:
                    captured = false;
                    break;
            }

            if (!captured)
            {
                ResetTracking();
                return;
            }

            _baselineCaptured =
                true;

            _trackingSuspended =
                false;
        }

        private void ReanchorTracking(
            RotationMeasurement measurement)
        {
            if (!TryNormalizeDirection(
                    measurement.Axis,
                    out _trackingAxis))
            {
                return;
            }

            bool captured;

            switch (_inputMode)
            {
                case RotationInputMode.OrbitPosition:
                    captured =
                        CaptureOrbitBaseline(
                            measurement);
                    break;

                case RotationInputMode.TangentialDrag:
                    captured =
                        CaptureTangentialBaseline(
                            measurement);
                    break;

                case RotationInputMode.ControlRotation:
                    captured =
                        CaptureControlRotationBaseline(
                            measurement);
                    break;

                default:
                    captured = false;
                    break;
            }

            if (!captured)
                return;

            _trackingSuspended =
                false;
        }

        private bool CaptureOrbitBaseline(
            RotationMeasurement measurement)
        {
            return TryGetOrbitDirection(
                measurement,
                _resumeTrackingRadius,
                out _previousOrbitDirection);
        }

        private bool CaptureTangentialBaseline(
            RotationMeasurement measurement)
        {
            _previousControlPosition =
                measurement.ControlPosition;

            Vector3 trackingPosition =
                measurement.HasTrackingPoint
                    ? measurement.TrackingPointPosition
                    : measurement.ControlPosition;

            Vector3 radial =
                trackingPosition -
                measurement.PivotPosition;

            radial =
                Vector3.ProjectOnPlane(
                    radial,
                    _trackingAxis);

            if (!IsFinite(radial))
                return false;

            float radius =
                radial.magnitude;

            float requiredRadius =
                Mathf.Max(
                    MinimumDirectionSqrMagnitude,
                    _resumeTrackingRadius);

            if (!IsFinite(radius) ||
                radius < requiredRadius)
            {
                return false;
            }

            _tangentialRadius =
                radius;

            _tangentialBaselineRadial =
                radial /
                radius;

            _tangentialBaselineAngle =
                _accumulatedAngle;

            return
                IsFinite(_tangentialBaselineRadial);
        }

        private bool CaptureControlRotationBaseline(
            RotationMeasurement measurement)
        {
            if (!TryNormalizeRotation(
                    measurement.ControlRotation,
                    out _previousControlRotation))
            {
                return false;
            }

            return true;
        }

        private bool TryGetOrbitDirection(
            RotationMeasurement measurement,
            float requiredRadius,
            out Vector3 direction)
        {
            direction =
                Vector3.zero;

            Vector3 radial =
                measurement.ControlPosition -
                measurement.PivotPosition;

            radial =
                Vector3.ProjectOnPlane(
                    radial,
                    _trackingAxis);

            if (!IsFinite(radial))
                return false;

            float radius =
                radial.magnitude;

            if (!IsFinite(radius) ||
                radius <
                Mathf.Max(
                    MinimumDirectionSqrMagnitude,
                    requiredRadius))
            {
                return false;
            }

            direction =
                radial /
                radius;

            return IsFinite(direction);
        }

        /// <summary>
        /// Получает все данные в одной системе координат.
        ///
        /// Без reference используется Axis Space.
        /// С reference используется система координат
        /// опорной руки.
        /// </summary>
        private bool TryGetMeasurement(
            ManipulationFrame frame,
            out RotationMeasurement measurement)
        {
            measurement =
                default;

            if (!IsFinite(frame.ControlPose.position) ||
                !TryNormalizeRotation(
                    frame.ControlPose.rotation,
                    out Quaternion controlWorldRotation))
            {
                return false;
            }

            Vector3 pivotWorldPosition =
                GetPivotPosition();

            if (!IsFinite(pivotWorldPosition))
                return false;

            if (!frame.HasReference)
            {
                return TryGetMeasurementWithoutReference(
                    frame.ControlPose.position,
                    controlWorldRotation,
                    pivotWorldPosition,
                    out measurement);
            }

            return TryGetMeasurementWithReference(
                frame,
                controlWorldRotation,
                pivotWorldPosition,
                out measurement);
        }

        private bool TryGetMeasurementWithoutReference(
            Vector3 controlWorldPosition,
            Quaternion controlWorldRotation,
            Vector3 pivotWorldPosition,
            out RotationMeasurement measurement)
        {
            measurement =
                default;

            Transform space =
                GetMeasurementSpace();

            if (space == null)
                return false;

            if (!TryNormalizeRotation(
                    space.rotation,
                    out Quaternion spaceRotation))
            {
                return false;
            }

            Quaternion inverseSpaceRotation =
                Quaternion.Inverse(
                    spaceRotation);

            Vector3 spaceOrigin =
                space.position;

            Vector3 controlPosition =
                inverseSpaceRotation *
                (
                    controlWorldPosition -
                    spaceOrigin
                );

            Quaternion controlRotation =
                inverseSpaceRotation *
                controlWorldRotation;

            Vector3 pivotPosition =
                inverseSpaceRotation *
                (
                    pivotWorldPosition -
                    spaceOrigin
                );

            if (!TryNormalizeDirection(
                    _localAxis,
                    out Vector3 axis))
            {
                return false;
            }

            bool hasTrackingPoint =
                _trackingPoint != null;

            Vector3 trackingPointPosition =
                Vector3.zero;

            if (hasTrackingPoint)
            {
                trackingPointPosition =
                    inverseSpaceRotation *
                    (
                        _trackingPoint.position -
                        spaceOrigin
                    );
            }

            if (!IsFinite(controlPosition) ||
                !IsFinite(controlRotation) ||
                !IsFinite(pivotPosition) ||
                !IsFinite(trackingPointPosition))
            {
                return false;
            }

            measurement =
                new RotationMeasurement(
                    controlPosition,
                    controlRotation,
                    pivotPosition,
                    axis,
                    trackingPointPosition,
                    hasTrackingPoint);

            return true;
        }

        private bool TryGetMeasurementWithReference(
            ManipulationFrame frame,
            Quaternion controlWorldRotation,
            Vector3 pivotWorldPosition,
            out RotationMeasurement measurement)
        {
            measurement =
                default;

            if (!TryNormalizeRotation(
                    frame.ReferencePose.rotation,
                    out Quaternion referenceRotation))
            {
                return false;
            }

            Quaternion inverseReferenceRotation =
                Quaternion.Inverse(
                    referenceRotation);

            Vector3 referencePosition =
                frame.ReferencePose.position;

            Vector3 controlPosition =
                inverseReferenceRotation *
                (
                    frame.ControlPose.position -
                    referencePosition
                );

            Quaternion controlRotation =
                inverseReferenceRotation *
                controlWorldRotation;

            if (!_referenceFrameCaptured)
            {
                Vector3 worldAxis =
                    GetWorldAxis();

                if (!TryNormalizeDirection(
                        worldAxis,
                        out worldAxis))
                {
                    return false;
                }

                _referenceLocalPivot =
                    inverseReferenceRotation *
                    (
                        pivotWorldPosition -
                        referencePosition
                    );

                _referenceLocalAxis =
                    inverseReferenceRotation *
                    worldAxis;

                if (!TryNormalizeDirection(
                        _referenceLocalAxis,
                        out _referenceLocalAxis))
                {
                    return false;
                }

                _referenceFrameCaptured =
                    true;
            }

            bool hasTrackingPoint =
                _trackingPoint != null;

            Vector3 trackingPointPosition =
                Vector3.zero;

            if (hasTrackingPoint)
            {
                trackingPointPosition =
                    inverseReferenceRotation *
                    (
                        _trackingPoint.position -
                        referencePosition
                    );
            }

            if (!IsFinite(controlPosition) ||
                !IsFinite(controlRotation) ||
                !IsFinite(_referenceLocalPivot) ||
                !IsFinite(_referenceLocalAxis) ||
                !IsFinite(trackingPointPosition))
            {
                return false;
            }

            measurement =
                new RotationMeasurement(
                    controlPosition,
                    controlRotation,
                    _referenceLocalPivot,
                    _referenceLocalAxis,
                    trackingPointPosition,
                    hasTrackingPoint);

            return true;
        }

        /// <summary>
        /// Выделяет из изменения ориентации только вращение
        /// вокруг заданной оси.
        /// </summary>
        private static bool TryGetSignedTwistDelta(
            Quaternion previousRotation,
            Quaternion currentRotation,
            Vector3 axis,
            out float angle)
        {
            angle =
                0f;

            if (!TryNormalizeRotation(
                    previousRotation,
                    out previousRotation) ||
                !TryNormalizeRotation(
                    currentRotation,
                    out currentRotation) ||
                !TryNormalizeDirection(
                    axis,
                    out axis))
            {
                return false;
            }

            /*
             * Quaternion q и -q описывают одинаковое вращение.
             *
             * Выбираем ближайшее представление,
             * чтобы избежать случайного скачка на 360 градусов.
             */
            if (Quaternion.Dot(
                    previousRotation,
                    currentRotation) < 0f)
            {
                currentRotation =
                    new Quaternion(
                        -currentRotation.x,
                        -currentRotation.y,
                        -currentRotation.z,
                        -currentRotation.w);
            }

            Quaternion deltaRotation =
                currentRotation *
                Quaternion.Inverse(
                    previousRotation);

            if (!TryNormalizeRotation(
                    deltaRotation,
                    out deltaRotation))
            {
                return false;
            }

            Vector3 deltaVector =
                new Vector3(
                    deltaRotation.x,
                    deltaRotation.y,
                    deltaRotation.z);

            Vector3 projectedVector =
                Vector3.Project(
                    deltaVector,
                    axis);

            Quaternion twist =
                new Quaternion(
                    projectedVector.x,
                    projectedVector.y,
                    projectedVector.z,
                    deltaRotation.w);

            if (!TryNormalizeRotation(
                    twist,
                    out twist))
            {
                /*
                 * Чистый swing на 180 градусов может не иметь
                 * определённой twist-компоненты.
                 */
                return false;
            }

            float signedSinHalfAngle =
                Vector3.Dot(
                    new Vector3(
                        twist.x,
                        twist.y,
                        twist.z),
                    axis);

            float angleRadians =
                2f *
                Mathf.Atan2(
                    signedSinHalfAngle,
                    twist.w);

            angle =
                angleRadians *
                Mathf.Rad2Deg;

            angle =
                Mathf.DeltaAngle(
                    0f,
                    angle);

            return IsFinite(angle);
        }

        private void SuspendTracking()
        {
            if (!_baselineCaptured)
                return;

            _trackingSuspended =
                true;
        }

        private void ResetTracking()
        {
            _accumulatedAngle =
                0f;

            _trackingAxis =
                Vector3.zero;

            _baselineCaptured =
                false;

            _trackingSuspended =
                false;

            _capturedInputMode =
                _inputMode;

            _previousOrbitDirection =
                Vector3.zero;

            _previousControlPosition =
                Vector3.zero;

            _tangentialBaselineRadial =
                Vector3.zero;

            _tangentialRadius =
                0f;

            _tangentialBaselineAngle =
                0f;

            _previousControlRotation =
                Quaternion.identity;

            _referenceFrameCaptured =
                false;

            _referenceLocalPivot =
                Vector3.zero;

            _referenceLocalAxis =
                Vector3.zero;
        }

        private float AngleToValue(
            float angle)
        {
            float range =
                _maximumAngle -
                _minimumAngle;

            if (!IsFinite(range) ||
                Mathf.Abs(range) <
                MinimumAngleRange)
            {
                return Value;
            }

            float normalized =
                (
                    angle -
                    _minimumAngle
                ) /
                range;

            return Mathf.Clamp01(
                normalized);
        }

        private float ValueToAngle(
            float value)
        {
            return Mathf.Lerp(
                _minimumAngle,
                _maximumAngle,
                Mathf.Clamp01(value));
        }

        private float ClampAngleToRange(
            float angle)
        {
            float lowerAngle =
                Mathf.Min(
                    _minimumAngle,
                    _maximumAngle);

            float upperAngle =
                Mathf.Max(
                    _minimumAngle,
                    _maximumAngle);

            return Mathf.Clamp(
                angle,
                lowerAngle,
                upperAngle);
        }

        private Vector3 GetPivotPosition()
        {
            return _pivot != null
                ? _pivot.position
                : transform.position;
        }

        private Transform GetMeasurementSpace()
        {
            if (_axisSpace != null)
                return _axisSpace;

            if (_pivot != null &&
                _pivot.parent != null)
            {
                return _pivot.parent;
            }

            if (transform.parent != null)
                return transform.parent;

            return transform;
        }

        private Vector3 GetWorldAxis()
        {
            Transform space =
                GetMeasurementSpace();

            Vector3 worldAxis =
                space.TransformDirection(
                    _localAxis);

            if (!TryNormalizeDirection(
                    worldAxis,
                    out worldAxis))
            {
                return space.up;
            }

            return worldAxis;
        }

        private static bool TryNormalizeDirection(
            Vector3 direction,
            out Vector3 normalized)
        {
            normalized =
                Vector3.zero;

            if (!IsFinite(direction))
                return false;

            float sqrMagnitude =
                direction.sqrMagnitude;

            if (!IsFinite(sqrMagnitude) ||
                sqrMagnitude <
                MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            float inverseMagnitude =
                1f /
                Mathf.Sqrt(
                    sqrMagnitude);

            if (!IsFinite(inverseMagnitude))
                return false;

            normalized =
                direction *
                inverseMagnitude;

            return IsFinite(normalized);
        }

        private static bool TryNormalizeRotation(
            Quaternion rotation,
            out Quaternion normalized)
        {
            normalized =
                Quaternion.identity;

            if (!IsFinite(rotation))
                return false;

            float sqrMagnitude =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;

            if (!IsFinite(sqrMagnitude) ||
                sqrMagnitude <
                MinimumQuaternionSqrMagnitude)
            {
                return false;
            }

            float inverseMagnitude =
                1f /
                Mathf.Sqrt(
                    sqrMagnitude);

            if (!IsFinite(inverseMagnitude))
                return false;

            normalized =
                new Quaternion(
                    rotation.x * inverseMagnitude,
                    rotation.y * inverseMagnitude,
                    rotation.z * inverseMagnitude,
                    rotation.w * inverseMagnitude);

            return IsFinite(normalized);
        }

        private static bool IsFinite(
            Quaternion rotation)
        {
            return
                IsFinite(rotation.x) &&
                IsFinite(rotation.y) &&
                IsFinite(rotation.z) &&
                IsFinite(rotation.w);
        }

        private static bool IsFinite(
            Vector3 vector)
        {
            return
                IsFinite(vector.x) &&
                IsFinite(vector.y) &&
                IsFinite(vector.z);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}