using Assets.Game.Scripts.Interactable.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Assets.Game.Scripts.Interactable.Movement
{
    /// <summary>
    /// Универсальный Grab Transformer.
    ///
    /// Поддерживает:
    /// - одну руку;
    /// - две руки;
    /// - переход 1 -> 2;
    /// - переход 2 -> 1;
    /// - отпускание первой руки;
    /// - отпускание второй руки;
    /// - socket attach.
    ///
    /// Здесь нет понятия "главная рука".
    /// При двух руках объект привязан к общему frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BaseInteractable))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class UniversalGrabTransformer
        : XRBaseGrabTransformer
    {
        private enum GrabState
        {
            None,
            Socket,
            OneManipulator,
            TwoManipulators
        }

        private BaseInteractable _source;
        private InteractionContext _context;

        private GrabState _mode;

        private IXRSelectInteractor _firstInteractor;
        private IXRSelectInteractor _secondInteractor;

        private Vector3 _localPositionOffset;
        private Quaternion _localRotationOffset =
            Quaternion.identity;

        private Quaternion _lastPairRotation =
            Quaternion.identity;

        /// <summary>
        /// Один экземпляр используется XRI и для одного,
        /// и для нескольких интеракторов.
        /// </summary>
        protected override RegistrationMode registrationMode =>
            RegistrationMode.SingleAndMultiple;

        private void Awake()
        {
            _source = GetComponent<BaseInteractable>();
            _context = _source.Context;

            XRGrabInteractable grabInteractable =
                _source.GrabInteractable;

            grabInteractable.addDefaultGrabTransformers = false;

            /*
             * Удаляем старые ссылки из Inspector.
             * Этот компонент зарегистрируется автоматически
             * через registrationMode.
             */
            grabInteractable
                .startingSingleGrabTransformers
                .Clear();

            grabInteractable
                .startingMultipleGrabTransformers
                .Clear();
        }

        protected override void Start()
        {
            XRGrabInteractable grabInteractable =
                _source.GrabInteractable;

            /*
             * К этому моменту остальные компоненты уже могли
             * зарегистрировать старые трансформеры.
             *
             * Полностью очищаем runtime-списки,
             * после чего base.Start зарегистрирует этот компонент.
             */
            grabInteractable.ClearSingleGrabTransformers();
            grabInteractable.ClearMultipleGrabTransformers();

            base.Start();
        }

        public override void OnGrab(
            XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            ResetState();
        }

        /// <summary>
        /// XRI вызывает метод непосредственно перед Process,
        /// когда изменилось количество селекторов.
        ///
        /// Именно здесь пересчитывается offset,
        /// благодаря чему объект не телепортируется.
        /// </summary>
        public override void OnGrabCountChanged(
            XRGrabInteractable grabInteractable,
            Pose targetPose,
            Vector3 localScale)
        {
            base.OnGrabCountChanged(
                grabInteractable,
                targetPose,
                localScale);

            InitializeForCurrentSelection(targetPose);
        }

        public override void OnUnlink(
            XRGrabInteractable grabInteractable)
        {
            ResetState();
            base.OnUnlink(grabInteractable);
        }

        public override void Process(
            XRGrabInteractable grabInteractable,
            XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose,
            ref Vector3 localScale)
        {
            int manipulatorCount =
                _context.ManipulatorCount;

            if (manipulatorCount >= 2)
            {
                ProcessTwoManipulators(
                    ref targetPose);

                return;
            }

            if (manipulatorCount == 1)
            {
                ProcessOneManipulator(
                    ref targetPose);

                return;
            }

            XRSocketInteractor socket =
                _context.GetFirstSocket();

            if (socket != null)
            {
                ProcessSocket(
                    socket,
                    ref targetPose);

                return;
            }

            ResetState();
        }

        private void InitializeForCurrentSelection(
            Pose targetPose)
        {
            int manipulatorCount =
                _context.ManipulatorCount;

            if (manipulatorCount >= 2)
            {
                TryInitializeTwoManipulators(
                    _context.GetManipulator(0),
                    _context.GetManipulator(1),
                    targetPose);

                return;
            }

            if (manipulatorCount == 1)
            {
                TryInitializeOneManipulator(
                    _context.GetManipulator(0),
                    targetPose);

                return;
            }

            if (_context.GetFirstSocket() != null)
            {
                _mode = GrabState.Socket;

                _firstInteractor = null;
                _secondInteractor = null;

                return;
            }

            ResetState();
        }

        private void ProcessOneManipulator(
            ref Pose targetPose)
        {
            IXRSelectInteractor interactor =
                _context.GetManipulator(0);

            if (interactor == null)
                return;

            bool requiresInitialization =
                _mode != GrabState.OneManipulator ||
                !ReferenceEquals(
                    _firstInteractor,
                    interactor);

            if (requiresInitialization)
            {
                bool initialized =
                    TryInitializeOneManipulator(
                        interactor,
                        targetPose);

                if (!initialized)
                    return;
            }

            Transform interactorAttach =
                _context.GetInteractorAttach(
                    interactor);

            if (interactorAttach == null)
                return;

            Pose interactorFrame =
                GrabPoseMath.GetPose(
                    interactorAttach);

            targetPose =
                GrabPoseMath.ApplyRelativePose(
                    interactorFrame,
                    _localPositionOffset,
                    _localRotationOffset);
        }

        private void ProcessTwoManipulators(
            ref Pose targetPose)
        {
            ResolveStablePair(
                out IXRSelectInteractor first,
                out IXRSelectInteractor second);

            if (first == null ||
                second == null)
            {
                return;
            }

            bool requiresInitialization =
                _mode != GrabState.TwoManipulators ||
                !ReferenceEquals(
                    _firstInteractor,
                    first) ||
                !ReferenceEquals(
                    _secondInteractor,
                    second);

            if (requiresInitialization)
            {
                bool initialized =
                    TryInitializeTwoManipulators(
                        first,
                        second,
                        targetPose);

                if (!initialized)
                    return;
            }

            Transform firstAttach =
                _context.GetInteractorAttach(first);

            Transform secondAttach =
                _context.GetInteractorAttach(second);

            bool frameCreated =
                GrabPoseMath.TryCreateTwoHandFrame(
                    firstAttach,
                    secondAttach,
                    _lastPairRotation,
                    out Pose pairFrame);

            if (!frameCreated)
                return;

            _lastPairRotation =
                pairFrame.rotation;

            targetPose =
                GrabPoseMath.ApplyRelativePose(
                    pairFrame,
                    _localPositionOffset,
                    _localRotationOffset);
        }

        private void ProcessSocket(
            XRSocketInteractor socket,
            ref Pose targetPose)
        {
            Transform socketAttach =
                socket.GetAttachTransform(
                    _source.GrabInteractable);

            Transform objectAttach =
                _context.GetStaticInteractableAttach();

            if (socketAttach == null ||
                objectAttach == null)
            {
                return;
            }

            Transform root =
                _source.GrabInteractable.transform;

            Pose currentRootPose =
                new Pose(
                    root.position,
                    root.rotation);

            Pose currentObjectAttachPose =
                GrabPoseMath.GetPose(
                    objectAttach);

            Pose targetSocketAttachPose =
                GrabPoseMath.GetPose(
                    socketAttach);

            targetPose =
                GrabPoseMath.AlignRootToAttach(
                    currentRootPose,
                    currentObjectAttachPose,
                    targetSocketAttachPose);

            _mode = GrabState.Socket;

            _firstInteractor = null;
            _secondInteractor = null;
        }

        private bool TryInitializeOneManipulator(
            IXRSelectInteractor interactor,
            Pose targetPose)
        {
            if (interactor == null)
            {
                ResetState();
                return false;
            }

            Transform interactorAttach =
                _context.GetInteractorAttach(
                    interactor);

            if (interactorAttach == null)
            {
                ResetState();
                return false;
            }

            Pose interactorFrame =
                GrabPoseMath.GetPose(
                    interactorAttach);

            GrabPoseMath.CaptureRelativePose(
                interactorFrame,
                targetPose,
                out _localPositionOffset,
                out _localRotationOffset);

            _mode = GrabState.OneManipulator;

            _firstInteractor = interactor;
            _secondInteractor = null;

            return true;
        }

        private bool TryInitializeTwoManipulators(
            IXRSelectInteractor first,
            IXRSelectInteractor second,
            Pose targetPose)
        {
            if (first == null ||
                second == null)
            {
                ResetState();
                return false;
            }

            Transform firstAttach =
                _context.GetInteractorAttach(first);

            Transform secondAttach =
                _context.GetInteractorAttach(second);

            Quaternion fallbackRotation =
                _mode == GrabState.TwoManipulators
                    ? _lastPairRotation
                    : targetPose.rotation;

            bool frameCreated =
                GrabPoseMath.TryCreateTwoHandFrame(
                    firstAttach,
                    secondAttach,
                    fallbackRotation,
                    out Pose pairFrame);

            if (!frameCreated)
            {
                ResetState();
                return false;
            }

            GrabPoseMath.CaptureRelativePose(
                pairFrame,
                targetPose,
                out _localPositionOffset,
                out _localRotationOffset);

            _lastPairRotation =
                pairFrame.rotation;

            _mode =
                GrabState.TwoManipulators;

            _firstInteractor = first;
            _secondInteractor = second;

            return true;
        }

        private void ResolveStablePair(
            out IXRSelectInteractor first,
            out IXRSelectInteractor second)
        {
            bool previousPairIsValid =
                _mode == GrabState.TwoManipulators &&
                _context.Contains(_firstInteractor) &&
                _context.Contains(_secondInteractor) &&
                !InteractionContext.IsSocket(
                    _firstInteractor) &&
                !InteractionContext.IsSocket(
                    _secondInteractor);

            if (previousPairIsValid)
            {
                first = _firstInteractor;
                second = _secondInteractor;

                return;
            }

            first =
                _context.GetManipulator(0);

            second =
                _context.GetManipulator(1);
        }

        private void ResetState()
        {
            _mode = GrabState.None;

            _firstInteractor = null;
            _secondInteractor = null;

            _localPositionOffset =
                Vector3.zero;

            _localRotationOffset =
                Quaternion.identity;

            _lastPairRotation =
                Quaternion.identity;
        }
    }
}