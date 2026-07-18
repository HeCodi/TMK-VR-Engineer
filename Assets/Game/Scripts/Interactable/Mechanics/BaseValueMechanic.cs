using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Mechanics
{
    /// <summary>
    /// Базовый обработчик Value другого BaseValueInteraction.
    ///
    /// Mechanic:
    /// - получает Value источника;
    /// - может изменить его;
    /// - сам становится новым источником Value для Driver
    ///   или следующего Mechanic.
    /// </summary>
    public abstract class BaseValueMechanic
        : BaseValueInteraction
    {
        [Header("Mechanic Source")]

        [Tooltip(
            "Источник значения. Например LinearInteraction, " +
            "RotationInteraction или другой Mechanic.")]
        [SerializeField]
        private BaseValueInteraction _source;

        private bool _subscribed;

        public BaseValueInteraction Source => _source;

        protected override void Awake()
        {
            base.Awake();

            if (ResolveSource(logError: true))
                return;

            enabled = false;
        }

        protected virtual void OnEnable()
        {
            if (!ResolveSource(logError: true))
            {
                enabled = false;
                return;
            }

            Subscribe();

            /*
             * Перенимаем текущее состояние источника
             * без лишнего ValueChanged при включении.
             */
            SetValueWithoutNotify(_source.Value);

            SetInteractionActive(
                _source.IsInteracting);

            OnSourceSynchronized();
        }

        protected override void OnDisable()
        {
            Unsubscribe();

            SetInteractionActive(false);

            base.OnDisable();
        }

        protected virtual void Reset()
        {
            TryAutoAssignSource();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_source == this)
                _source = null;

            if (_source == null)
                TryAutoAssignSource();
        }

        /// <summary>
        /// Вызывается после начальной синхронизации с Source.
        /// </summary>
        protected virtual void OnSourceSynchronized()
        {
        }

        /// <summary>
        /// По умолчанию Mechanic просто пропускает Value дальше.
        /// </summary>
        protected virtual void OnSourceValueChanged(float value)
        {
            TrySetValueFromInteraction(value);
        }

        protected virtual void OnSourceInteractionStarted()
        {
            SetInteractionActive(true);
        }

        protected virtual void OnSourceInteractionEnded()
        {
            SetInteractionActive(false);
        }

        /// <summary>
        /// Синхронизирует внутреннее значение Source
        /// с фактическим итоговым положением Mechanic.
        ///
        /// Нужен после возврата и защёлкивания, чтобы при следующем
        /// касании Interaction не использовал устаревший Value.
        /// </summary>
        protected void WriteValueBackToSource(float value)
        {
            if (_source == null)
                return;

            _source.SetValueWithoutNotify(value);
        }

        private void Subscribe()
        {
            if (_subscribed || _source == null)
                return;

            _source.ValueChanged +=
                HandleSourceValueChanged;

            _source.InteractionStarted +=
                HandleSourceInteractionStarted;

            _source.InteractionEnded +=
                HandleSourceInteractionEnded;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _source == null)
                return;

            _source.ValueChanged -=
                HandleSourceValueChanged;

            _source.InteractionStarted -=
                HandleSourceInteractionStarted;

            _source.InteractionEnded -=
                HandleSourceInteractionEnded;

            _subscribed = false;
        }

        private void HandleSourceValueChanged(float value)
        {
            OnSourceValueChanged(value);
        }

        private void HandleSourceInteractionStarted()
        {
            OnSourceInteractionStarted();
        }

        private void HandleSourceInteractionEnded()
        {
            OnSourceInteractionEnded();
        }

        private bool ResolveSource(bool logError)
        {
            if (_source == null)
                TryAutoAssignSource();

            if (_source != null && _source != this)
                return true;

            if (logError)
            {
                Debug.LogError(
                    $"{GetType().Name} on '{name}' requires a valid " +
                    $"{nameof(BaseValueInteraction)} Source.",
                    this);
            }

            return false;
        }

        private void TryAutoAssignSource()
        {
            BaseValueInteraction[] candidates =
                GetComponents<BaseValueInteraction>();

            BaseValueInteraction found = null;
            int foundCount = 0;

            for (int i = 0; i < candidates.Length; i++)
            {
                BaseValueInteraction candidate =
                    candidates[i];

                if (candidate == null || candidate == this)
                    continue;

                found = candidate;
                foundCount++;
            }

            if (foundCount == 1)
                _source = found;
        }
    }
}