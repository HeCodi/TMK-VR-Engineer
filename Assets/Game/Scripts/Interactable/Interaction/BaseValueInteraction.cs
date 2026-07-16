using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    [Serializable]
    public sealed class NormalizedValueEvent : UnityEvent<float>
    {
    }

    /// <summary>
    /// База для ограниченных механических интеракций:
    /// - LinearInteraction;
    /// - RotationInteraction;
    /// - будущих ButtonInteraction и LeverInteraction.
    ///
    /// Использует XRSimpleInteractable только как источник Select.
    /// Сам XRSimpleInteractable ничего не перемещает.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public abstract class BaseValueInteraction : MonoBehaviour
    {
        private const float ValueEpsilon = 0.00001f;

        [Header("Input")]

        [SerializeField]
        private XRSimpleInteractable _inputInteractable;

        [Header("Value")]

        [SerializeField]
        [Range(0f, 1f)]
        private float _initialValue;

        [SerializeField]
        private NormalizedValueEvent _onValueChanged =
            new NormalizedValueEvent();

        private readonly List<IXRSelectInteractor> _manipulators =
            new List<IXRSelectInteractor>(2);

        private IXRSelectInteractor _activeManipulator;

        private float _value;
        private bool _valueInitialized;
        private bool _subscribed;

        public event Action<float> ValueChanged;

        public float Value
        {
            get
            {
                EnsureValueInitialized();
                return _value;
            }
        }

        public XRSimpleInteractable InputInteractable =>
            _inputInteractable;

        public bool HasActiveManipulator =>
            _activeManipulator != null;

        protected IXRSelectInteractor ActiveManipulator =>
            _activeManipulator;

        /// <summary>
        /// Мировая точка механизма.
        /// Используется при выборе ближайшей оставшейся руки.
        /// </summary>
        protected abstract Vector3 InteractionPoint { get; }

        protected virtual void Awake()
        {
            ResolveInputInteractable();
            EnsureValueInitialized();
        }

        protected virtual void OnEnable()
        {
            ResolveInputInteractable();
            Subscribe();

            _manipulators.Clear();
            SetActiveManipulator(null);

            /*
             * Обычно компонент включён до захвата.
             * Но если его включили во время Select,
             * восстанавливаем уже выбранные интеракторы.
             */
            if (_inputInteractable != null)
            {
                for (int i = 0;
                     i < _inputInteractable.interactorsSelecting.Count;
                     i++)
                {
                    IXRSelectInteractor interactor =
                        _inputInteractable.interactorsSelecting[i];

                    AddManipulator(interactor);
                }
            }

            EnsureActiveManipulator();
        }

        protected virtual void OnDisable()
        {
            Unsubscribe();

            _manipulators.Clear();
            SetActiveManipulator(null);
        }

        protected virtual void OnValidate()
        {
            _initialValue = Mathf.Clamp01(_initialValue);

            if (_inputInteractable == null)
            {
                _inputInteractable =
                    GetComponent<XRSimpleInteractable>();
            }
        }

        protected virtual void Reset()
        {
            _inputInteractable =
                GetComponent<XRSimpleInteractable>();
        }

        public void SetValue(float value)
        {
            SetValueInternal(
                value,
                notify: true,
                force: false);
        }

        public void SetValueWithoutNotify(float value)
        {
            SetValueInternal(
                value,
                notify: false,
                force: false);
        }

        protected bool TrySetValueFromInteraction(float value)
        {
            return SetValueInternal(
                value,
                notify: true,
                force: false);
        }

        protected bool TryGetActiveAttach(
            out Transform attachTransform)
        {
            EnsureActiveManipulator();

            if (_activeManipulator == null ||
                _inputInteractable == null)
            {
                attachTransform = null;
                return false;
            }

            attachTransform =
                _activeManipulator.GetAttachTransform(
                    _inputInteractable);

            if (attachTransform != null)
                return true;

            RemoveManipulator(_activeManipulator);
            EnsureActiveManipulator();

            if (_activeManipulator == null)
            {
                attachTransform = null;
                return false;
            }

            attachTransform =
                _activeManipulator.GetAttachTransform(
                    _inputInteractable);

            return attachTransform != null;
        }

        /// <summary>
        /// Вызывается после изменения Value.
        /// </summary>
        protected virtual void OnValueUpdated(
            float previousValue,
            float currentValue)
        {
        }

        /// <summary>
        /// Вызывается при смене руки, управляющей механизмом.
        /// Derived-классы используют это для сброса grab offset.
        /// </summary>
        protected virtual void OnActiveManipulatorChanged(
            IXRSelectInteractor previous,
            IXRSelectInteractor current)
        {
        }

        private void ResolveInputInteractable()
        {
            if (_inputInteractable == null)
            {
                _inputInteractable =
                    GetComponent<XRSimpleInteractable>();
            }

            if (_inputInteractable != null)
                return;

            Debug.LogError(
                $"{GetType().Name} on '{name}' requires " +
                $"{nameof(XRSimpleInteractable)}.",
                this);

            enabled = false;
        }

        private void Subscribe()
        {
            if (_subscribed ||
                _inputInteractable == null)
            {
                return;
            }

            _inputInteractable.selectEntered.AddListener(
                HandleSelectEntered);

            _inputInteractable.selectExited.AddListener(
                HandleSelectExited);

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                _inputInteractable == null)
            {
                return;
            }

            _inputInteractable.selectEntered.RemoveListener(
                HandleSelectEntered);

            _inputInteractable.selectExited.RemoveListener(
                HandleSelectExited);

            _subscribed = false;
        }

        private void HandleSelectEntered(
            SelectEnterEventArgs args)
        {
            AddManipulator(args.interactorObject);
            EnsureActiveManipulator();
        }

        private void HandleSelectExited(
            SelectExitEventArgs args)
        {
            RemoveManipulator(args.interactorObject);
            EnsureActiveManipulator();
        }

        private void AddManipulator(
            IXRSelectInteractor interactor)
        {
            if (interactor == null ||
                interactor is XRSocketInteractor ||
                _manipulators.Contains(interactor))
            {
                return;
            }

            _manipulators.Add(interactor);
        }

        private void RemoveManipulator(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return;

            bool wasActive =
                ReferenceEquals(
                    _activeManipulator,
                    interactor);

            _manipulators.Remove(interactor);

            if (wasActive)
                SetActiveManipulator(null);
        }

        private void EnsureActiveManipulator()
        {
            bool activeIsValid =
                _activeManipulator != null &&
                _manipulators.Contains(
                    _activeManipulator);

            if (activeIsValid)
            {
                Transform attach =
                    _activeManipulator.GetAttachTransform(
                        _inputInteractable);

                if (attach != null)
                    return;
            }

            IXRSelectInteractor closest =
                FindClosestManipulator();

            SetActiveManipulator(closest);
        }

        private IXRSelectInteractor FindClosestManipulator()
        {
            IXRSelectInteractor closest = null;
            float closestDistance =
                float.PositiveInfinity;

            Vector3 point =
                InteractionPoint;

            for (int i = 0;
                 i < _manipulators.Count;
                 i++)
            {
                IXRSelectInteractor manipulator =
                    _manipulators[i];

                if (manipulator == null)
                    continue;

                Transform attach =
                    manipulator.GetAttachTransform(
                        _inputInteractable);

                if (attach == null)
                    continue;

                float distance =
                    (attach.position - point)
                    .sqrMagnitude;

                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                closest = manipulator;
            }

            return closest;
        }

        private void SetActiveManipulator(
            IXRSelectInteractor manipulator)
        {
            if (ReferenceEquals(
                    _activeManipulator,
                    manipulator))
            {
                return;
            }

            IXRSelectInteractor previous =
                _activeManipulator;

            _activeManipulator =
                manipulator;

            OnActiveManipulatorChanged(
                previous,
                _activeManipulator);
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
            bool notify,
            bool force)
        {
            EnsureValueInitialized();

            float clampedValue =
                Mathf.Clamp01(value);

            if (!force &&
                Mathf.Abs(clampedValue - _value) <=
                ValueEpsilon)
            {
                return false;
            }

            float previousValue = _value;
            _value = clampedValue;

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