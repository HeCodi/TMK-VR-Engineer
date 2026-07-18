using System.Collections.Generic;
using Assets.Game.Scripts.Interactable.Core;
using Assets.Game.Scripts.Interactable.Inputs;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Assets.Game.Scripts.Interactable.Movement
{
    /// <summary>
    /// Универсальный grab transformer.
    ///
    /// Поддерживает:
    /// - grab одной рукой;
    /// - обычный grab двумя руками;
    /// - управление дочерним механизмом второй рукой;
    /// - устойчивое вращение при сближении рук;
    /// - переходы между одной и двумя руками;
    /// - socket attach;
    /// - защиту от нулевых, NaN и ненормализованных quaternion.
    ///
    /// Для штангенциркуля:
    /// - primary-рука держит корпус;
    /// - control-рука управляет кареткой;
    /// - control-рука дополнительно участвует во вращении;
    /// - при сближении рук влияние направления отключается.
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

        private const float MinimumDirectionSqrMagnitude =
            0.000001f;

        private const float MinimumQuaternionSqrMagnitude =
            0.00000001f;

        [Header("Control Routing")]

        [Tooltip(
            "Control Input компонентов дочерних механизмов. " +
            "Для штангенциркуля здесь назначается " +
            "GrabRegionControlInput каретки.")]
        [SerializeField]
        private GrabRegionControlInput[] _controlInputs;

        [Header("Two Hand Rotation Stability")]

        [Tooltip(
            "Ниже этой дистанции влияние направления " +
            "между руками отключается.")]
        [SerializeField]
        [Min(0f)]
        private float _twoHandRotationDisableDistance =
            0.04f;

        [Tooltip(
            "После отключения влияние второй руки возвращается " +
            "только после достижения этой дистанции.")]
        [SerializeField]
        [Min(0f)]
        private float _twoHandRotationEnableDistance =
            0.07f;

        [Tooltip(
            "На этой дистанции влияние направления " +
            "между руками становится полным.")]
        [SerializeField]
        [Min(0f)]
        private float _twoHandRotationFullDistance =
            0.12f;

        private readonly List<IXRSelectInteractor>
            _rootManipulators =
                new List<IXRSelectInteractor>(4);

        private BaseInteractable _source;
        private InteractionContext _context;

        private GrabState _mode;

        private IXRSelectInteractor _firstInteractor;
        private IXRSelectInteractor _secondInteractor;

        /*
         * Рука, управляющая кареткой.
         */
        private IXRSelectInteractor
            _routedControlInteractor;

        /*
         * Рука, удерживающая корпус.
         */
        private IXRSelectInteractor
            _primaryInteractor;

        private Vector3 _localPositionOffset;

        private Quaternion _localRotationOffset =
            Quaternion.identity;

        private Quaternion _lastPairRotation =
            Quaternion.identity;

        /*
         * Начальные данные routed two-hand режима.
         */
        private Quaternion _primaryStartAttachRotation =
            Quaternion.identity;

        private Quaternion _hybridStartFrameRotation =
            Quaternion.identity;

        /*
         * Состояние вращения по направлению между руками.
         */
        private bool _pairRotationActive;
        private bool _pairBaselineCaptured;

        private Vector3 _pairBaselineDirectionInPrimarySpace =
            Vector3.forward;

        /*
         * Последний rotation, который точно был валидным.
         * Используется для автоматического восстановления.
         */
        private Quaternion _lastValidTargetRotation =
            Quaternion.identity;

        private bool _hasLastValidTargetRotation;
        private bool _invalidRotationReported;

        protected override RegistrationMode registrationMode =>
            RegistrationMode.SingleAndMultiple;

        private void Awake()
        {
            _source =
                GetComponent<BaseInteractable>();

            _context =
                _source.Context;

            ResolveControlInputs();

            XRGrabInteractable grabInteractable =
                _source.GrabInteractable;

            grabInteractable.addDefaultGrabTransformers =
                false;

            grabInteractable
                .startingSingleGrabTransformers
                .Clear();

            grabInteractable
                .startingMultipleGrabTransformers
                .Clear();

            _lastValidTargetRotation =
                ResolveSafeRotation(
                    grabInteractable.transform.rotation,
                    Quaternion.identity);

            _hasLastValidTargetRotation =
                true;
        }

        private void Reset()
        {
            ResolveControlInputs();
        }

        private void OnValidate()
        {
            ResolveControlInputs();

            _twoHandRotationDisableDistance =
                Mathf.Max(
                    0f,
                    _twoHandRotationDisableDistance);

            _twoHandRotationEnableDistance =
                Mathf.Max(
                    _twoHandRotationDisableDistance +
                    0.001f,
                    _twoHandRotationEnableDistance);

            _twoHandRotationFullDistance =
                Mathf.Max(
                    _twoHandRotationEnableDistance +
                    0.001f,
                    _twoHandRotationFullDistance);
        }

        protected override void Start()
        {
            XRGrabInteractable grabInteractable =
                _source.GrabInteractable;

            grabInteractable
                .ClearSingleGrabTransformers();

            grabInteractable
                .ClearMultipleGrabTransformers();

            base.Start();
        }

        public override void OnGrab(
            XRGrabInteractable grabInteractable)
        {
            base.OnGrab(
                grabInteractable);

            ResetState();

            _hasLastValidTargetRotation =
                false;

            _lastValidTargetRotation =
                ResolveSafeRotation(
                    grabInteractable.transform.rotation,
                    Quaternion.identity);

            _hasLastValidTargetRotation =
                true;

            _invalidRotationReported =
                false;

            RefreshRootManipulators();
        }

        public override void OnGrabCountChanged(
            XRGrabInteractable grabInteractable,
            Pose targetPose,
            Vector3 localScale)
        {
            base.OnGrabCountChanged(
                grabInteractable,
                targetPose,
                localScale);

            targetPose.rotation =
                ResolveSafeRotation(
                    targetPose.rotation,
                    grabInteractable.transform.rotation);

            InitializeForCurrentSelection(
                targetPose);
        }

        public override void OnUnlink(
            XRGrabInteractable grabInteractable)
        {
            ResetState();

            base.OnUnlink(
                grabInteractable);
        }

        public override void Process(
            XRGrabInteractable grabInteractable,
            XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose,
            ref Vector3 localScale)
        {
            /*
             * Валидный rotation на входе в текущий кадр.
             * Он станет fallback, если дальнейшая математика
             * создаст некорректный quaternion.
             */
            Quaternion frameFallbackRotation =
                ResolveSafeRotation(
                    targetPose.rotation,
                    grabInteractable.transform.rotation);

            targetPose.rotation =
                frameFallbackRotation;

            RefreshRootManipulators();

            int manipulatorCount =
                _rootManipulators.Count;

            if (manipulatorCount >= 2)
            {
                ProcessTwoManipulators(
                    ref targetPose);
            }
            else if (manipulatorCount == 1)
            {
                ProcessOneManipulator(
                    ref targetPose);
            }
            else
            {
                XRSocketInteractor socket =
                    _context.GetFirstSocket();

                if (socket != null)
                {
                    ProcessSocket(
                        socket,
                        ref targetPose);
                }
                else
                {
                    ResetState();
                }
            }

            /*
             * Последний защитный барьер.
             *
             * Невалидный quaternion никогда не должен
             * попасть обратно в XRGrabInteractable.
             */
            FinalizeTargetRotation(
                ref targetPose,
                frameFallbackRotation,
                updatePhase);
        }

        private void InitializeForCurrentSelection(
            Pose targetPose)
        {
            targetPose.rotation =
                ResolveSafeRotation(
                    targetPose.rotation,
                    _lastValidTargetRotation);

            RefreshRootManipulators();

            int manipulatorCount =
                _rootManipulators.Count;

            if (manipulatorCount >= 2)
            {
                TryInitializeTwoManipulators(
                    GetRootManipulator(0),
                    GetRootManipulator(1),
                    targetPose);

                return;
            }

            if (manipulatorCount == 1)
            {
                TryInitializeOneManipulator(
                    GetRootManipulator(0),
                    targetPose);

                return;
            }

            if (_context.GetFirstSocket() != null)
            {
                _mode =
                    GrabState.Socket;

                _firstInteractor =
                    null;

                _secondInteractor =
                    null;

                ResetRoutedTwoHandState();

                return;
            }

            ResetState();
        }

        private void ProcessOneManipulator(
            ref Pose targetPose)
        {
            IXRSelectInteractor interactor =
                GetRootManipulator(0);

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

            Quaternion attachRotation =
                ResolveSafeRotation(
                    interactorAttach.rotation,
                    targetPose.rotation);

            Pose interactorFrame =
                new Pose(
                    interactorAttach.position,
                    attachRotation);

            Pose calculatedPose =
                GrabPoseMath.ApplyRelativePose(
                    interactorFrame,
                    _localPositionOffset,
                    _localRotationOffset);

            calculatedPose.rotation =
                ResolveSafeRotation(
                    calculatedPose.rotation,
                    targetPose.rotation);

            targetPose =
                calculatedPose;
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

            IXRSelectInteractor controlInteractor =
                GetControlInteractorForPair(
                    first,
                    second);

            IXRSelectInteractor primaryInteractor =
                GetPrimaryInteractorForPair(
                    first,
                    second,
                    controlInteractor);

            bool requiresInitialization =
                _mode != GrabState.TwoManipulators ||
                !ReferenceEquals(
                    _firstInteractor,
                    first) ||
                !ReferenceEquals(
                    _secondInteractor,
                    second) ||
                !ReferenceEquals(
                    _routedControlInteractor,
                    controlInteractor) ||
                !ReferenceEquals(
                    _primaryInteractor,
                    primaryInteractor);

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

            if (_routedControlInteractor != null &&
                _primaryInteractor != null)
            {
                ProcessRoutedTwoManipulators(
                    ref targetPose);

                return;
            }

            ProcessStandardTwoManipulators(
                first,
                second,
                ref targetPose);
        }

        private void ProcessStandardTwoManipulators(
            IXRSelectInteractor first,
            IXRSelectInteractor second,
            ref Pose targetPose)
        {
            Transform firstAttach =
                _context.GetInteractorAttach(
                    first);

            Transform secondAttach =
                _context.GetInteractorAttach(
                    second);

            if (firstAttach == null ||
                secondAttach == null)
            {
                return;
            }

            Vector3 handsDelta =
                secondAttach.position -
                firstAttach.position;

            Pose pairFrame;

            if (!IsFinite(handsDelta) ||
                handsDelta.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
            {
                pairFrame =
                    new Pose(
                        (
                            firstAttach.position +
                            secondAttach.position
                        ) * 0.5f,
                        ResolveSafeRotation(
                            _lastPairRotation,
                            targetPose.rotation));
            }
            else
            {
                Quaternion fallbackRotation =
                    ResolveSafeRotation(
                        _lastPairRotation,
                        targetPose.rotation);

                bool frameCreated =
                    GrabPoseMath.TryCreateTwoHandFrame(
                        firstAttach,
                        secondAttach,
                        fallbackRotation,
                        out pairFrame);

                if (!frameCreated)
                    return;

                if (!TryNormalizeRotation(
                        pairFrame.rotation,
                        out Quaternion normalizedPairRotation))
                {
                    pairFrame.rotation =
                        fallbackRotation;
                }
                else
                {
                    pairFrame.rotation =
                        normalizedPairRotation;
                }

                _lastPairRotation =
                    pairFrame.rotation;
            }

            Pose calculatedPose =
                GrabPoseMath.ApplyRelativePose(
                    pairFrame,
                    _localPositionOffset,
                    _localRotationOffset);

            calculatedPose.rotation =
                ResolveSafeRotation(
                    calculatedPose.rotation,
                    targetPose.rotation);

            targetPose =
                calculatedPose;
        }

        private void ProcessRoutedTwoManipulators(
            ref Pose targetPose)
        {
            Transform primaryAttach =
                _context.GetInteractorAttach(
                    _primaryInteractor);

            Transform controlAttach =
                _context.GetInteractorAttach(
                    _routedControlInteractor);

            if (primaryAttach == null ||
                controlAttach == null)
            {
                return;
            }

            Quaternion primaryRotation =
                ResolveSafeRotation(
                    primaryAttach.rotation,
                    targetPose.rotation);

            Quaternion primaryStartRotation =
                ResolveSafeRotation(
                    _primaryStartAttachRotation,
                    primaryRotation);

            Quaternion startFrameRotation =
                ResolveSafeRotation(
                    _hybridStartFrameRotation,
                    targetPose.rotation);

            Quaternion primaryRotationDelta =
                ResolveSafeRotation(
                    primaryRotation *
                    Quaternion.Inverse(
                        primaryStartRotation),
                    Quaternion.identity);

            Quaternion primaryFrameRotation =
                ResolveSafeRotation(
                    primaryRotationDelta *
                    startFrameRotation,
                    targetPose.rotation);

            Vector3 handsDelta =
                controlAttach.position -
                primaryAttach.position;

            if (!IsFinite(handsDelta))
                return;

            float handsDistance =
                handsDelta.magnitude;

            float disableDistance =
                Mathf.Max(
                    0f,
                    _twoHandRotationDisableDistance);

            float enableDistance =
                Mathf.Max(
                    disableDistance + 0.001f,
                    _twoHandRotationEnableDistance);

            float fullDistance =
                Mathf.Max(
                    enableDistance + 0.001f,
                    _twoHandRotationFullDistance);

            /*
             * Baseline сохраняется один раз
             * в течение текущего grab.
             */
            if (!_pairBaselineCaptured &&
                handsDistance >= enableDistance)
            {
                CapturePairDirectionBaseline(
                    primaryRotation,
                    handsDelta);
            }

            /*
             * Гистерезис.
             */
            if (_pairRotationActive)
            {
                if (handsDistance <=
                    disableDistance)
                {
                    _pairRotationActive =
                        false;
                }
            }
            else
            {
                if (_pairBaselineCaptured &&
                    handsDistance >= enableDistance)
                {
                    _pairRotationActive =
                        true;
                }
            }

            Quaternion finalFrameRotation =
                primaryFrameRotation;

            if (_pairRotationActive &&
                _pairBaselineCaptured &&
                handsDelta.sqrMagnitude >
                MinimumDirectionSqrMagnitude)
            {
                Vector3 currentWorldDirection =
                    handsDelta.normalized;

                Vector3 currentDirectionInPrimarySpace =
                    Quaternion.Inverse(
                        primaryRotation) *
                    currentWorldDirection;

                if (IsFinite(
                        currentDirectionInPrimarySpace) &&
                    currentDirectionInPrimarySpace.sqrMagnitude >
                    MinimumDirectionSqrMagnitude)
                {
                    currentDirectionInPrimarySpace.Normalize();

                    Quaternion directionDeltaInPrimarySpace =
                        Quaternion.FromToRotation(
                            _pairBaselineDirectionInPrimarySpace,
                            currentDirectionInPrimarySpace);

                    directionDeltaInPrimarySpace =
                        ResolveSafeRotation(
                            directionDeltaInPrimarySpace,
                            Quaternion.identity);

                    Quaternion directionDeltaInWorldSpace =
                        ResolveSafeRotation(
                            primaryRotation *
                            directionDeltaInPrimarySpace *
                            Quaternion.Inverse(
                                primaryRotation),
                            Quaternion.identity);

                    Quaternion twoHandFrameRotation =
                        ResolveSafeRotation(
                            directionDeltaInWorldSpace *
                            primaryFrameRotation,
                            primaryFrameRotation);

                    float twoHandInfluence =
                        Mathf.InverseLerp(
                            enableDistance,
                            fullDistance,
                            handsDistance);

                    /*
                     * SmoothStep.
                     */
                    twoHandInfluence =
                        twoHandInfluence *
                        twoHandInfluence *
                        (
                            3f -
                            2f * twoHandInfluence
                        );

                    Quaternion blendedRotation =
                        Quaternion.Slerp(
                            primaryFrameRotation,
                            twoHandFrameRotation,
                            twoHandInfluence);

                    finalFrameRotation =
                        ResolveSafeRotation(
                            blendedRotation,
                            primaryFrameRotation);
                }
            }

            Pose hybridFrame =
                new Pose(
                    primaryAttach.position,
                    finalFrameRotation);

            Pose calculatedPose =
                GrabPoseMath.ApplyRelativePose(
                    hybridFrame,
                    _localPositionOffset,
                    _localRotationOffset);

            calculatedPose.rotation =
                ResolveSafeRotation(
                    calculatedPose.rotation,
                    targetPose.rotation);

            targetPose =
                calculatedPose;
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
                    ResolveSafeRotation(
                        root.rotation,
                        targetPose.rotation));

            Pose currentObjectAttachPose =
                new Pose(
                    objectAttach.position,
                    ResolveSafeRotation(
                        objectAttach.rotation,
                        currentRootPose.rotation));

            Pose targetSocketAttachPose =
                new Pose(
                    socketAttach.position,
                    ResolveSafeRotation(
                        socketAttach.rotation,
                        currentRootPose.rotation));

            Pose calculatedPose =
                GrabPoseMath.AlignRootToAttach(
                    currentRootPose,
                    currentObjectAttachPose,
                    targetSocketAttachPose);

            calculatedPose.rotation =
                ResolveSafeRotation(
                    calculatedPose.rotation,
                    targetPose.rotation);

            targetPose =
                calculatedPose;

            _mode =
                GrabState.Socket;

            _firstInteractor =
                null;

            _secondInteractor =
                null;

            ResetRoutedTwoHandState();
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

            Quaternion targetRotation =
                ResolveSafeRotation(
                    targetPose.rotation,
                    _lastValidTargetRotation);

            Quaternion attachRotation =
                ResolveSafeRotation(
                    interactorAttach.rotation,
                    targetRotation);

            Pose interactorFrame =
                new Pose(
                    interactorAttach.position,
                    attachRotation);

            Pose safeTargetPose =
                new Pose(
                    targetPose.position,
                    targetRotation);

            GrabPoseMath.CaptureRelativePose(
                interactorFrame,
                safeTargetPose,
                out _localPositionOffset,
                out _localRotationOffset);

            _localRotationOffset =
                ResolveSafeRotation(
                    _localRotationOffset,
                    Quaternion.identity);

            _mode =
                GrabState.OneManipulator;

            _firstInteractor =
                interactor;

            _secondInteractor =
                null;

            ResetRoutedTwoHandState();

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
                _context.GetInteractorAttach(
                    first);

            Transform secondAttach =
                _context.GetInteractorAttach(
                    second);

            if (firstAttach == null ||
                secondAttach == null)
            {
                ResetState();
                return false;
            }

            targetPose.rotation =
                ResolveSafeRotation(
                    targetPose.rotation,
                    _lastValidTargetRotation);

            IXRSelectInteractor controlInteractor =
                GetControlInteractorForPair(
                    first,
                    second);

            IXRSelectInteractor primaryInteractor =
                GetPrimaryInteractorForPair(
                    first,
                    second,
                    controlInteractor);

            /*
             * Корпус + управляющая рука каретки.
             */
            if (controlInteractor != null &&
                primaryInteractor != null)
            {
                Transform primaryAttach =
                    _context.GetInteractorAttach(
                        primaryInteractor);

                if (primaryAttach == null)
                {
                    ResetState();
                    return false;
                }

                Quaternion primaryRotation =
                    ResolveSafeRotation(
                        primaryAttach.rotation,
                        targetPose.rotation);

                Pose hybridFrame =
                    new Pose(
                        primaryAttach.position,
                        targetPose.rotation);

                GrabPoseMath.CaptureRelativePose(
                    hybridFrame,
                    targetPose,
                    out _localPositionOffset,
                    out _localRotationOffset);

                _localRotationOffset =
                    ResolveSafeRotation(
                        _localRotationOffset,
                        Quaternion.identity);

                _primaryStartAttachRotation =
                    primaryRotation;

                _hybridStartFrameRotation =
                    targetPose.rotation;

                _routedControlInteractor =
                    controlInteractor;

                _primaryInteractor =
                    primaryInteractor;

                _pairRotationActive =
                    false;

                _pairBaselineCaptured =
                    false;

                TryCaptureInitialPairBaseline();

                _mode =
                    GrabState.TwoManipulators;

                _firstInteractor =
                    first;

                _secondInteractor =
                    second;

                _lastPairRotation =
                    targetPose.rotation;

                return true;
            }

            /*
             * Обычный симметричный grab двумя руками.
             */
            Quaternion fallbackRotation =
                _mode == GrabState.TwoManipulators
                    ? ResolveSafeRotation(
                        _lastPairRotation,
                        targetPose.rotation)
                    : targetPose.rotation;

            Vector3 handsDelta =
                secondAttach.position -
                firstAttach.position;

            Pose pairFrame;

            if (!IsFinite(handsDelta) ||
                handsDelta.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
            {
                pairFrame =
                    new Pose(
                        (
                            firstAttach.position +
                            secondAttach.position
                        ) * 0.5f,
                        fallbackRotation);
            }
            else
            {
                bool frameCreated =
                    GrabPoseMath.TryCreateTwoHandFrame(
                        firstAttach,
                        secondAttach,
                        fallbackRotation,
                        out pairFrame);

                if (!frameCreated)
                {
                    ResetState();
                    return false;
                }

                pairFrame.rotation =
                    ResolveSafeRotation(
                        pairFrame.rotation,
                        fallbackRotation);
            }

            GrabPoseMath.CaptureRelativePose(
                pairFrame,
                targetPose,
                out _localPositionOffset,
                out _localRotationOffset);

            _localRotationOffset =
                ResolveSafeRotation(
                    _localRotationOffset,
                    Quaternion.identity);

            _lastPairRotation =
                ResolveSafeRotation(
                    pairFrame.rotation,
                    targetPose.rotation);

            _mode =
                GrabState.TwoManipulators;

            _firstInteractor =
                first;

            _secondInteractor =
                second;

            ResetRoutedTwoHandState();

            return true;
        }

        private void TryCaptureInitialPairBaseline()
        {
            Transform primaryAttach =
                _context.GetInteractorAttach(
                    _primaryInteractor);

            Transform controlAttach =
                _context.GetInteractorAttach(
                    _routedControlInteractor);

            if (primaryAttach == null ||
                controlAttach == null)
            {
                _pairRotationActive =
                    false;

                _pairBaselineCaptured =
                    false;

                return;
            }

            Quaternion primaryRotation =
                ResolveSafeRotation(
                    primaryAttach.rotation,
                    _lastValidTargetRotation);

            Vector3 handsDelta =
                controlAttach.position -
                primaryAttach.position;

            float enableDistance =
                Mathf.Max(
                    _twoHandRotationDisableDistance +
                    0.001f,
                    _twoHandRotationEnableDistance);

            if (!IsFinite(handsDelta) ||
                handsDelta.magnitude <
                enableDistance)
            {
                _pairRotationActive =
                    false;

                _pairBaselineCaptured =
                    false;

                return;
            }

            CapturePairDirectionBaseline(
                primaryRotation,
                handsDelta);
        }

        private void CapturePairDirectionBaseline(
            Quaternion primaryRotation,
            Vector3 handsDelta)
        {
            if (!IsFinite(handsDelta) ||
                handsDelta.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
            {
                _pairRotationActive =
                    false;

                _pairBaselineCaptured =
                    false;

                return;
            }

            primaryRotation =
                ResolveSafeRotation(
                    primaryRotation,
                    Quaternion.identity);

            Vector3 worldDirection =
                handsDelta.normalized;

            Vector3 localDirection =
                Quaternion.Inverse(
                    primaryRotation) *
                worldDirection;

            if (!IsFinite(localDirection) ||
                localDirection.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
            {
                _pairRotationActive =
                    false;

                _pairBaselineCaptured =
                    false;

                return;
            }

            _pairBaselineDirectionInPrimarySpace =
                localDirection.normalized;

            _pairBaselineCaptured =
                true;

            _pairRotationActive =
                true;
        }

        private void ResolveStablePair(
            out IXRSelectInteractor first,
            out IXRSelectInteractor second)
        {
            bool previousPairIsValid =
                _mode == GrabState.TwoManipulators &&
                ContainsRootManipulator(
                    _firstInteractor) &&
                ContainsRootManipulator(
                    _secondInteractor);

            if (previousPairIsValid)
            {
                first =
                    _firstInteractor;

                second =
                    _secondInteractor;

                return;
            }

            first =
                GetRootManipulator(0);

            second =
                GetRootManipulator(1);
        }

        private void RefreshRootManipulators()
        {
            _rootManipulators.Clear();

            if (_context == null)
                return;

            ResolveControlInputs();

            for (int i = 0;
                 i < _controlInputs.Length;
                 i++)
            {
                GrabRegionControlInput input =
                    _controlInputs[i];

                if (input == null ||
                    !input.isActiveAndEnabled)
                {
                    continue;
                }

                input.RefreshState();
            }

            IXRSelectInteractor controlInteractor =
                GetClaimedControlInteractor();

            /*
             * Обычные руки добавляются первыми.
             */
            for (int i = 0;
                 i < _context.ManipulatorCount;
                 i++)
            {
                IXRSelectInteractor interactor =
                    _context.GetManipulator(i);

                if (interactor == null)
                    continue;

                if (ReferenceEquals(
                        interactor,
                        controlInteractor))
                {
                    continue;
                }

                _rootManipulators.Add(
                    interactor);
            }

            /*
             * Управляющая рука добавляется последней.
             */
            if (controlInteractor != null &&
                _context.Contains(
                    controlInteractor))
            {
                _rootManipulators.Add(
                    controlInteractor);
            }
        }

        private IXRSelectInteractor
            GetClaimedControlInteractor()
        {
            if (_context == null)
                return null;

            for (int i = 0;
                 i < _context.ManipulatorCount;
                 i++)
            {
                IXRSelectInteractor interactor =
                    _context.GetManipulator(i);

                if (IsClaimedControlInteractor(
                        interactor))
                {
                    return interactor;
                }
            }

            return null;
        }

        private IXRSelectInteractor
            GetControlInteractorForPair(
                IXRSelectInteractor first,
                IXRSelectInteractor second)
        {
            if (IsClaimedControlInteractor(first))
                return first;

            if (IsClaimedControlInteractor(second))
                return second;

            return null;
        }

        private IXRSelectInteractor
            GetPrimaryInteractorForPair(
                IXRSelectInteractor first,
                IXRSelectInteractor second,
                IXRSelectInteractor controlInteractor)
        {
            if (controlInteractor == null)
                return null;

            if (ReferenceEquals(
                    first,
                    controlInteractor))
            {
                return second;
            }

            if (ReferenceEquals(
                    second,
                    controlInteractor))
            {
                return first;
            }

            return null;
        }

        /*
         * Здесь намеренно НЕ проверяется input.IsActive.
         *
         * IsActive может временно пропасть из-за pose/tracking,
         * но роль руки каретки должна сохраняться до отпускания.
         */
        private bool IsClaimedControlInteractor(
            IXRSelectInteractor interactor)
        {
            if (interactor == null ||
                _controlInputs == null ||
                _context == null)
            {
                return false;
            }

            if (!_context.Contains(interactor))
                return false;

            for (int i = 0;
                 i < _controlInputs.Length;
                 i++)
            {
                GrabRegionControlInput input =
                    _controlInputs[i];

                if (input == null ||
                    !input.isActiveAndEnabled)
                {
                    continue;
                }

                if (input.ClaimsInteractor(
                        interactor))
                {
                    return true;
                }
            }

            return false;
        }

        private IXRSelectInteractor GetRootManipulator(
            int index)
        {
            if (index < 0 ||
                index >= _rootManipulators.Count)
            {
                return null;
            }

            return _rootManipulators[index];
        }

        private bool ContainsRootManipulator(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            for (int i = 0;
                 i < _rootManipulators.Count;
                 i++)
            {
                if (ReferenceEquals(
                        _rootManipulators[i],
                        interactor))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveControlInputs()
        {
            bool hasValidInput =
                false;

            if (_controlInputs != null)
            {
                for (int i = 0;
                     i < _controlInputs.Length;
                     i++)
                {
                    if (_controlInputs[i] != null)
                    {
                        hasValidInput =
                            true;

                        break;
                    }
                }
            }

            if (hasValidInput)
                return;

            _controlInputs =
                GetComponentsInChildren<
                    GrabRegionControlInput>(true);

            if (_controlInputs == null)
            {
                _controlInputs =
                    new GrabRegionControlInput[0];
            }
        }

        private void FinalizeTargetRotation(
            ref Pose targetPose,
            Quaternion fallbackRotation,
            XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            if (TryNormalizeRotation(
                    targetPose.rotation,
                    out Quaternion normalizedRotation))
            {
                targetPose.rotation =
                    normalizedRotation;

                _lastValidTargetRotation =
                    normalizedRotation;

                _hasLastValidTargetRotation =
                    true;

                _invalidRotationReported =
                    false;

                return;
            }

            Quaternion recoveredRotation =
                ResolveSafeRotation(
                    fallbackRotation,
                    transform.rotation);

            targetPose.rotation =
                recoveredRotation;

            _lastValidTargetRotation =
                recoveredRotation;

            _hasLastValidTargetRotation =
                true;

            RecoverFromInvalidRotation();

            if (!_invalidRotationReported)
            {
                Debug.LogError(
                    $"{nameof(UniversalGrabTransformer)} on '{name}' " +
                    $"generated an invalid rotation during {updatePhase}. " +
                    "The rotation was rejected and the current grab " +
                    "state was reinitialized automatically.",
                    this);

                _invalidRotationReported =
                    true;
            }
        }

        private Quaternion ResolveSafeRotation(
            Quaternion preferred,
            Quaternion secondary)
        {
            if (TryNormalizeRotation(
                    preferred,
                    out Quaternion normalized))
            {
                return normalized;
            }

            if (_hasLastValidTargetRotation &&
                TryNormalizeRotation(
                    _lastValidTargetRotation,
                    out normalized))
            {
                return normalized;
            }

            if (TryNormalizeRotation(
                    secondary,
                    out normalized))
            {
                return normalized;
            }

            return Quaternion.identity;
        }

        private static bool TryNormalizeRotation(
            Quaternion rotation,
            out Quaternion normalized)
        {
            normalized =
                Quaternion.identity;

            if (!IsFinite(rotation.x) ||
                !IsFinite(rotation.y) ||
                !IsFinite(rotation.z) ||
                !IsFinite(rotation.w))
            {
                return false;
            }

            float sqrMagnitude =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;

            if (!IsFinite(sqrMagnitude) ||
                sqrMagnitude <
                MinimumQuaternionSqrMagnitude)
            {
                return false;
            }

            float inverseMagnitude =
                1f / Mathf.Sqrt(
                    sqrMagnitude);

            if (!IsFinite(inverseMagnitude))
                return false;

            normalized =
                new Quaternion(
                    rotation.x * inverseMagnitude,
                    rotation.y * inverseMagnitude,
                    rotation.z * inverseMagnitude,
                    rotation.w * inverseMagnitude);

            return
                IsFinite(normalized.x) &&
                IsFinite(normalized.y) &&
                IsFinite(normalized.z) &&
                IsFinite(normalized.w);
        }

        private static bool IsFinite(
            Quaternion rotation)
        {
            return
                IsFinite(rotation.x) &&
                IsFinite(rotation.y) &&
                IsFinite(rotation.z) &&
                IsFinite(rotation.w);
        }

        private static bool IsFinite(
            Vector3 vector)
        {
            return
                IsFinite(vector.x) &&
                IsFinite(vector.y) &&
                IsFinite(vector.z);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private void RecoverFromInvalidRotation()
        {
            /*
             * Сбрасываем только математическое состояние.
             *
             * Руки продолжают держать объект.
             * На следующем кадре offsets будут захвачены заново
             * от последнего валидного положения.
             */
            _mode =
                GrabState.None;

            _firstInteractor =
                null;

            _secondInteractor =
                null;

            _localPositionOffset =
                Vector3.zero;

            _localRotationOffset =
                Quaternion.identity;

            _lastPairRotation =
                ResolveSafeRotation(
                    _lastValidTargetRotation,
                    Quaternion.identity);

            ResetRoutedTwoHandState();
        }

        private void ResetRoutedTwoHandState()
        {
            _routedControlInteractor =
                null;

            _primaryInteractor =
                null;

            _primaryStartAttachRotation =
                Quaternion.identity;

            _hybridStartFrameRotation =
                Quaternion.identity;

            _pairRotationActive =
                false;

            _pairBaselineCaptured =
                false;

            _pairBaselineDirectionInPrimarySpace =
                Vector3.forward;
        }

        private void ResetState()
        {
            _mode =
                GrabState.None;

            _firstInteractor =
                null;

            _secondInteractor =
                null;

            _localPositionOffset =
                Vector3.zero;

            _localRotationOffset =
                Quaternion.identity;

            _lastPairRotation =
                ResolveSafeRotation(
                    _lastValidTargetRotation,
                    Quaternion.identity);

            ResetRoutedTwoHandState();
        }
    }
}