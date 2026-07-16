using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Core
{
    /// <summary>
    /// Мост между XRGrabInteractable и библиотекой интеракций.
    ///
    /// XRGrabInteractable отвечает за:
    /// - Hover;
    /// - Select;
    /// - Socket;
    /// - применение target pose;
    /// - Rigidbody;
    /// - бросок.
    ///
    /// Пользовательский Grab Transformer отвечает
    /// только за вычисление target pose.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class BaseInteractable : MonoBehaviour
    {
        [Header("Automatic configuration")]

        [SerializeField]
        private bool _configureGrabInteractable = true;

        private XRGrabInteractable _grabInteractable;
        private InteractionContext _context;
        private bool _subscribed;

        public event Action<IXRSelectInteractor> Selected;
        public event Action<IXRSelectInteractor> Deselected;

        public XRGrabInteractable GrabInteractable
        {
            get
            {
                EnsureInitialized();
                return _grabInteractable;
            }
        }

        public InteractionContext Context
        {
            get
            {
                EnsureInitialized();
                return _context;
            }
        }

        private void Awake()
        {
            EnsureInitialized();

            if (_configureGrabInteractable)
                ConfigureGrabInteractable();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            EnsureInitialized();

            if (_configureGrabInteractable)
                ConfigureGrabInteractable();
        }

        private void EnsureInitialized()
        {
            if (_grabInteractable == null)
            {
                _grabInteractable =
                    GetComponent<XRGrabInteractable>();
            }

            if (_context == null &&
                _grabInteractable != null)
            {
                _context =
                    new InteractionContext(_grabInteractable);
            }
        }

        private void ConfigureGrabInteractable()
        {
            if (_grabInteractable == null)
                return;

            // Для одновременного захвата двумя руками.
            _grabInteractable.selectMode =
                InteractableSelectMode.Multiple;

            // XRI должен применять target pose,
            // который рассчитывает наш трансформер.
            _grabInteractable.trackPosition = true;
            _grabInteractable.trackRotation = true;

            // Масштабирование пока не используется.
            _grabInteractable.trackScale = false;

            // Позволяет хватать объект в произвольной точке.
            _grabInteractable.useDynamicAttach = true;
            _grabInteractable.matchAttachPosition = true;
            _grabInteractable.matchAttachRotation = true;

            // Дополнительная защита от прыжка
            // при переходе 2 руки -> 1 рука.
            _grabInteractable
                .reinitializeDynamicAttachEverySingleGrab = true;

            // Стандартные трансформеры XRI не должны
            // конфликтовать с UniversalGrabTransformer.
            _grabInteractable.addDefaultGrabTransformers = false;
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            _grabInteractable.selectEntered.AddListener(
                HandleSelectEntered);

            _grabInteractable.selectExited.AddListener(
                HandleSelectExited);

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                _grabInteractable == null)
            {
                return;
            }

            _grabInteractable.selectEntered.RemoveListener(
                HandleSelectEntered);

            _grabInteractable.selectExited.RemoveListener(
                HandleSelectExited);

            _subscribed = false;
        }

        private void HandleSelectEntered(
            SelectEnterEventArgs args)
        {
            Selected?.Invoke(args.interactorObject);
        }

        private void HandleSelectExited(
            SelectExitEventArgs args)
        {
            Deselected?.Invoke(args.interactorObject);
        }
    }
}