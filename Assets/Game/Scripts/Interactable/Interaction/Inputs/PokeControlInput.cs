using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Inputs
{
    /// <summary>
    /// Универсальный источник управляющей точки от XRPokeInteractor.
    ///
    /// Не знает, чем управляет палец:
    /// кнопкой, тумблером, ползунком или вращательной ручкой.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    [DefaultExecutionOrder(-100)]
    public sealed class PokeControlInput : BaseControlInput
    {
        [Header("Poke Input")]

        [SerializeField]
        private XRSimpleInteractable _interactable;

        [Tooltip(
            "Точка механизма, около которой выбирается ближайший палец. " +
            "Она не перемещает палец и не создаёт snap.")]
        [SerializeField]
        private Transform _controlAnchor;

        private readonly List<IXRHoverInteractor> _interactors =
            new List<IXRHoverInteractor>(2);

        private IXRHoverInteractor _activeInteractor;
        private bool _subscribed;

        public override object ActiveSource =>
            _activeInteractor;

        public XRSimpleInteractable Interactable =>
            _interactable;

        public Transform ControlAnchor =>
            _controlAnchor;

        private void Awake()
        {
            ResolveInteractable();
        }

        private void OnEnable()
        {
            if (!ResolveInteractable())
                return;

            Subscribe();

            _interactors.Clear();
            SetActiveInteractor(null);

            IReadOnlyList<IXRHoverInteractor> hovering =
                _interactable.interactorsHovering;

            for (int i = 0; i < hovering.Count; i++)
                AddInteractor(hovering[i]);

            EnsureActiveInteractor();
        }

        protected override void OnDisable()
        {
            Unsubscribe();

            _interactors.Clear();
            SetActiveInteractor(null);

            base.OnDisable();
        }

        private void Reset()
        {
            _interactable =
                GetComponent<XRSimpleInteractable>();

            _controlAnchor = transform;
        }

        private void OnValidate()
        {
            if (_interactable == null)
            {
                _interactable =
                    GetComponent<XRSimpleInteractable>();
            }
        }

        public override bool TryGetControlPose(
            out Pose pose)
        {
            pose = default;

            EnsureActiveInteractor();

            if (!IsActiveInteractorUsable())
                return false;

            Transform attach =
                _activeInteractor.GetAttachTransform(
                    _interactable);

            if (attach == null)
                return false;

            pose = new Pose(
                attach.position,
                attach.rotation);

            return true;
        }

        private bool ResolveInteractable()
        {
            if (_interactable == null)
            {
                _interactable =
                    GetComponent<XRSimpleInteractable>();
            }

            if (_interactable != null)
                return true;

            Debug.LogError(
                $"{nameof(PokeControlInput)} on '{name}' requires " +
                $"{nameof(XRSimpleInteractable)}.",
                this);

            enabled = false;
            return false;
        }

        private void Subscribe()
        {
            if (_subscribed || _interactable == null)
                return;

            _interactable.hoverEntered.AddListener(
                HandleHoverEntered);

            _interactable.hoverExited.AddListener(
                HandleHoverExited);

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _interactable == null)
                return;

            _interactable.hoverEntered.RemoveListener(
                HandleHoverEntered);

            _interactable.hoverExited.RemoveListener(
                HandleHoverExited);

            _subscribed = false;
        }

        private void HandleHoverEntered(
            HoverEnterEventArgs args)
        {
            AddInteractor(args.interactorObject);
            EnsureActiveInteractor();
        }

        private void HandleHoverExited(
            HoverExitEventArgs args)
        {
            RemoveInteractor(args.interactorObject);
            EnsureActiveInteractor();
        }

        private void AddInteractor(
            IXRHoverInteractor interactor)
        {
            if (!IsValid(interactor))
                return;

            // Обычные Near/Far и Ray hover не считаются пальцем.
            if (!(interactor is XRPokeInteractor))
                return;

            if (_interactors.Contains(interactor))
                return;

            _interactors.Add(interactor);
        }

        private void RemoveInteractor(
            IXRHoverInteractor interactor)
        {
            if (interactor == null)
                return;

            bool wasActive =
                ReferenceEquals(
                    _activeInteractor,
                    interactor);

            _interactors.Remove(interactor);

            if (wasActive)
                SetActiveInteractor(null);
        }

        private void EnsureActiveInteractor()
        {
            RemoveInvalidInteractors();

            if (IsActiveInteractorUsable())
                return;

            SetActiveInteractor(
                FindClosestInteractor());
        }

        private bool IsActiveInteractorUsable()
        {
            if (!IsValid(_activeInteractor))
                return false;

            if (!_interactors.Contains(_activeInteractor))
                return false;

            if (_interactable == null)
                return false;

            return _activeInteractor.GetAttachTransform(
                _interactable) != null;
        }

        private IXRHoverInteractor FindClosestInteractor()
        {
            if (_interactable == null)
                return null;

            Vector3 point =
                _controlAnchor != null
                    ? _controlAnchor.position
                    : transform.position;

            IXRHoverInteractor closest = null;

            float closestSqrDistance =
                float.PositiveInfinity;

            for (int i = 0; i < _interactors.Count; i++)
            {
                IXRHoverInteractor candidate =
                    _interactors[i];

                if (!IsValid(candidate))
                    continue;

                Transform attach =
                    candidate.GetAttachTransform(
                        _interactable);

                if (attach == null)
                    continue;

                float sqrDistance =
                    (attach.position - point).sqrMagnitude;

                if (sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closest = candidate;
            }

            return closest;
        }

        private void SetActiveInteractor(
            IXRHoverInteractor interactor)
        {
            if (ReferenceEquals(
                    _activeInteractor,
                    interactor))
            {
                return;
            }

            bool wasActive =
                IsValid(_activeInteractor);

            _activeInteractor = interactor;

            bool isActive =
                IsValid(_activeInteractor);

            NotifySourceChanged();

            if (wasActive != isActive)
                SetInputActive(isActive);
        }

        private void RemoveInvalidInteractors()
        {
            for (int i = _interactors.Count - 1;
                 i >= 0;
                 i--)
            {
                IXRHoverInteractor interactor =
                    _interactors[i];

                if (IsValid(interactor))
                    continue;

                bool wasActive =
                    ReferenceEquals(
                        _activeInteractor,
                        interactor);

                _interactors.RemoveAt(i);

                if (wasActive)
                    SetActiveInteractor(null);
            }
        }

        private static bool IsValid(
            IXRHoverInteractor interactor)
        {
            if (interactor == null)
                return false;

            if (interactor is Object unityObject)
                return unityObject != null;

            return true;
        }
    }
}