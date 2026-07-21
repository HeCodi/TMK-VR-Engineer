using Assets.Game.Scripts.Interactable.Core;
using Assets.Game.Scripts.Interactable.Inputs;
using Assets.Game.Scripts.Interactable.Mechanics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Assets.Game.Scripts.Interactable.Providers
{
    public enum SwitchMotionType
    {
        Rotation,
        Linear
    }

    /// <summary>
    /// Высокоуровневый Provider двухпозиционного переключателя.
    ///
    /// Поддерживает:
    /// - вращательный переключатель;
    /// - линейный переключатель;
    /// - автоматическую защёлку в 0 или 1;
    /// - события включения и выключения.
    ///
    /// Внутри использует:
    ///
    /// ControlInput
    /// → Interaction
    /// → DetentMechanic
    /// ├→ Driver
    /// └→ ThresholdMechanic
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(520)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(BaseInteractable))]
    public sealed class SwitchProvider : MonoBehaviour
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        private const float MinimumRange =
            0.0001f;

        private const string SwitchImplementationName =
            "SwitchImplementation";

        [Header("Switch Type")]

        [SerializeField]
        private SwitchMotionType _motionType =
            SwitchMotionType.Rotation;

        [Header("Common Movement")]

        [Tooltip(
            "Ось движения в локальной системе AxisSpace.")]
        [SerializeField]
        private Vector3 _localAxis =
            Vector3.right;

        [Tooltip(
            "Инвертировать управление рукой.")]
        [SerializeField]
        private bool _invertInput;

        [Header("Rotation Switch")]

        [Tooltip(
            "Угол переключателя в состоянии Off.")]
        [SerializeField]
        private float _minimumAngle =
            -25f;

        [Tooltip(
            "Угол переключателя в состоянии On.")]
        [SerializeField]
        private float _maximumAngle =
            25f;

        [Header("Linear Switch")]

        [Tooltip(
            "Положение переключателя в состоянии Off.")]
        [SerializeField]
        private float _minimumPosition =
            0f;

        [Tooltip(
            "Положение переключателя в состоянии On.")]
        [SerializeField]
        private float _maximumPosition =
            0.035f;

        [Header("Detent")]

        [Tooltip(
            "Скорость автоматического движения " +
            "к ближайшему состоянию после отпускания.")]
        [SerializeField]
        [Min(0.0001f)]
        private float _snapSpeed =
            8f;

        [Tooltip(
            "Автоматически защёлкнуть переключатель " +
            "при включении объекта.")]
        [SerializeField]
        private bool _snapOnEnable =
            true;

        [Header("State Threshold")]

        [Tooltip(
            "При достижении этого значения " +
            "переключатель считается включённым.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _activateThreshold =
            0.6f;

        [Tooltip(
            "При падении до этого значения " +
            "переключатель считается выключенным.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _deactivateThreshold =
            0.4f;

        [Header("Grab Region")]

        [SerializeField]
        [Min(0f)]
        private float _classificationTolerance =
            0.005f;

        [SerializeField]
        private bool _createDefaultCollider =
            true;

        [Header("Rotation Tracking")]

        [SerializeField]
        [Min(0f)]
        private float _minimumTrackingRadius =
            0.01f;

        [SerializeField]
        [Min(0f)]
        private float _resumeTrackingRadius =
            0.015f;

        [SerializeField]
        [Range(0f, 180f)]
        private float _maximumDeltaAnglePerFrame =
            120f;

        [Header("Root")]

        [Tooltip(
            "Корень переключателя остаётся неподвижным.")]
        [SerializeField]
        private bool _keepRootStationary =
            true;

        [Header("Editor")]

        [SerializeField]
        private bool _autoApply =
            true;

        [Header("Generated References")]

        [SerializeField]
        [HideInInspector]
        private Rigidbody _rigidbody;

        [SerializeField]
        [HideInInspector]
        private XRGrabInteractable _grabInteractable;

        [SerializeField]
        [HideInInspector]
        private BaseInteractable _interactable;

        [SerializeField]
        [HideInInspector]
        private OneHandRotationProvider
            _rotationProvider;

        [SerializeField]
        [HideInInspector]
        private OneHandLinearProvider
            _linearProvider;

        [SerializeField]
        [HideInInspector]
        private Transform _switchImplementation;

        [SerializeField]
        [HideInInspector]
        private DetentMechanic _detentMechanic;

        [SerializeField]
        [HideInInspector]
        private ThresholdMechanic _thresholdMechanic;

#if UNITY_EDITOR
        private bool _creationQueued;
        private bool _applyQueued;
#endif

        public SwitchMotionType MotionType =>
            _motionType;

        public DetentMechanic Detent =>
            _detentMechanic;

        public ThresholdMechanic Threshold =>
            _thresholdMechanic;

        public GrabRegionControlInput ControlInput
        {
            get
            {
                if (_motionType ==
                    SwitchMotionType.Rotation)
                {
                    return _rotationProvider != null
                        ? _rotationProvider.ControlInput
                        : null;
                }

                return _linearProvider != null
                    ? _linearProvider.ControlInput
                    : null;
            }
        }

        public MonoBehaviour Interaction
        {
            get
            {
                if (_motionType ==
                    SwitchMotionType.Rotation)
                {
                    return _rotationProvider != null
                        ? _rotationProvider.Interaction
                        : null;
                }

                return _linearProvider != null
                    ? _linearProvider.Interaction
                    : null;
            }
        }

        public MonoBehaviour Driver
        {
            get
            {
                if (_motionType ==
                    SwitchMotionType.Rotation)
                {
                    return _rotationProvider != null
                        ? _rotationProvider.Driver
                        : null;
                }

                return _linearProvider != null
                    ? _linearProvider.Driver
                    : null;
            }
        }

        public Transform Visual
        {
            get
            {
                if (_motionType ==
                    SwitchMotionType.Rotation)
                {
                    return _rotationProvider != null
                        ? _rotationProvider.Visual
                        : null;
                }

                return _linearProvider != null
                    ? _linearProvider.MovingVisual
                    : null;
            }
        }

        public Transform GrabRegion
        {
            get
            {
                if (_motionType ==
                    SwitchMotionType.Rotation)
                {
                    return _rotationProvider != null
                        ? _rotationProvider.GrabRegion
                        : null;
                }

                return _linearProvider != null
                    ? _linearProvider.GrabRegion
                    : null;
            }
        }

        public Transform ControlAnchor
        {
            get
            {
                if (_motionType ==
                    SwitchMotionType.Rotation)
                {
                    return _rotationProvider != null
                        ? _rotationProvider.TrackingPoint
                        : null;
                }

                return _linearProvider != null
                    ? _linearProvider.ControlAnchor
                    : null;
            }
        }

        public bool HasStructure
        {
            get
            {
                bool selectedProviderReady =
                    _motionType ==
                    SwitchMotionType.Rotation
                        ? _rotationProvider != null &&
                          _rotationProvider.HasStructure
                        : _linearProvider != null &&
                          _linearProvider.HasStructure;

                return
                    selectedProviderReady &&
                    _rigidbody != null &&
                    _grabInteractable != null &&
                    _interactable != null &&
                    _switchImplementation != null &&
                    _detentMechanic != null &&
                    _thresholdMechanic != null;
            }
        }

        private void Awake()
        {
            ResolveExistingReferences();
            ApplyRuntimeRootSettings();
            ApplyRuntimeModeState();
        }

        private void OnEnable()
        {
            ApplyRuntimeRootSettings();
            ApplyRuntimeModeState();
        }

        private void Reset()
        {
            ValidateValues();

#if UNITY_EDITOR
            QueueStructureCreation();
#endif
        }

        private void OnValidate()
        {
            ValidateValues();
            ResolveExistingReferences();

#if UNITY_EDITOR
            if (_autoApply &&
                HasStructure &&
                !Application.isPlaying)
            {
                QueueApplyConfiguration();
            }
#endif
        }

        private void ValidateValues()
        {
            if (!IsFinite(_localAxis) ||
                _localAxis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                _localAxis =
                    Vector3.right;
            }

            if (!IsFinite(_minimumAngle))
                _minimumAngle = -25f;

            if (!IsFinite(_maximumAngle))
                _maximumAngle = 25f;

            if (Mathf.Abs(
                    _maximumAngle -
                    _minimumAngle) <
                MinimumRange)
            {
                _maximumAngle =
                    _minimumAngle + 1f;
            }

            if (!IsFinite(_minimumPosition))
                _minimumPosition = 0f;

            if (!IsFinite(_maximumPosition))
                _maximumPosition = 0.035f;

            if (Mathf.Abs(
                    _maximumPosition -
                    _minimumPosition) <
                MinimumRange)
            {
                _maximumPosition =
                    _minimumPosition + 0.001f;
            }

            if (!IsFinite(_snapSpeed))
                _snapSpeed = 8f;

            _snapSpeed =
                Mathf.Max(
                    0.0001f,
                    _snapSpeed);

            _classificationTolerance =
                Mathf.Max(
                    0f,
                    _classificationTolerance);

            _activateThreshold =
                Mathf.Clamp01(
                    _activateThreshold);

            _deactivateThreshold =
                Mathf.Clamp(
                    _deactivateThreshold,
                    0f,
                    _activateThreshold);

            _minimumTrackingRadius =
                Mathf.Max(
                    0f,
                    _minimumTrackingRadius);

            _resumeTrackingRadius =
                Mathf.Max(
                    _minimumTrackingRadius,
                    _resumeTrackingRadius);

            _maximumDeltaAnglePerFrame =
                Mathf.Clamp(
                    _maximumDeltaAnglePerFrame,
                    0f,
                    180f);
        }

        private void ResolveExistingReferences()
        {
            if (_rigidbody == null)
            {
                _rigidbody =
                    GetComponent<Rigidbody>();
            }

            if (_grabInteractable == null)
            {
                _grabInteractable =
                    GetComponent<XRGrabInteractable>();
            }

            if (_interactable == null)
            {
                _interactable =
                    GetComponent<BaseInteractable>();
            }

            if (_rotationProvider == null)
            {
                _rotationProvider =
                    GetComponent<OneHandRotationProvider>();
            }

            if (_linearProvider == null)
            {
                _linearProvider =
                    GetComponent<OneHandLinearProvider>();
            }

            if (_switchImplementation == null)
            {
                _switchImplementation =
                    FindDirectChild(
                        transform,
                        SwitchImplementationName);
            }

            if (_switchImplementation == null)
                return;

            if (_detentMechanic == null)
            {
                _detentMechanic =
                    _switchImplementation
                        .GetComponent<DetentMechanic>();
            }

            if (_thresholdMechanic == null)
            {
                _thresholdMechanic =
                    _switchImplementation
                        .GetComponent<ThresholdMechanic>();
            }
        }

        private void ApplyRuntimeRootSettings()
        {
            if (!_keepRootStationary)
                return;

            if (_rigidbody != null)
            {
                _rigidbody.useGravity =
                    false;

                _rigidbody.isKinematic =
                    true;

                _rigidbody.constraints =
                    RigidbodyConstraints.FreezeAll;
            }

            if (_grabInteractable != null)
            {
                _grabInteractable.trackPosition =
                    false;

                _grabInteractable.trackRotation =
                    false;

                _grabInteractable.trackScale =
                    false;

                _grabInteractable
                    .addDefaultGrabTransformers =
                    false;
            }
        }

        private void ApplyRuntimeModeState()
        {
            bool rotationActive =
                _motionType ==
                SwitchMotionType.Rotation;

            SetProviderRuntimeState(
                _rotationProvider,
                rotationActive);

            SetProviderRuntimeState(
                _linearProvider,
                !rotationActive);
        }

        private static void SetProviderRuntimeState(
            OneHandRotationProvider provider,
            bool active)
        {
            if (provider == null)
                return;

            provider.enabled =
                active;

            if (provider.ControlInput != null)
            {
                provider.ControlInput.enabled =
                    active;
            }

            if (provider.Interaction != null)
            {
                provider.Interaction.enabled =
                    active;
            }

            if (provider.Driver != null)
            {
                provider.Driver.enabled =
                    active;
            }

            SetColliderState(
                provider.GrabRegion,
                active);
        }

        private static void SetProviderRuntimeState(
            OneHandLinearProvider provider,
            bool active)
        {
            if (provider == null)
                return;

            provider.enabled =
                active;

            if (provider.ControlInput != null)
            {
                provider.ControlInput.enabled =
                    active;
            }

            if (provider.Interaction != null)
            {
                provider.Interaction.enabled =
                    active;
            }

            if (provider.Driver != null)
            {
                provider.Driver.enabled =
                    active;
            }

            SetColliderState(
                provider.GrabRegion,
                active);
        }

        private static void SetColliderState(
            Transform region,
            bool active)
        {
            if (region == null)
                return;

            Collider[] colliders =
                region.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled =
                        active;
                }
            }
        }

#if UNITY_EDITOR

        [ContextMenu("Create Or Repair Structure")]
        public void CreateOrRepairStructure()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"{nameof(SwitchProvider)} cannot create " +
                    "structure during Play Mode.",
                    this);

                return;
            }

            ValidateValues();

            int undoGroup =
                Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName(
                "Create Switch Structure");

            Undo.RecordObject(
                this,
                "Update Switch Provider");

            EnsureRootComponents();

            _switchImplementation =
                EnsureChild(
                    _switchImplementation,
                    transform,
                    SwitchImplementationName);

            _detentMechanic =
                EnsureComponent<DetentMechanic>(
                    _switchImplementation.gameObject);

            _thresholdMechanic =
                EnsureComponent<ThresholdMechanic>(
                    _switchImplementation.gameObject);

            EnsureSelectedProvider();
            ConfigureSelectedProvider();
            CreateSelectedProviderStructure();

            ConfigureSwitchMechanics();
            ConfigureSelectedDriver();
            UpdateGrabInteractableColliders();

            ApplyRuntimeRootSettings();
            ApplyRuntimeModeState();

            SetDirtyAndRecordPrefab(this);
            SetDirtyAndRecordPrefab(_rigidbody);
            SetDirtyAndRecordPrefab(_grabInteractable);
            SetDirtyAndRecordPrefab(_interactable);
            SetDirtyAndRecordPrefab(_detentMechanic);
            SetDirtyAndRecordPrefab(_thresholdMechanic);

            Undo.CollapseUndoOperations(
                undoGroup);

            MarkSceneDirty();
        }

        [ContextMenu("Apply Configuration")]
        public void ApplyConfiguration()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"{nameof(SwitchProvider)} cannot change " +
                    "configuration during Play Mode.",
                    this);

                return;
            }

            ValidateValues();
            ResolveExistingReferences();

            if (!HasStructure)
            {
                CreateOrRepairStructure();
                return;
            }

            ConfigureSelectedProvider();
            ApplySelectedProviderConfiguration();

            ConfigureSwitchMechanics();
            ConfigureSelectedDriver();
            UpdateGrabInteractableColliders();

            ApplyRuntimeRootSettings();
            ApplyRuntimeModeState();

            SetDirtyAndRecordPrefab(this);
            SetDirtyAndRecordPrefab(_detentMechanic);
            SetDirtyAndRecordPrefab(_thresholdMechanic);

            MarkSceneDirty();
        }

        public void SelectVisual()
        {
            if (Visual != null)
            {
                Selection.activeGameObject =
                    Visual.gameObject;
            }
        }

        public void SelectGrabRegion()
        {
            if (GrabRegion != null)
            {
                Selection.activeGameObject =
                    GrabRegion.gameObject;
            }
        }

        public void SelectControlAnchor()
        {
            if (ControlAnchor != null)
            {
                Selection.activeGameObject =
                    ControlAnchor.gameObject;
            }
        }

        public void SelectSwitchImplementation()
        {
            if (_switchImplementation != null)
            {
                Selection.activeGameObject =
                    _switchImplementation.gameObject;
            }
        }

        private void EnsureRootComponents()
        {
            _rigidbody =
                EnsureComponent<Rigidbody>(
                    gameObject);

            _grabInteractable =
                EnsureComponent<XRGrabInteractable>(
                    gameObject);

            _interactable =
                EnsureComponent<BaseInteractable>(
                    gameObject);
        }

        private void EnsureSelectedProvider()
        {
            if (_motionType ==
                SwitchMotionType.Rotation)
            {
                _rotationProvider =
                    EnsureComponent<OneHandRotationProvider>(
                        gameObject);

                SetGeneratedProviderFlags(
                    _rotationProvider);

                return;
            }

            _linearProvider =
                EnsureComponent<OneHandLinearProvider>(
                    gameObject);

            SetGeneratedProviderFlags(
                _linearProvider);
        }

        private static void SetGeneratedProviderFlags(
            MonoBehaviour provider)
        {
            if (provider == null)
                return;

            provider.hideFlags =
                HideFlags.HideInInspector;

            EditorUtility.SetDirty(
                provider);
        }

        private void ConfigureSelectedProvider()
        {
            if (_motionType ==
                SwitchMotionType.Rotation)
            {
                ConfigureRotationProvider();
                return;
            }

            ConfigureLinearProvider();
        }

        private void ConfigureRotationProvider()
        {
            if (_rotationProvider == null)
                return;

            SerializedObject serialized =
                new SerializedObject(
                    _rotationProvider);

            serialized.Update();

            SetObjectReference(
                serialized,
                "_interactable",
                _interactable);

            SetObjectReference(
                serialized,
                "_referenceInteractable",
                null);

            SetVector3(
                serialized,
                "_localAxis",
                _localAxis);

            SetFloat(
                serialized,
                "_minimumAngle",
                _minimumAngle);

            SetFloat(
                serialized,
                "_maximumAngle",
                _maximumAngle);

            SetBool(
                serialized,
                "_invertInput",
                _invertInput);

            SetFloat(
                serialized,
                "_classificationTolerance",
                _classificationTolerance);

            SetBool(
                serialized,
                "_createDefaultBoxCollider",
                _createDefaultCollider);

            SetFloat(
                serialized,
                "_minimumTrackingRadius",
                _minimumTrackingRadius);

            SetFloat(
                serialized,
                "_resumeTrackingRadius",
                _resumeTrackingRadius);

            SetFloat(
                serialized,
                "_maximumDeltaAnglePerFrame",
                _maximumDeltaAnglePerFrame);

            SetBool(
                serialized,
                "_autoApply",
                false);

            serialized.ApplyModifiedProperties();
        }

        private void ConfigureLinearProvider()
        {
            if (_linearProvider == null)
                return;

            SerializedObject serialized =
                new SerializedObject(
                    _linearProvider);

            serialized.Update();

            SetVector3(
                serialized,
                "_localAxis",
                _localAxis);

            SetFloat(
                serialized,
                "_minimumPosition",
                _minimumPosition);

            SetFloat(
                serialized,
                "_maximumPosition",
                _maximumPosition);

            SetBool(
                serialized,
                "_invertInput",
                _invertInput);

            SetFloat(
                serialized,
                "_classificationTolerance",
                _classificationTolerance);

            SetBool(
                serialized,
                "_createDefaultCollider",
                _createDefaultCollider);

            SetBool(
                serialized,
                "_keepRootStationary",
                _keepRootStationary);

            SetBool(
                serialized,
                "_autoApply",
                false);

            serialized.ApplyModifiedProperties();
        }

        private void CreateSelectedProviderStructure()
        {
            if (_motionType ==
                SwitchMotionType.Rotation)
            {
                _rotationProvider
                    .CreateOrRepairStructure();

                return;
            }

            _linearProvider
                .CreateOrRepairStructure();
        }

        private void ApplySelectedProviderConfiguration()
        {
            if (_motionType ==
                SwitchMotionType.Rotation)
            {
                _rotationProvider
                    .ApplyConfiguration();

                return;
            }

            _linearProvider
                .ApplyConfiguration();
        }

        private void ConfigureSwitchMechanics()
        {
            if (_detentMechanic == null ||
                _thresholdMechanic == null ||
                Interaction == null)
            {
                return;
            }

            SerializedObject detentSerialized =
                new SerializedObject(
                    _detentMechanic);

            detentSerialized.Update();

            SetObjectReference(
                detentSerialized,
                "_source",
                Interaction);

            SetFloatArray(
                detentSerialized,
                "_detents",
                new[]
                {
                    0f,
                    1f
                });

            SetFloat(
                detentSerialized,
                "_snapSpeed",
                _snapSpeed);

            SetBool(
                detentSerialized,
                "_snapOnEnable",
                _snapOnEnable);

            detentSerialized.ApplyModifiedProperties();

            SerializedObject thresholdSerialized =
                new SerializedObject(
                    _thresholdMechanic);

            thresholdSerialized.Update();

            SetObjectReference(
                thresholdSerialized,
                "_source",
                _detentMechanic);

            SetFloat(
                thresholdSerialized,
                "_activateThreshold",
                _activateThreshold);

            SetFloat(
                thresholdSerialized,
                "_deactivateThreshold",
                _deactivateThreshold);

            thresholdSerialized.ApplyModifiedProperties();
        }

        private void ConfigureSelectedDriver()
        {
            if (Driver == null ||
                _detentMechanic == null)
            {
                return;
            }

            SerializedObject serialized =
                new SerializedObject(
                    Driver);

            serialized.Update();

            SetObjectReference(
                serialized,
                "_source",
                _detentMechanic);

            serialized.ApplyModifiedProperties();

            SetDirtyAndRecordPrefab(
                Driver);
        }

        private void UpdateGrabInteractableColliders()
        {
            if (_grabInteractable == null ||
                GrabRegion == null)
            {
                return;
            }

            Collider[] regionColliders =
                GrabRegion.GetComponentsInChildren<Collider>(
                    true);

            _grabInteractable.colliders.Clear();

            for (int i = 0;
                 i < regionColliders.Length;
                 i++)
            {
                Collider collider =
                    regionColliders[i];

                if (collider == null)
                    continue;

                if (_grabInteractable
                    .colliders
                    .Contains(collider))
                {
                    continue;
                }

                _grabInteractable
                    .colliders
                    .Add(collider);
            }

            SetDirtyAndRecordPrefab(
                _grabInteractable);
        }

        private Transform EnsureChild(
            Transform current,
            Transform parent,
            string childName)
        {
            if (current == null)
            {
                current =
                    FindDirectChild(
                        parent,
                        childName);
            }

            if (current == null)
            {
                GameObject child =
                    new GameObject(
                        childName);

                Undo.RegisterCreatedObjectUndo(
                    child,
                    $"Create {childName}");

                current =
                    child.transform;

                current.SetParent(
                    parent,
                    false);

                current.localPosition =
                    Vector3.zero;

                current.localRotation =
                    Quaternion.identity;

                current.localScale =
                    Vector3.one;
            }

            return current;
        }

        private T EnsureComponent<T>(
            GameObject target)
            where T : Component
        {
            T component =
                target.GetComponent<T>();

            if (component != null)
                return component;

            return Undo.AddComponent<T>(
                target);
        }

        private void QueueStructureCreation()
        {
            if (_creationQueued)
                return;

            _creationQueued = true;

            EditorApplication.delayCall +=
                ApplyQueuedStructureCreation;
        }

        private void ApplyQueuedStructureCreation()
        {
            _creationQueued = false;

            if (this != null &&
                !Application.isPlaying)
            {
                CreateOrRepairStructure();
            }
        }

        private void QueueApplyConfiguration()
        {
            if (_applyQueued)
                return;

            _applyQueued = true;

            EditorApplication.delayCall +=
                ApplyQueuedConfiguration;
        }

        private void ApplyQueuedConfiguration()
        {
            _applyQueued = false;

            if (this != null &&
                !Application.isPlaying &&
                _autoApply &&
                HasStructure)
            {
                ApplyConfiguration();
            }
        }

        private static void SetObjectReference(
            SerializedObject serialized,
            string propertyName,
            Object value)
        {
            SerializedProperty property =
                FindRequiredProperty(
                    serialized,
                    propertyName);

            if (property != null)
            {
                property.objectReferenceValue =
                    value;
            }
        }

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                FindRequiredProperty(
                    serialized,
                    propertyName);

            if (property != null)
            {
                property.floatValue =
                    value;
            }
        }

        private static void SetBool(
            SerializedObject serialized,
            string propertyName,
            bool value)
        {
            SerializedProperty property =
                FindRequiredProperty(
                    serialized,
                    propertyName);

            if (property != null)
            {
                property.boolValue =
                    value;
            }
        }

        private static void SetVector3(
            SerializedObject serialized,
            string propertyName,
            Vector3 value)
        {
            SerializedProperty property =
                FindRequiredProperty(
                    serialized,
                    propertyName);

            if (property != null)
            {
                property.vector3Value =
                    value;
            }
        }

        private static void SetFloatArray(
            SerializedObject serialized,
            string propertyName,
            float[] values)
        {
            SerializedProperty property =
                FindRequiredProperty(
                    serialized,
                    propertyName);

            if (property == null ||
                !property.isArray)
            {
                return;
            }

            property.arraySize =
                values != null
                    ? values.Length
                    : 0;

            for (int i = 0;
                 i < property.arraySize;
                 i++)
            {
                property
                    .GetArrayElementAtIndex(i)
                    .floatValue =
                    values[i];
            }
        }

        private static SerializedProperty FindRequiredProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
                return property;

            Debug.LogError(
                $"{nameof(SwitchProvider)} could not find " +
                $"field '{propertyName}' on " +
                $"{serialized.targetObject.GetType().Name}.",
                serialized.targetObject);

            return null;
        }

        private static void SetDirtyAndRecordPrefab(
            Object target)
        {
            if (target == null)
                return;

            EditorUtility.SetDirty(target);

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    target);
        }

        private void MarkSceneDirty()
        {
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    gameObject.scene);
            }
        }

