using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Game.Scripts.Interactable.Mechanics
{
    /// <summary>
    /// Пока пользователь управляет Source, значение следует за ним.
    ///
    /// После окончания взаимодействия значение возвращается
    /// к Return Value.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(230)]
    public sealed class SpringReturnMechanic
        : BaseValueMechanic
    {
        private const float MinimumSpeed = 0.0001f;
        private const float ReturnEpsilon = 0.0001f;

        [Header("Spring Return")]

        [SerializeField]
        [Range(0f, 1f)]
        private float _returnValue;

        [Tooltip(
            "Скорость возврата в единицах Value в секунду.")]
        [SerializeField]
        [Min(MinimumSpeed)]
        private float _returnSpeed = 6f;

        [Tooltip(
            "Возвращать механизм при включении сцены, " +
            "если он сейчас не управляется.")]
        [SerializeField]
        private bool _returnOnEnable = true;

        [Header("Return Events")]

        [SerializeField]
        private UnityEvent _onReturnStarted =
            new UnityEvent();

        [SerializeField]
        private UnityEvent _onReturnCompleted =
            new UnityEvent();

        private bool _isReturning;

        public event Action ReturnStarted;
        public event Action ReturnCompleted;

        public bool IsReturning => _isReturning;

        public float ReturnValue => _returnValue;

        protected override void OnEnable()
        {
            _isReturning = false;

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _isReturning = false;

            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            _returnValue =
                Mathf.Clamp01(_returnValue);

            _returnSpeed =
                Mathf.Max(
                    MinimumSpeed,
                    _returnSpeed);
        }

        protected override void OnSourceSynchronized()
        {
            if (Source == null ||
                Source.IsInteracting)
            {
                return;
            }

            if (_returnOnEnable)
                BeginReturn();
        }

        protected override void OnSourceValueChanged(float value)
        {
            if (Source != null &&
                Source.IsInteracting)
            {
                CancelReturn();

                TrySetValueFromInteraction(value);
                return;
            }

            /*
             * Источник изменили программно,
             * когда пользователь его не держит.
             */
            TrySetValueFromInteraction(value);
            BeginReturn();
        }

        protected override void OnSourceInteractionStarted()
        {
            CancelReturn();

            /*
             * Возвращаем фактическое визуальное значение обратно
             * в Interaction до захвата нового baseline.
             */
            WriteValueBackToSource(Value);

            SetInteractionActive(true);
        }

        protected override void OnSourceInteractionEnded()
        {
            SetInteractionActive(false);

            BeginReturn();
        }

        private void Update()
        {
            if (!_isReturning)
                return;

            float nextValue =
                Mathf.MoveTowards(
                    Value,
                    _returnValue,
                    _returnSpeed * Time.deltaTime);

            TrySetValueFromInteraction(nextValue);

            if (Mathf.Abs(
                    Value - _returnValue) >
                ReturnEpsilon)
            {
                return;
            }

            CompleteReturn();
        }

        public void BeginReturn()
        {
            if (Source != null &&
                Source.IsInteracting)
            {
                return;
            }

            if (Mathf.Abs(
                    Value - _returnValue) <=
                ReturnEpsilon)
            {
                TrySetValueFromInteraction(
                    _returnValue);

                WriteValueBackToSource(
                    _returnValue);

                _isReturning = false;
                return;
            }

            if (_isReturning)
                return;

            _isReturning = true;

            ReturnStarted?.Invoke();
            _onReturnStarted?.Invoke();
        }

        public void ReturnImmediately()
        {
            bool wasAwayFromTarget =
                Mathf.Abs(
                    Value - _returnValue) >
                ReturnEpsilon;

            _isReturning = false;

            TrySetValueFromInteraction(
                _returnValue);

            WriteValueBackToSource(
                _returnValue);

            if (!wasAwayFromTarget)
                return;

            ReturnCompleted?.Invoke();
            _onReturnCompleted?.Invoke();
        }

        private void CancelReturn()
        {
            _isReturning = false;
        }

        private void CompleteReturn()
        {
            TrySetValueFromInteraction(
                _returnValue);

            WriteValueBackToSource(
                _returnValue);

            _isReturning = false;

            ReturnCompleted?.Invoke();
            _onReturnCompleted?.Invoke();
        }
    }
}