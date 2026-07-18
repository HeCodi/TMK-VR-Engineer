using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    [Serializable]
    public sealed class NormalizedValueEvent : UnityEvent<float>
    {
    }

    /// <summary>
    /// Базовый источник нормализованного значения 0..1.
    ///
    /// Класс не знает, откуда поступает значение:
    /// рука, палец, физический контакт, таймер или внешний код.
    /// </summary>
    public abstract class BaseValueInteraction : MonoBehaviour
    {
        private const float ValueEpsilon = 0.00001f;

        [Header("Value")]

        [SerializeField]
        [Range(0f, 1f)]
        private float _initialValue;

        [SerializeField]
        private NormalizedValueEvent _onValueChanged =
            new NormalizedValueEvent();

        [Header("Interaction Events")]

        [SerializeField]
        private UnityEvent _onInteractionStarted =
            new UnityEvent();

        [SerializeField]
        private UnityEvent _onInteractionEnded =
            new UnityEvent();

        private float _value;
        private bool _valueInitialized;
        private bool _isInteracting;

        /// <summary>
        /// Вызывается при изменении Value.
        /// </summary>
        public event Action<float> ValueChanged;

        /// <summary>
        /// Вызывается при начале управления механизмом.
        /// </summary>
        public event Action InteractionStarted;

        /// <summary>
        /// Вызывается при окончании управления механизмом.
        /// </summary>
        public event Action InteractionEnded;

        /// <summary>
        /// Текущее нормализованное значение 0..1.
        /// </summary>
        public float Value
        {
            get
            {
                EnsureValueInitialized();
                return _value;
            }
        }

        /// <summary>
        /// Управляет ли пользователь механизмом прямо сейчас.
        /// </summary>
        public bool IsInteracting => _isInteracting;

        protected virtual void Awake()
        {
            EnsureValueInitialized();
        }

        protected virtual void OnDisable()
        {
            SetInteractionActive(false);
        }

        protected virtual void OnValidate()
        {
            _initialValue =
                Mathf.Clamp01(_initialValue);
        }

        /// <summary>
        /// Устанавливает значение с вызовом событий.
        /// </summary>
        public void SetValue(float value)
        {
            SetValueInternal(
                value,
                notify: true);
        }

        /// <summary>
        /// Устанавливает значение без вызова событий.
        /// </summary>
        public void SetValueWithoutNotify(float value)
        {
            SetValueInternal(
                value,
                notify: false);
        }

        /// <summary>
        /// Используется производными Interaction-компонентами.
        /// Возвращает true, если значение действительно изменилось.
        /// </summary>
        protected bool TrySetValueFromInteraction(float value)
        {
            return SetValueInternal(
                value,
                notify: true);
        }

        /// <summary>
        /// Изменяет состояние активного взаимодействия.
        ///
        /// Protected, чтобы BaseManipulatorValueInteraction
        /// и будущий PokeButtonInteraction могли вызывать этот метод.
        /// </summary>
        protected void SetInteractionActive(bool active)
        {
            if (_isInteracting == active)
                return;

            _isInteracting = active;

            if (_isInteracting)
            {
                OnInteractionStarted();

                InteractionStarted?.Invoke();
                _onInteractionStarted?.Invoke();
            }
            else
            {
                OnInteractionEnded();

                InteractionEnded?.Invoke();
                _onInteractionEnded?.Invoke();
            }
        }

        /// <summary>
        /// Вызывается после изменения Value,
        /// но перед публичными событиями.
        /// </summary>
        protected virtual void OnValueUpdated(
            float previousValue,
            float currentValue)
        {
        }

        /// <summary>
        /// Внутренний callback начала взаимодействия.
        /// </summary>
        protected virtual void OnInteractionStarted()
        {
        }

        /// <summary>
        /// Внутренний callback окончания взаимодействия.
        /// </summary>
        protected virtual void OnInteractionEnded()
        {
        }

        private void EnsureValueInitialized()
        {
            if (_valueInitialized)
                return;

            _value =
                Mathf.Clamp01(_initialValue);

            _valueInitialized = true;
        }

        private bool SetValueInternal(
            float value,
            bool notify)
        {
            EnsureValueInitialized();

            float clampedValue =
                Mathf.Clamp01(value);

            if (Mathf.Abs(
                    clampedValue - _value) <=
                ValueEpsilon)
            {
                return false;
            }

            float previousValue =
                _value;

            _value =
                clampedValue;

            OnValueUpdated(
                previousValue,
                _value);

            if (notify)
            {
                ValueChanged?.Invoke(_value);
                _onValueChanged?.Invoke(_value);
            }

            return true;
        }
    }
}