#endif

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0;
                 i < parent.childCount;
                 i++)
            {
                Transform child =
                    parent.GetChild(i);

                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static bool IsFinite(
            Vector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(SwitchProvider))]
    public sealed class SwitchProviderEditor : Editor
    {
        private SerializedProperty _motionType;

        private SerializedProperty _localAxis;
        private SerializedProperty _invertInput;

        private SerializedProperty _minimumAngle;
        private SerializedProperty _maximumAngle;

        private SerializedProperty _minimumPosition;
        private SerializedProperty _maximumPosition;

        private SerializedProperty _snapSpeed;
        private SerializedProperty _snapOnEnable;

        private SerializedProperty _activateThreshold;
        private SerializedProperty _deactivateThreshold;

        private SerializedProperty _classificationTolerance;
        private SerializedProperty _createDefaultCollider;

        private SerializedProperty _minimumTrackingRadius;
        private SerializedProperty _resumeTrackingRadius;
        private SerializedProperty _maximumDeltaAnglePerFrame;

        private SerializedProperty _keepRootStationary;
        private SerializedProperty _autoApply;

        private void OnEnable()
        {
            _motionType =
                serializedObject.FindProperty(
                    "_motionType");

            _localAxis =
                serializedObject.FindProperty(
                    "_localAxis");

            _invertInput =
                serializedObject.FindProperty(
                    "_invertInput");

            _minimumAngle =
                serializedObject.FindProperty(
                    "_minimumAngle");

            _maximumAngle =
                serializedObject.FindProperty(
                    "_maximumAngle");

            _minimumPosition =
                serializedObject.FindProperty(
                    "_minimumPosition");

            _maximumPosition =
                serializedObject.FindProperty(
                    "_maximumPosition");

            _snapSpeed =
                serializedObject.FindProperty(
                    "_snapSpeed");

            _snapOnEnable =
                serializedObject.FindProperty(
                    "_snapOnEnable");

            _activateThreshold =
                serializedObject.FindProperty(
                    "_activateThreshold");

            _deactivateThreshold =
                serializedObject.FindProperty(
                    "_deactivateThreshold");

            _classificationTolerance =
                serializedObject.FindProperty(
                    "_classificationTolerance");

            _createDefaultCollider =
                serializedObject.FindProperty(
                    "_createDefaultCollider");

            _minimumTrackingRadius =
                serializedObject.FindProperty(
                    "_minimumTrackingRadius");

            _resumeTrackingRadius =
                serializedObject.FindProperty(
                    "_resumeTrackingRadius");

            _maximumDeltaAnglePerFrame =
                serializedObject.FindProperty(
                    "_maximumDeltaAnglePerFrame");

            _keepRootStationary =
                serializedObject.FindProperty(
                    "_keepRootStationary");

            _autoApply =
                serializedObject.FindProperty(
                    "_autoApply");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();

            //EditorGUILayout.Space(6f);

            //DrawHeader("Switch Type");

            EditorGUILayout.PropertyField(
                _motionType);

            SwitchMotionType motionType =
                (SwitchMotionType)
                _motionType.enumValueIndex;

            //EditorGUILayout.Space(6f);

            //DrawHeader("Movement");

            EditorGUILayout.PropertyField(
                _localAxis);

            EditorGUILayout.PropertyField(
                _invertInput);

            EditorGUILayout.Space(4f);

            if (motionType ==
                SwitchMotionType.Rotation)
            {
                DrawRotationSettings();
            }
            else
            {
                DrawLinearSettings();
            }

            EditorGUILayout.Space(6f);

            DrawDetentSettings();
            DrawThresholdSettings();
            DrawGrabRegionSettings();

            if (motionType ==
                SwitchMotionType.Rotation)
            {
                DrawRotationTrackingSettings();
            }

            DrawRootSettings();
            DrawEditorSettings();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(12f);

            DrawStructureControls(
                (SwitchProvider)target);
        }

        private void DrawScriptField()
        {
            SerializedProperty scriptProperty =
                serializedObject.FindProperty(
                    "m_Script");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    scriptProperty);
            }
        }

        private void DrawRotationSettings()
        {
            //DrawHeader("Rotation Switch");

            EditorGUILayout.PropertyField(
                _minimumAngle);

            EditorGUILayout.PropertyField(
                _maximumAngle);
        }

        private void DrawLinearSettings()
        {
            //DrawHeader("Linear Switch");

            EditorGUILayout.PropertyField(
                _minimumPosition);

            EditorGUILayout.PropertyField(
                _maximumPosition);
        }

        private void DrawDetentSettings()
        {
            //EditorGUILayout.Space(6f);

            //DrawHeader("Detent");

            EditorGUILayout.PropertyField(
                _snapSpeed);

            EditorGUILayout.PropertyField(
                _snapOnEnable);
        }

        private void DrawThresholdSettings()
        {
            //EditorGUILayout.Space(6f);

            //DrawHeader("State Threshold");

            EditorGUILayout.PropertyField(
                _activateThreshold);

            EditorGUILayout.PropertyField(
                _deactivateThreshold);

            if (_deactivateThreshold.floatValue >
                _activateThreshold.floatValue)
            {
                EditorGUILayout.HelpBox(
                    "Deactivate Threshold не должен быть " +
                    "больше Activate Threshold.",
                    MessageType.Warning);
            }
        }

        private void DrawGrabRegionSettings()
        {
            //EditorGUILayout.Space(6f);

            //DrawHeader("Grab Region");

            EditorGUILayout.PropertyField(
                _classificationTolerance);

            EditorGUILayout.PropertyField(
                _createDefaultCollider);
        }

        private void DrawRotationTrackingSettings()
        {
            //EditorGUILayout.Space(6f);

            //DrawHeader("Rotation Tracking");

            EditorGUILayout.PropertyField(
                _minimumTrackingRadius);

            EditorGUILayout.PropertyField(
                _resumeTrackingRadius);

            EditorGUILayout.PropertyField(
                _maximumDeltaAnglePerFrame);
        }

        private void DrawRootSettings()
        {
            //EditorGUILayout.Space(6f);

            //DrawHeader("Root");

            EditorGUILayout.PropertyField(
                _keepRootStationary);
        }

        private void DrawEditorSettings()
        {
            //EditorGUILayout.Space(6f);

            //DrawHeader("Editor");

            EditorGUILayout.PropertyField(
                _autoApply);
        }

        private static void DrawStructureControls(
            SwitchProvider provider)
        {
            DrawHeader("Generated Structure");

            if (GUILayout.Button(
                    "Create / Repair Structure",
                    GUILayout.Height(32f)))
            {
                provider.CreateOrRepairStructure();
            }

            using (new EditorGUI.DisabledScope(
                       !provider.HasStructure))
            {
                if (GUILayout.Button(
                        "Apply Configuration"))
                {
                    provider.ApplyConfiguration();
                }
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           provider.Visual == null))
                {
                    if (GUILayout.Button("Visual"))
                    {
                        provider.SelectVisual();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           provider.GrabRegion == null))
                {
                    if (GUILayout.Button("Grab Region"))
                    {
                        provider.SelectGrabRegion();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           provider.ControlAnchor == null))
                {
                    if (GUILayout.Button("Anchor"))
                    {
                        provider.SelectControlAnchor();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(
                       provider.Detent == null ||
                       provider.Threshold == null))
            {
                if (GUILayout.Button(
                        "Switch Events / Mechanics"))
                {
                    provider.SelectSwitchImplementation();
                }
            }

            EditorGUILayout.Space(10f);

            DrawHelpBox(provider);

            if (!provider.HasStructure)
                return;

            EditorGUILayout.Space(6f);

            DrawGeneratedComponents(provider);
        }

        private static void DrawHelpBox(
            SwitchProvider provider)
        {
            if (!provider.HasStructure)
            {
                EditorGUILayout.HelpBox(
                    "Выбери тип движения и нажми " +
                    "Create / Repair Structure.",
                    MessageType.Info);

                return;
            }

            if (provider.MotionType ==
                SwitchMotionType.Rotation)
            {
                EditorGUILayout.HelpBox(
                    "Вращательный переключатель:\n" +
                    "1. Помести рычаг в Visual.\n" +
                    "2. Поставь Pivot на ось вращения.\n" +
                    "3. Настрой Grab Region.\n" +
                    "4. Поставь Anchor около места захвата.\n" +
                    "5. Укажи углы Off и On.",
                    MessageType.Info);

                return;
            }

            EditorGUILayout.HelpBox(
                "Линейный переключатель:\n" +
                "1. Помести кнопку или ползунок в Visual.\n" +
                "2. Настрой Grab Region.\n" +
                "3. Поставь Anchor около места захвата.\n" +
                "4. Укажи положения Off и On.",
                MessageType.Info);
        }

        private static void DrawGeneratedComponents(
            SwitchProvider provider)
        {
            DrawHeader("Generated Components");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Control Input",
                    provider.ControlInput,
                    typeof(GrabRegionControlInput),
                    true);

                EditorGUILayout.ObjectField(
                    "Interaction",
                    provider.Interaction,
                    typeof(MonoBehaviour),
                    true);

                EditorGUILayout.ObjectField(
                    "Detent",
                    provider.Detent,
                    typeof(DetentMechanic),
                    true);

                EditorGUILayout.ObjectField(
                    "Threshold",
                    provider.Threshold,
                    typeof(ThresholdMechanic),
                    true);

                EditorGUILayout.ObjectField(
                    "Driver",
                    provider.Driver,
                    typeof(MonoBehaviour),
                    true);
            }
        }

        private static void DrawHeader(
            string title)
        {
            EditorGUILayout.LabelField(
                title,
                EditorStyles.boldLabel);
        }
    }

#endif
}