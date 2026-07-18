using Assets.Game.Scripts.Interactable.Core;
using Assets.Game.Scripts.Interactable.Inputs;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    /// <summary>
    /// База для взаимодействий, преобразующих управляющую Pose
    /// в нормализованное значение 0..1.
    ///
    /// Control Input может быть Select, Poke или Composite.
    ///
    /// При назначенном Reference Interactable управление разрешается
    /// только при наличии другой руки, удерживающей reference-объект.
    /// </summary>
    public abstract class BasePoseValueInteraction
        : BaseValueInteraction
    {
        [Header("Control")]

        [SerializeField]
        private BaseControlInput _controlInput;

        [Tooltip(
            "Необязательный переносимый объект. Если он назначен, " +
            "движение измеряется относительно другой руки, " +
            "удерживающей этот объект.")]
        [SerializeField]
        private BaseInteractable _referenceInteractable;

        private IXRSelectInteractor _referenceManipulator;
        private bool _subscribed;

        public BaseControlInput ControlInput =>
            _controlInput;

        public BaseInteractable ReferenceInteractable =>
            _referenceInteractable;

        public bool RequiresReference =>
            _referenceInteractable != null;

        protected override void Awake()
        {
            base.Awake();

            if (ResolveControlInput(logError: true))
                return;

            enabled = false;
        }

        protected virtual void OnEnable()
        {
            if (!ResolveControlInput(logError: true))
            {
                enabled = false;
                return;
            }

            Subscribe();

            SetReferenceManipulator(null);
            OnManipulationFrameInvalidated();
        }

        protected override void OnDisable()
        {
            Unsubscribe();

            SetReferenceManipulator(null);
            SetInteractionActive(false);

            base.OnDisable();
        }

        protected virtual void Reset()
        {
            TryAutoAssignControlInput();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_controlInput == null)
                TryAutoAssignControlInput();
        }

        /// <summary>
        /// Получает управляющую Pose и, при необходимости,
        /// Pose опорной руки.
        ///
        /// Возвращает false, когда:
        /// - input отсутствует;
        /// - input не активен;
        /// - управляющая Pose недоступна;
        /// - reference назначен, но его не держит другая рука.
        /// </summary>
        protected bool TryGetManipulationFrame(
            out ManipulationFrame frame)
        {
            frame = default;

            if (_controlInput == null ||
                !_controlInput.IsActive ||
                !_controlInput.TryGetControlPose(
                    out Pose controlPose))
            {
                SetReferenceManipulator(null);
                SetInteractionActive(false);
                return false;
            }

            if (_referenceInteractable == null)
            {
                SetReferenceManipulator(null);
                SetInteractionActive(true);

                frame =
                    ManipulationFrame.WithoutReference(
                        controlPose);

                return true;
            }

            if (!TryGetReferencePose(
                    out Pose referencePose))
            {
                SetInteractionActive(false);
                return false;
            }

            SetInteractionActive(true);

            frame =
                ManipulationFrame.WithReference(
                    controlPose,
                    referencePose);

            return true;
        }

        /// <summary>
        /// Вызывается при смене управляющего input,
        /// управляющего interactor или reference-руки.
        ///
        /// Производные классы должны сбрасывать здесь baseline.
        /// </summary>
        protected virtual void OnManipulationFrameInvalidated()
        {
        }

        private bool TryGetReferencePose(
            out Pose pose)
        {
            pose = default;

            if (_referenceInteractable == null)
            {
                SetReferenceManipulator(null);
                return false;
            }

            InteractionContext context =
                _referenceInteractable.Context;

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

            IXRSelectInteractor selectedManipulator = null;
            Transform selectedAttach = null;

            for (int i = 0;
                 i < context.ManipulatorCount;
                 i++)
            {
                IXRSelectInteractor candidate =
                    context.GetManipulator(i);

                if (!IsInteractorValid(candidate))
                    continue;

                // Управляющая и опорная руки должны отличаться.
                if (ReferenceEquals(
                        candidate,
                        _controlInput.ActiveSource))
                {
                    continue;
                }

                Transform candidateAttach =
                    context.GetInteractorAttach(candidate);

                if (candidateAttach == null)
                    continue;

                selectedManipulator = candidate;
                selectedAttach = candidateAttach;
                break;
            }

            SetReferenceManipulator(
                selectedManipulator);

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
            if (!IsInteractorValid(
                    _referenceManipulator))
            {
                return false;
            }

            if (ReferenceEquals(
                    _referenceManipulator,
                    _controlInput.ActiveSource))
            {
                return false;
            }

            return context.Contains(
                _referenceManipulator);
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

            _referenceManipulator = manipulator;

            OnManipulationFrameInvalidated();
        }

        private void Subscribe()
        {
            if (_subscribed ||
                _controlInput == null)
            {
                return;
            }

            _controlInput.Started +=
                HandleInputStarted;

            _controlInput.Ended +=
                HandleInputEnded;

            _controlInput.SourceChanged +=
                HandleInputSourceChanged;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                _controlInput == null)
            {
                return;
            }

            _controlInput.Started -=
                HandleInputStarted;

            _controlInput.Ended -=
                HandleInputEnded;

            _controlInput.SourceChanged -=
                HandleInputSourceChanged;

            _subscribed = false;
        }

        private void HandleInputStarted()
        {
            SetReferenceManipulator(null);
            OnManipulationFrameInvalidated();
        }

        private void HandleInputEnded()
        {
            SetReferenceManipulator(null);
            SetInteractionActive(false);
            OnManipulationFrameInvalidated();
        }

        private void HandleInputSourceChanged()
        {
            SetReferenceManipulator(null);
            OnManipulationFrameInvalidated();
        }

        private bool ResolveControlInput(bool logError)
        {
            if (_controlInput != null)
                return true;

            TryAutoAssignControlInput();

            if (_controlInput != null)
                return true;

            if (!logError)
                return false;

            BaseControlInput[] inputs =
                GetComponents<BaseControlInput>();

            if (inputs.Length == 0)
            {
                Debug.LogError(
                    $"{GetType().Name} on '{name}' requires a " +
                    $"{nameof(BaseControlInput)}.",
                    this);
            }
            else
            {
                Debug.LogError(
                    $"{GetType().Name} on '{name}' found several " +
                    $"{nameof(BaseControlInput)} components. " +
                    $"Assign Control Input explicitly. When using " +
                    $"Select and Poke together, assign " +
                    $"{nameof(CompositeControlInput)}.",
                    this);
            }

            return false;
        }

        private void TryAutoAssignControlInput()
        {
            BaseControlInput[] inputs =
                GetComponents<BaseControlInput>();

            if (inputs.Length == 1)
            {
                _controlInput = inputs[0];
                return;
            }

            CompositeControlInput composite = null;
            int compositeCount = 0;

            for (int i = 0; i < inputs.Length; i++)
            {
                if (!(inputs[i] is CompositeControlInput candidate))
                    continue;

                composite = candidate;
                compositeCount++;
            }

            if (compositeCount == 1)
                _controlInput = composite;
        }

        private static bool IsInteractorValid(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            if (interactor is Object unityObject)
                return unityObject != null;

            return true;
        }
    }
}