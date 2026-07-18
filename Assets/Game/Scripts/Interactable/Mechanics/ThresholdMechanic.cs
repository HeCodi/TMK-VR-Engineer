using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Game.Scripts.Interactable.Mechanics
{
    [Serializable]
    public sealed class ThresholdStateUnityEvent
        : UnityEvent<bool>
    {
    }

    /// <summary>
    /// Преобразует непрерывный Value 0..1
    /// в дискретное состояние true/false.
    ///
    /// Использует два разных порога для защиты
    /// от дрожания около точки переключения.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(220)]
    public sealed class ThresholdMechanic
        : BaseValueMechanic,
        IBooleanStateSource
    {
        [Header("Thresholds")]

        [Tooltip(
            "При достижении этого значения состояние включается.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _activateThreshold = 0.8f;

        [Tooltip(
            "При падении до этого значения состояние выключается.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _deactivateThreshold = 0.6f;

        [Header("Threshold Events")]

        [SerializeField]
        private UnityEvent _onActivated =
            new UnityEvent();

        [SerializeField]
        private UnityEvent _onDeactivated =
            new UnityEvent();

        [SerializeField]
        private ThresholdStateUnityEvent _onStateChanged =
            new ThresholdStateUnityEvent();

        private bool _isActivated;
        private bool _stateInitialized;

        public event Action Activated;
        public event Action Deactivated;
        public event Action<bool> StateChanged;

        public bool IsActivated => _isActivated;

        public bool IsActive => IsActivated;

        protected override void OnEnable()
        {
            _stateInitialized = false;

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _stateInitialized = false;

            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            _activateThreshold =
                Mathf.Clamp01(_activateThreshold);

            _deactivateThreshold =
                Mathf.Clamp(
                    _deactivateThreshold,
                    0f,
                    _activateThreshold);
        }

        protected override void OnSourceSynchronized()
        {
            /*
             * Начальное состояние выставляется без событий.
             */
            _isActivated =
                Value >= _activateThreshold;

            _stateInitialized = true;
        }

        protected override void OnValueUpdated(
            float previousValue,
            float currentValue)
        {
            base.OnValueUpdated(
                previousValue,
                currentValue);

            if (!_stateInitialized)
                return;

            EvaluateState(currentValue);
        }

        public void ResetStateWithoutNotify(bool activated)
        {
            _isActivated = activated;
            _stateInitialized = true;
        }

        private void EvaluateState(float value)
        {
            if (!_isActivated &&
                value >= _activateThreshold)
            {
                SetActivated(true);
                return;
            }

            if (_isActivated &&
                value <= _deactivateThreshold)
            {
                SetActivated(false);
            }
        }

        private void SetActivated(bool activated)
        {
            if (_isActivated == activated)
                return;

            _isActivated = activated;

            StateChanged?.Invoke(_isActivated);
            _onStateChanged?.Invoke(_isActivated);

            if (_isActivated)
            {
                Activated?.Invoke();
                _onActivated?.Invoke();
            }
            else
            {
                Deactivated?.Invoke();
                _onDeactivated?.Invoke();
            }
        }
    }
}