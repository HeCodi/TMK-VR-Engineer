using System.Collections.Generic;
using Assets.Game.Scripts.Interactable.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// База для механизмов, которыми управляют через Select.
    ///
    /// Поддерживает:
    /// - управляющую руку;
    /// - Control Anchor;
    /// - необязательный Reference Interactable;
    /// - автоматический выбор опорной руки;
    /// - безопасную смену рук.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public abstract class BaseManipulatorValueInteraction
        : BaseValueInteraction
    {
        [Header("Manipulator Input")]

        [SerializeField]
        private XRSimpleInteractable _inputInteractable;

        [Tooltip(
            "UX-точка управления механизмом. " +
            "Используется для выбора ближайшей руки и визуальной подсказки. " +
            "Не притягивает руку и не вызывает snap.")]
        [SerializeField]
        private Transform _controlAnchor;

        [Tooltip(
            "Необязательный удерживаемый объект, относительно руки которого " +
            "измеряется движение. Оставьте None для шторы, ящика и других " +
            "механизмов с неподвижной системой отсчёта.")]
        [SerializeField]
        private BaseInteractable _referenceInteractable;

        private readonly List<IXRSelectInteractor> _manipulators =
            new List<IXRSelectInteractor>(2);

        private IXRSelectInteractor _activeManipulator;
        private IXRSelectInteractor _referenceManipulator;

        private bool _subscribed;

        public XRSimpleInteractable InputInteractable =>
            _inputInteractable;

        public Transform ControlAnchor =>
            _controlAnchor;

        public BaseInteractable ReferenceInteractable =>
            _referenceInteractable;

        public bool RequiresReference =>
            _referenceInteractable != null;

        public bool HasActiveManipulator =>
            IsInteractorValid(_activeManipulator);

        protected IXRSelectInteractor ActiveManipulator =>
            _activeManipulator;

        protected virtual Vector3 InteractionPoint
        {
            get
            {
                if (_controlAnchor != null)
                    return _controlAnchor.position;

                return transform.position;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            ResolveInputInteractable();
        }

        protected virtual void OnEnable()
        {
            if (!ResolveInputInteractable())
                return;

            Subscribe();

            _manipulators.Clear();
            SetActiveManipulator(null);
            SetReferenceManipulator(null);

            IReadOnlyList<IXRSelectInteractor> selecting =
                _inputInteractable.interactorsSelecting;

            for (int i = 0; i < selecting.Count; i++)
                AddManipulator(selecting[i]);

            EnsureActiveManipulator();
        }

        protected override void OnDisable()
        {
            Unsubscribe();

            _manipulators.Clear();

            SetReferenceManipulator(null);
            SetActiveManipulator(null);

            base.OnDisable();
        }

        protected virtual void Reset()
        {
            _inputInteractable =
                GetComponent<XRSimpleInteractable>();

            _controlAnchor = transform;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_inputInteractable == null)
            {
                _inputInteractable =
                    GetComponent<XRSimpleInteractable>();
            }
        }

        /// <summary>
        /// Возвращает текущие позы управления.
        ///
        /// Если Reference Interactable назначен, но его не держит
        /// другая рука, метод возвращает false.
        /// </summary>
        protected bool TryGetManipulationFrame(
            out ManipulationFrame frame)
        {
            frame = default;

            EnsureActiveManipulator();

            if (!TryGetControlPose(out Pose controlPose))
                return false;

            if (_referenceInteractable == null)
            {
                SetReferenceManipulator(null);

                frame =
                    ManipulationFrame.WithoutReference(
                        controlPose);

                return true;
            }

            if (!TryGetReferencePose(out Pose referencePose))
                return false;

            frame =
                ManipulationFrame.WithReference(
                    controlPose,
                    referencePose);

            return true;
        }

        /// <summary>
        /// Вызывается, когда управляющая или опорная рука изменилась.
        /// Производные классы должны сбрасывать здесь baseline.
        /// </summary>
        protected virtual void OnManipulationFrameInvalidated()
        {
        }

        protected virtual void OnActiveManipulatorChanged(
            IXRSelectInteractor previous,
            IXRSelectInteractor current)
        {
        }

        protected virtual void OnReferenceManipulatorChanged(
            IXRSelectInteractor previous,
            IXRSelectInteractor current)
        {
        }

        private bool TryGetControlPose(out Pose pose)
        {
            pose = default;

            if (!IsInteractorValid(_activeManipulator) ||
                _inputInteractable == null)
            {
                return false;
            }

            Transform attach =
                _activeManipulator.GetAttachTransform(
                    _inputInteractable);

            if (attach == null)
                return false;

            pose = new Pose(
                attach.position,
                attach.rotation);

            return true;
        }

        private bool TryGetReferencePose(out Pose pose)
        {
            pose = default;

            InteractionContext context =
                _referenceInteractable != null
                    ? _referenceInteractable.Context
                    : null;

            if (context == null ||
                context.ManipulatorCount == 0)
            {
                SetReferenceManipulator(null);
                return false;
            }

            if (IsReferenceManipulatorUsable(context))
            {
                Transform retainedAttach =
                    context.GetInteractorAttach(
                        _referenceManipulator);

                if (retainedAttach != null)
                {
                    pose = new Pose(
                        retainedAttach.position,
                        retainedAttach.rotation);

                    return true;
                }
            }

            IXRSelectInteractor selectedReference = null;
            Transform selectedAttach = null;

            for (int i = 0;
                 i < context.ManipulatorCount;
                 i++)
            {
                IXRSelectInteractor candidate =
                    context.GetManipulator(i);

                if (!IsInteractorValid(candidate))
                    continue;

                if (ReferenceEquals(
                        candidate,
                        _activeManipulator))
                {
                    continue;
                }

                Transform candidateAttach =
                    context.GetInteractorAttach(candidate);

                if (candidateAttach == null)
                    continue;

                selectedReference = candidate;
                selectedAttach = candidateAttach;
                break;
            }

            SetReferenceManipulator(selectedReference);

            if (selectedAttach == null)
                return false;

            pose = new Pose(
                selectedAttach.position,
                selectedAttach.rotation);

            return true;
        }

        private bool IsReferenceManipulatorUsable(
            InteractionContext context)
        {
            if (!IsInteractorValid(_referenceManipulator))
                return false;

            if (ReferenceEquals(
                    _referenceManipulator,
                    _activeManipulator))
            {
                return false;
            }

            return context.Contains(_referenceManipulator);
        }

        private bool ResolveInputInteractable()
        {
            if (_inputInteractable == null)
            {
                _inputInteractable =
                    GetComponent<XRSimpleInteractable>();
            }

            if (_inputInteractable != null)
                return true;

            Debug.LogError(
                $"{GetType().Name} on '{name}' requires " +
                $"{nameof(XRSimpleInteractable)}.",
                this);

            enabled = false;
            return false;
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
            if (!IsInteractorValid(interactor))
                return;

            if (interactor is XRSocketInteractor)
                return;

            if (_manipulators.Contains(interactor))
                return;

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
            RemoveInvalidManipulators();

            if (IsActiveManipulatorUsable())
                return;

            SetActiveManipulator(
                FindClosestManipulator());
        }

        private bool IsActiveManipulatorUsable()
        {
            if (!IsInteractorValid(_activeManipulator))
                return false;

            if (!_manipulators.Contains(
                    _activeManipulator))
            {
                return false;
            }

            Transform attach =
                _activeManipulator.GetAttachTransform(
                    _inputInteractable);

            return attach != null;
        }

        private IXRSelectInteractor FindClosestManipulator()
        {
            if (_inputInteractable == null)
                return null;

            IXRSelectInteractor closest = null;

            float closestSqrDistance =
                float.PositiveInfinity;

            Vector3 interactionPoint =
                InteractionPoint;

            for (int i = 0;
                 i < _manipulators.Count;
                 i++)
            {
                IXRSelectInteractor candidate =
                    _manipulators[i];

                if (!IsInteractorValid(candidate))
                    continue;

                Transform attach =
                    candidate.GetAttachTransform(
                        _inputInteractable);

                if (attach == null)
                    continue;

                float sqrDistance =
                    (attach.position - interactionPoint)
                    .sqrMagnitude;

                if (sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closest = candidate;
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

            _activeManipulator = manipulator;

            SetReferenceManipulator(null);

            OnActiveManipulatorChanged(
                previous,
                _activeManipulator);

            OnManipulationFrameInvalidated();

            SetInteractionActive(
                IsInteractorValid(_activeManipulator));
        }

        private void SetReferenceManipulator(
            IXRSelectInteractor manipulator)
        {
            if (ReferenceEquals(
                    _referenceManipulator,
                    manipulator))
            {
                return;
            }

            IXRSelectInteractor previous =
                _referenceManipulator;

            _referenceManipulator = manipulator;

            OnReferenceManipulatorChanged(
                previous,
                _referenceManipulator);

            OnManipulationFrameInvalidated();
        }

        private void RemoveInvalidManipulators()
        {
            for (int i = _manipulators.Count - 1;
                 i >= 0;
                 i--)
            {
                if (IsInteractorValid(_manipulators[i]))
                    continue;

                bool wasActive =
                    ReferenceEquals(
                        _activeManipulator,
                        _manipulators[i]);

                _manipulators.RemoveAt(i);

                if (wasActive)
                    SetActiveManipulator(null);
            }
        }

        private static bool IsInteractorValid(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            if (interactor is UnityEngine.Object unityObject)
                return unityObject != null;

            return true;
        }
    }
}