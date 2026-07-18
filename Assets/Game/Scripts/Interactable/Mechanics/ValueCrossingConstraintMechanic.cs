using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Game.Scripts.Interactable.Mechanics
{
    /// <summary>
    /// Ограничивает пересечение заданной границы внутри Value 0..1.
    ///
    /// Значение свободно двигается по обе стороны границы.
    /// Но переход через границу в указанном направлении
    /// требует активного Permission Source.
    ///
    /// Пример двери:
    ///
    /// Source:
    ///     RotationInteraction двери.
    ///
    /// Barrier Value:
    ///     Закрытое положение двери.
    ///
    /// Blocked Direction:
    ///     Increasing.
    ///
    /// Permission Source:
    ///     ThresholdMechanic ручки.
    ///
    /// Тогда дверь можно закрыть всегда,
    /// но открыть можно только при повёрнутой ручке.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(230)]
    public sealed class ValueCrossingConstraintMechanic
        : BaseValueMechanic
    {
        private const float ValueEpsilon = 0.00001f;

        public enum CrossingDirection
        {
            Increasing,
            Decreasing,
            Both
        }

        [Header("Crossing Constraint")]

        [Tooltip(
            "Граница внутри диапазона 0..1. " +
            "При запрещённом пересечении итоговое значение " +
            "останавливается на этой точке.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _barrierValue = 0f;

        [Tooltip(
            "Какое направление пересечения требует разрешения.\n\n" +
            "Increasing — переход в сторону увеличения Value.\n" +
            "Decreasing — переход в сторону уменьшения Value.\n" +
            "Both — переход в обе стороны.")]
        [SerializeField]
        private CrossingDirection _blockedDirection =
            CrossingDirection.Increasing;

        [Header("Permission")]

        [Tooltip(
            "Компонент, предоставляющий логическое разрешение. " +
            "Он должен реализовывать IBooleanStateSource. " +
            "Например ThresholdMechanic ручки.")]
        [SerializeField]
        private MonoBehaviour _permissionSourceBehaviour;

        [Header("Events")]

        [Tooltip(
            "Вызывается при первой попытке пересечь " +
            "закрытую границу. Подходит для звука удара " +
            "о защёлку или вибрации.")]
        [SerializeField]
        private UnityEvent _onBlocked = new UnityEvent();

        private IBooleanStateSource _permissionSource;

        /*
         * Сырое предыдущее значение Source.
         *
         * Нужно для обработки изменения через delta.
         * Благодаря этому дверь не прыгает после того,
         * как ручка разрешила открытие.
         */
        private float _previousSourceValue;

        /*
         * Не позволяет вызывать OnBlocked каждый кадр,
         * пока пользователь продолжает давить на защёлку.
         */
        private bool _blockedAttemptActive;

        private bool _permissionSubscribed;

        public float BarrierValue => _barrierValue;

        public CrossingDirection BlockedDirection =>
            _blockedDirection;

        public bool IsPermissionGranted =>
            _permissionSource != null &&
            _permissionSource.IsActive;

        public bool IsBlockedAttemptActive =>
            _blockedAttemptActive;

        public event Action Blocked;

        protected override void OnEnable()
        {
            if (!ResolvePermissionSource(logError: true))
            {
                enabled = false;
                return;
            }

            SubscribePermissionSource();

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            UnsubscribePermissionSource();

            _blockedAttemptActive = false;

            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            _barrierValue =
                Mathf.Clamp01(_barrierValue);

            if (_permissionSourceBehaviour == this)
                _permissionSourceBehaviour = null;
        }

        protected override void OnSourceSynchronized()
        {
            /*
             * BaseValueMechanic уже скопировал Source.Value
             * в Value без уведомления.
             */
            _previousSourceValue =
                Mathf.Clamp01(Source.Value);

            _blockedAttemptActive = false;
        }

        protected override void OnSourceValueChanged(float sourceValue)
        {
            sourceValue =
                Mathf.Clamp01(sourceValue);

            /*
             * Берём не абсолютное значение Source,
             * а только его изменение.
             *
             * Например:
             *
             * Source продолжил движение до 0.8,
             * но дверь упёрлась в барьер на 0.02.
             *
             * После разрешения дверь не прыгнет сразу на 0.8.
             * Она продолжит двигаться только на новое изменение руки.
             */
            float sourceDelta =
                sourceValue -
                _previousSourceValue;

            /*
             * Предыдущее сырое значение обновляется всегда,
             * даже когда движение было заблокировано.
             */
            _previousSourceValue =
                sourceValue;

            if (Mathf.Abs(sourceDelta) <= ValueEpsilon)
                return;

            float currentValue =
                Value;

            float candidateValue =
                Mathf.Clamp01(
                    currentValue +
                    sourceDelta);

            bool wasBlocked =
                TryApplyConstraint(
                    currentValue,
                    candidateValue,
                    out float constrainedValue);

            TrySetValueFromInteraction(
                constrainedValue);

            UpdateBlockedAttempt(
                wasBlocked);
        }

        protected override void OnSourceInteractionStarted()
        {
            /*
             * Новый захват начинает расчёт с текущего
             * сырого положения источника.
             */
            _previousSourceValue =
                Mathf.Clamp01(Source.Value);

            _blockedAttemptActive = false;

            base.OnSourceInteractionStarted();
        }

        protected override void OnSourceInteractionEnded()
        {
            /*
             * Если Source продолжил двигаться за границей,
             * а итоговый Mechanic остался на барьере,
             * синхронизируем Source при отпускании.
             *
             * Пока пользователь держит объект, этого делать нельзя:
             * иначе после открытия разрешения могла бы появиться
             * накопленная дельта и скачок.
             */
            if (Mathf.Abs(Source.Value - Value) >
                ValueEpsilon)
            {
                WriteValueBackToSource(Value);

                _previousSourceValue =
                    Value;
            }

            _blockedAttemptActive = false;

            base.OnSourceInteractionEnded();
        }

        /// <summary>
        /// Принудительно синхронизирует Source
        /// с текущим итоговым Value.
        ///
        /// Может пригодиться после загрузки сохранения,
        /// телепорта значения или программного изменения двери.
        /// </summary>
        public void SynchronizeSourceWithOutput()
        {
            WriteValueBackToSource(Value);

            _previousSourceValue =
                Value;

            _blockedAttemptActive = false;
        }

        private bool TryApplyConstraint(
            float currentValue,
            float candidateValue,
            out float constrainedValue)
        {
            constrainedValue =
                candidateValue;

            /*
             * Когда разрешение активно,
             * никакой блокировки нет.
             */
            if (IsPermissionGranted)
                return false;

            bool increasingCrossing =
                IsIncreasingCrossing(
                    currentValue,
                    candidateValue);

            bool decreasingCrossing =
                IsDecreasingCrossing(
                    currentValue,
                    candidateValue);

            bool shouldBlock;

            switch (_blockedDirection)
            {
                case CrossingDirection.Increasing:
                    shouldBlock =
                        increasingCrossing;
                    break;

                case CrossingDirection.Decreasing:
                    shouldBlock =
                        decreasingCrossing;
                    break;

                case CrossingDirection.Both:
                    shouldBlock =
                        increasingCrossing ||
                        decreasingCrossing;
                    break;

                default:
                    shouldBlock = false;
                    break;
            }

            if (!shouldBlock)
                return false;

            /*
             * Останавливаемся ровно на границе,
             * а не возвращаемся в начало диапазона.
             */
            constrainedValue =
                _barrierValue;

            return true;
        }

        private bool IsIncreasingCrossing(
            float currentValue,
            float candidateValue)
        {
            /*
             * Значение должно действительно увеличиваться.
             */
            if (candidateValue <=
                currentValue + ValueEpsilon)
            {
                return false;
            }

            /*
             * Текущее положение находится на нижней стороне
             * или точно на границе, а следующее пытается
             * попасть на верхнюю сторону.
             */
            return
                currentValue <=
                _barrierValue + ValueEpsilon &&
                candidateValue >
                _barrierValue + ValueEpsilon;
        }

        private bool IsDecreasingCrossing(
            float currentValue,
            float candidateValue)
        {
            /*
             * Значение должно действительно уменьшаться.
             */
            if (candidateValue >=
                currentValue - ValueEpsilon)
            {
                return false;
            }

            /*
             * Текущее положение находится на верхней стороне
             * или точно на границе, а следующее пытается
             * попасть на нижнюю сторону.
             */
            return
                currentValue >=
                _barrierValue - ValueEpsilon &&
                candidateValue <
                _barrierValue - ValueEpsilon;
        }

        private void UpdateBlockedAttempt(bool wasBlocked)
        {
            if (!wasBlocked)
            {
                /*
                 * Пользователь отступил от барьера
                 * или теперь двигается в разрешённую сторону.
                 *
                 * Следующая новая попытка пересечения
                 * снова сможет вызвать событие.
                 */
                _blockedAttemptActive = false;
                return;
            }

            if (_blockedAttemptActive)
                return;

            _blockedAttemptActive = true;

            Blocked?.Invoke();
            _onBlocked?.Invoke();
        }

        private void HandlePermissionStateChanged(bool isActive)
        {
            /*
             * При открытии разрешения дверь сама не двигается.
             * Движение продолжится только от следующего
             * изменения основного Source.
             */
            if (isActive)
                _blockedAttemptActive = false;
        }

        private bool ResolvePermissionSource(bool logError)
        {
            _permissionSource =
                _permissionSourceBehaviour
                    as IBooleanStateSource;

            bool valid =
                _permissionSourceBehaviour != null &&
                _permissionSource != null &&
                _permissionSourceBehaviour != this;

            if (valid)
                return true;

            if (!logError)
                return false;

            if (_permissionSourceBehaviour == null)
            {
                Debug.LogError(
                    $"{nameof(ValueCrossingConstraintMechanic)} " +
                    $"on '{name}' requires a Permission Source.",
                    this);

                return false;
            }

            Debug.LogError(
                $"{nameof(ValueCrossingConstraintMechanic)} " +
                $"on '{name}' requires Permission Source " +
                $"to implement {nameof(IBooleanStateSource)}.",
                this);

            return false;
        }

        private void SubscribePermissionSource()
        {
            if (_permissionSubscribed ||
                _permissionSource == null)
            {
                return;
            }

            _permissionSource.StateChanged +=
                HandlePermissionStateChanged;

            _permissionSubscribed = true;
        }

        private void UnsubscribePermissionSource()
        {
            if (!_permissionSubscribed ||
                _permissionSource == null)
            {
                return;
            }

            _permissionSource.StateChanged -=
                HandlePermissionStateChanged;

            _permissionSubscribed = false;
        }
    }
}