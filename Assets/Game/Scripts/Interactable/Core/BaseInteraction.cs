using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Core
{
    /// <summary>
    /// Базовый класс для механических интеракций:
    ///
    /// - RotationInteraction;
    /// - LinearInteraction;
    /// - ButtonInteraction;
    /// - LeverInteraction.
    ///
    /// Компонент может находиться на дочернем объекте.
    /// BaseInteractable будет найден в родителях.
    /// </summary>
    public abstract class BaseInteraction : MonoBehaviour
    {
        [SerializeField]
        private BaseInteractable _source;

        private bool _subscribed;

        protected BaseInteractable Source =>
            _source;

        protected InteractionContext Context =>
            _source != null
                ? _source.Context
                : null;

        protected bool HasManipulator =>
            Context != null &&
            Context.ManipulatorCount > 0;

        protected virtual void Awake()
        {
            ResolveSource();
        }

        protected virtual void OnEnable()
        {
            if (!ResolveSource())
                return;

            Subscribe();
        }

        protected virtual void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Вызывается при добавлении любого селектора.
        /// Сокет тоже может прийти сюда.
        /// </summary>
        protected virtual void OnInteractorSelected(
            IXRSelectInteractor interactor)
        {
        }

        /// <summary>
        /// Вызывается при удалении любого селектора.
        /// </summary>
        protected virtual void OnInteractorDeselected(
            IXRSelectInteractor interactor)
        {
        }

        protected IXRSelectInteractor GetManipulator(int index)
        {
            return Context?.GetManipulator(index);
        }

        protected Transform GetManipulatorAttach(int index)
        {
            IXRSelectInteractor interactor =
                GetManipulator(index);

            return Context?.GetInteractorAttach(interactor);
        }

        private bool ResolveSource()
        {
            if (_source == null)
            {
                _source =
                    GetComponentInParent<BaseInteractable>(true);
            }

            if (_source != null)
                return true;

            Debug.LogError(
                $"{GetType().Name} on '{name}' requires " +
                "BaseInteractable on this object or a parent.",
                this);

            enabled = false;
            return false;
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            _source.Selected += OnInteractorSelected;
            _source.Deselected += OnInteractorDeselected;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                _source == null)
            {
                return;
            }

            _source.Selected -= OnInteractorSelected;
            _source.Deselected -= OnInteractorDeselected;

            _subscribed = false;
        }
    }
}