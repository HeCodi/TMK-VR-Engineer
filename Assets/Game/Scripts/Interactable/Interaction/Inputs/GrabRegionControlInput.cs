using System.Collections.Generic;
using Assets.Game.Scripts.Interactable.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Inputs
{
    /// <summary>
    /// Получает управляющую руку из общего XRGrabInteractable
    /// по области, за которую был взят объект.
    ///
    /// Одна рука на области:
    /// - input не активируется;
    /// - рука остаётся обычным root-манипулятором;
    /// - перемещается весь предмет.
    ///
    /// Область + другая рука:
    /// - рука области становится ControlInput;
    /// - она исключается из движения корня;
    /// - другая рука перемещает корень.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GrabRegionControlInput : BaseControlInput
    {
        [Header("Grab Source")]

        [SerializeField]
        private BaseInteractable _interactable;

        [Tooltip(
            "Коллайдеры области управления. Для штангенциркуля — " +
            "коллайдеры подвижной каретки.")]
        [SerializeField]
        private Collider[] _regionColliders;

        [Tooltip(
            "Точка, используемая для выбора управляющей руки, " +
            "если область одновременно удерживают несколько рук.")]
        [SerializeField]
        private Transform _controlAnchor;

        [Tooltip(
            "Допуск при проверке точки захвата относительно Collider.")]
        [SerializeField]
        [Min(0f)]
        private float _classificationTolerance = 0.005f;

        [Tooltip(
            "Активировать управление областью только при наличии " +
            "другой руки на этом же XRGrabInteractable.")]
        [SerializeField]
        private bool _requireAnotherManipulator = true;

        private readonly HashSet<IXRSelectInteractor>
            _knownInteractors =
                new HashSet<IXRSelectInteractor>();

        private readonly HashSet<IXRSelectInteractor>
            _regionInteractors =
                new HashSet<IXRSelectInteractor>();

        private readonly List<IXRSelectInteractor>
            _currentManipulators =
                new List<IXRSelectInteractor>(4);

        private readonly List<IXRSelectInteractor>
            _removalBuffer =
                new List<IXRSelectInteractor>(4);

        private IXRSelectInteractor _activeInteractor;
        private bool _subscribed;

        public override object ActiveSource =>
            _activeInteractor;

        public BaseInteractable Interactable =>
            _interactable;

        public IXRSelectInteractor ActiveInteractor =>
            _activeInteractor;

        private void Awake()
        {
            if (!ResolveReferences(logError: true))
                enabled = false;
        }

        private void OnEnable()
        {
            if (!ResolveReferences(logError: true))
            {
                enabled = false;
                return;
            }

            Subscribe();
            RefreshState();
        }

        protected override void OnDisable()
        {
            Unsubscribe();
            ClearState();

            base.OnDisable();
        }

        private void Update()
        {
            RefreshState();
        }

        private void Reset()
        {
            _interactable =
                GetComponentInParent<BaseInteractable>();

            _regionColliders =
                GetComponentsInChildren<Collider>(true);

            if (_controlAnchor == null)
                _controlAnchor = transform;
        }

        private void OnValidate()
        {
            _classificationTolerance =
                Mathf.Max(
                    0f,
                    _classificationTolerance);

            if (_interactable == null)
            {
                _interactable =
                    GetComponentInParent<BaseInteractable>();
            }

            if (_controlAnchor == null)
                _controlAnchor = transform;
        }

        public override bool TryGetControlPose(
            out Pose pose)
        {
            pose = default;

            if (_activeInteractor == null ||
                _interactable == null ||
                _interactable.Context == null)
            {
                return false;
            }

            Transform attach =
                _interactable.Context.GetInteractorAttach(
                    _activeInteractor);

            if (attach == null)
                return false;

            pose = new Pose(
                attach.position,
                attach.rotation);

            return true;
        }

        /// <summary>
        /// Вызывается также из UniversalGrabTransformer,
        /// чтобы роли рук были определены до расчёта targetPose.
        /// </summary>
        public void RefreshState()
        {
            if (!ResolveReferences(logError: false))
            {
                ClearState();
                return;
            }

            InteractionContext context =
                _interactable.Context;

            if (context == null)
            {
                ClearState();
                return;
            }

            CollectCurrentManipulators(context);
            RemoveMissingInteractors();
            ClassifyNewInteractors(context);

            bool hasAnotherManipulator =
                HasNonRegionManipulator();

            bool mayActivate =
                !_requireAnotherManipulator ||
                hasAnotherManipulator;

            IXRSelectInteractor nextActive = null;

            if (mayActivate)
            {
                if (IsCurrentRegionInteractor(
                        _activeInteractor))
                {
                    nextActive = _activeInteractor;
                }
                else
                {
                    nextActive =
                        FindClosestRegionInteractor(
                            context);
                }
            }

            SetActiveInteractor(nextActive);
        }

        /// <summary>
        /// Возвращает true, если данный interactor сейчас должен
        /// управлять дочерним механизмом, а не корнем предмета.
        ///
        /// Когда input активен, исключаются все руки,
        /// захватившие эту область. В обычном VR это одна рука.
        /// </summary>
        public bool ClaimsInteractor(
            IXRSelectInteractor interactor)
        {
            if (!IsActive ||
                interactor == null)
            {
                return false;
            }

            return _regionInteractors.Contains(interactor);
        }

        private void CollectCurrentManipulators(
            InteractionContext context)
        {
            _currentManipulators.Clear();

            for (int i = 0;
                 i < context.ManipulatorCount;
                 i++)
            {
                IXRSelectInteractor interactor =
                    context.GetManipulator(i);

                if (!IsInteractorValid(interactor))
                    continue;

                _currentManipulators.Add(interactor);
            }
        }

        private void RemoveMissingInteractors()
        {
            _removalBuffer.Clear();

            foreach (IXRSelectInteractor known
                     in _knownInteractors)
            {
                if (!ContainsCurrent(known))
                    _removalBuffer.Add(known);
            }

            for (int i = 0;
                 i < _removalBuffer.Count;
                 i++)
            {
                IXRSelectInteractor removed =
                    _removalBuffer[i];

                _knownInteractors.Remove(removed);
                _regionInteractors.Remove(removed);
            }
        }

        private void ClassifyNewInteractors(
            InteractionContext context)
        {
            for (int i = 0;
                 i < _currentManipulators.Count;
                 i++)
            {
                IXRSelectInteractor interactor =
                    _currentManipulators[i];

                if (_knownInteractors.Contains(interactor))
                    continue;

                if (!TryClassifyInteractor(
                        context,
                        interactor,
                        out bool isRegionInteractor))
                {
                    /*
                     * Attach может быть ещё не создан в текущем кадре.
                     * Не добавляем interactor в known, чтобы повторить
                     * классификацию на следующем RefreshState.
                     */
                    continue;
                }

                _knownInteractors.Add(interactor);

                if (isRegionInteractor)
                    _regionInteractors.Add(interactor);
            }
        }

        private bool TryClassifyInteractor(
            InteractionContext context,
            IXRSelectInteractor interactor,
            out bool isRegionInteractor)
        {
            isRegionInteractor = false;

            Transform objectAttach =
                context.GetInteractableAttach(interactor);

            if (objectAttach == null)
                return false;

            isRegionInteractor =
                IsPointInsideRegion(
                    objectAttach.position);

            return true;
        }

        private bool IsPointInsideRegion(
            Vector3 worldPoint)
        {
            if (_regionColliders == null ||
                _regionColliders.Length == 0)
            {
                return false;
            }

            float toleranceSqr =
                _classificationTolerance *
                _classificationTolerance;

            for (int i = 0;
                 i < _regionColliders.Length;
                 i++)
            {
                Collider region =
                    _regionColliders[i];

                if (region == null)
                    continue;

                Vector3 closest =
                    region.ClosestPoint(worldPoint);

                if ((closest - worldPoint).sqrMagnitude
                    <= toleranceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasNonRegionManipulator()
        {
            for (int i = 0;
                 i < _currentManipulators.Count;
                 i++)
            {
                if (!_regionInteractors.Contains(
                        _currentManipulators[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private IXRSelectInteractor
            FindClosestRegionInteractor(
                InteractionContext context)
        {
            IXRSelectInteractor closestInteractor = null;
            float closestDistanceSqr =
                float.PositiveInfinity;

            Vector3 anchorPosition =
                _controlAnchor != null
                    ? _controlAnchor.position
                    : transform.position;

            for (int i = 0;
                 i < _currentManipulators.Count;
                 i++)
            {
                IXRSelectInteractor candidate =
                    _currentManipulators[i];

                if (!_regionInteractors.Contains(candidate))
                    continue;

                Transform objectAttach =
                    context.GetInteractableAttach(candidate);

                if (objectAttach == null)
                    continue;

                float distanceSqr =
                    (objectAttach.position -
                     anchorPosition).sqrMagnitude;

                if (distanceSqr >= closestDistanceSqr)
                    continue;

                closestDistanceSqr = distanceSqr;
                closestInteractor = candidate;
            }

            return closestInteractor;
        }

        private bool IsCurrentRegionInteractor(
            IXRSelectInteractor interactor)
        {
            return IsInteractorValid(interactor) &&
                   _regionInteractors.Contains(interactor) &&
                   ContainsCurrent(interactor);
        }

        private bool ContainsCurrent(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            for (int i = 0;
                 i < _currentManipulators.Count;
                 i++)
            {
                if (ReferenceEquals(
                        _currentManipulators[i],
                        interactor))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetActiveInteractor(
            IXRSelectInteractor interactor)
        {
            if (ReferenceEquals(
                    _activeInteractor,
                    interactor))
            {
                return;
            }

            _activeInteractor = interactor;

            NotifySourceChanged();
            SetInputActive(_activeInteractor != null);
        }

        private void Subscribe()
        {
            if (_subscribed ||
                _interactable == null)
            {
                return;
            }

            _interactable.Selected +=
                HandleSelectionChanged;

            _interactable.Deselected +=
                HandleSelectionChanged;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                _interactable == null)
            {
                return;
            }

            _interactable.Selected -=
                HandleSelectionChanged;

            _interactable.Deselected -=
                HandleSelectionChanged;

            _subscribed = false;
        }

        private void HandleSelectionChanged(
            IXRSelectInteractor interactor)
        {
            RefreshState();
        }

        private void ClearState()
        {
            _knownInteractors.Clear();
            _regionInteractors.Clear();
            _currentManipulators.Clear();
            _removalBuffer.Clear();

            SetActiveInteractor(null);
        }

        private bool ResolveReferences(bool logError)
        {
            if (_interactable == null)
            {
                _interactable =
                    GetComponentInParent<BaseInteractable>();
            }

            bool valid =
                _interactable != null &&
                _regionColliders != null &&
                _regionColliders.Length > 0;

            if (!valid && logError)
            {
                Debug.LogError(
                    $"{nameof(GrabRegionControlInput)} on '{name}' " +
                    $"requires a BaseInteractable and at least one " +
                    $"region Collider.",
                    this);
            }

            return valid;
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