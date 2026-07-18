using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// Преобразует линейное движение управляющей точки
    /// в нормализованное значение 0..1.
    ///
    /// При наличии reference проекция считается относительно
    /// reference-точки, но в текущей системе координат механизма.
    /// Поэтому общее перемещение и вращение предмета не должны
    /// изменять значение механизма.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class LinearInteraction
        : BasePoseValueInteraction
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        private const float MinimumDistanceRange =
            0.000001f;

        [Header("Linear Frame")]

        [Tooltip(
            "Неподвижное начало линейного механизма. " +
            "Не должно быть Target объекта LinearDriver.")]
        [SerializeField]
        private Transform _origin;

        [Tooltip(
            "Система координат оси движения. " +
            "Должна вращаться вместе с корнем инструмента.")]
        [SerializeField]
        private Transform _axisSpace;

        [Tooltip(
            "Ось движения в локальной системе Axis Space.")]
        [SerializeField]
        private Vector3 _localAxis =
            Vector3.right;

        [Header("Linear Range")]

        [SerializeField]
        private float _minimumDistance;

        [SerializeField]
        private float _maximumDistance = 1f;

        [SerializeField]
        private bool _invertInput;

        private float _startCoordinate;
        private float _startDistance;
        private bool _baselineCaptured;

        private void LateUpdate()
        {
            if (!TryGetManipulationFrame(
                    out ManipulationFrame frame))
            {
                ResetBaseline();
                return;
            }

            if (!TryGetCoordinate(
                    frame,
                    out float coordinate))
            {
                ResetBaseline();
                return;
            }

            /*
             * В первый валидный кадр сохраняем:
             * - текущую координату руки;
             * - текущее положение механизма.
             *
             * Благодаря этому при начале grab
             * каретка не телепортируется.
             */
            if (!_baselineCaptured)
            {
                CaptureBaseline(coordinate);
                return;
            }

            float coordinateDelta =
                coordinate -
                _startCoordinate;

            float distance =
                _startDistance +
                coordinateDelta;

            TrySetValueFromInteraction(
                DistanceToValue(distance));
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
                _localAxis =
                    Vector3.right;
            }

            if (Mathf.Abs(
                    _maximumDistance -
                    _minimumDistance) <
                MinimumDistanceRange)
            {
                _maximumDistance =
                    _minimumDistance +
                    0.001f;
            }
        }

        /// <summary>
        /// Получает положение управляющей руки вдоль оси механизма.
        ///
        /// Важно: относительный вектор переводится в текущую
        /// систему координат Axis Space. Поэтому при вращении
        /// всего предмета проекция сохраняется.
        /// </summary>
        private bool TryGetCoordinate(
            ManipulationFrame frame,
            out float coordinate)
        {
            coordinate = 0f;

            Transform axisSpace =
                GetAxisSpace();

            if (axisSpace == null)
                return false;

            Vector3 normalizedLocalAxis =
                GetNormalizedLocalAxis();

            if (normalizedLocalAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                return false;
            }

            Vector3 relativeWorldPosition;

            if (frame.HasReference)
            {
                /*
                 * Убираем общее перемещение предмета.
                 *
                 * Нас интересует положение управляющей руки
                 * относительно руки, удерживающей корпус.
                 */
                relativeWorldPosition =
                    frame.ControlPose.position -
                    frame.ReferencePose.position;
            }
            else
            {
                /*
                 * Для обычного одноручного линейного механизма
                 * считаем положение относительно Origin.
                 */
                relativeWorldPosition =
                    frame.ControlPose.position -
                    GetOriginPosition();
            }

            /*
             * Переводим относительный вектор в текущую
             * систему координат механизма.
             *
             * Используем только rotation, намеренно игнорируя scale,
             * чтобы масштаб объекта не изменял чувствительность.
             */
            Vector3 relativeLocalPosition =
                Quaternion.Inverse(axisSpace.rotation) *
                relativeWorldPosition;

            coordinate =
                Vector3.Dot(
                    relativeLocalPosition,
                    normalizedLocalAxis);

            return true;
        }

        private void CaptureBaseline(
            float coordinate)
        {
            _startCoordinate =
                coordinate;

            _startDistance =
                ValueToDistance(Value);

            _baselineCaptured =
                true;
        }

        private void ResetBaseline()
        {
            _baselineCaptured =
                false;

            _startCoordinate =
                0f;

            _startDistance =
                0f;
        }

        private float DistanceToValue(
            float distance)
        {
            float normalized =
                Mathf.InverseLerp(
                    _minimumDistance,
                    _maximumDistance,
                    distance);

            if (_invertInput)
                normalized = 1f - normalized;

            return normalized;
        }

        private float ValueToDistance(
            float value)
        {
            float normalized =
                Mathf.Clamp01(value);

            if (_invertInput)
                normalized = 1f - normalized;

            return Mathf.Lerp(
                _minimumDistance,
                _maximumDistance,
                normalized);
        }

        private Transform GetAxisSpace()
        {
            if (_axisSpace != null)
                return _axisSpace;

            if (_origin != null)
                return _origin;

            return transform;
        }

        private Vector3 GetNormalizedLocalAxis()
        {
            if (_localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                return Vector3.right;
            }

            return _localAxis.normalized;
        }

        private Vector3 GetOriginPosition()
        {
            return _origin != null
                ? _origin.position
                : transform.position;
        }
    }
}