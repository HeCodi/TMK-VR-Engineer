using Assets.Game.Scripts.Interactable.Core;
using Assets.Game.Scripts.Interactable.Drivers;
using Assets.Game.Scripts.Interactable.Inputs;
using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Assets.Game.Scripts.Interactable.Providers
{
    /// <summary>
    /// Provider для стационарного линейного механизма,
    /// управляемого одной рукой.
    ///
    /// Примеры:
    /// - выдвижной ящик;
    /// - штора;
    /// - заслонка;
    /// - линейный ползунок;
    /// - выдвижная панель.
    ///
    /// Создаёт структуру:
    ///
    /// Root
    /// ├── AxisSpace
    /// │   ├── Origin
    /// │   ├── FixedPart
    /// │   │   └── Visual
    /// │   └── MovingPart
    /// │       ├── Visual
    /// │       ├── GrabRegion
    /// │       └── ControlAnchor
    /// └── Implementation
    ///
    /// На Implementation создаются:
    /// - GrabRegionControlInput;
    /// - LinearInteraction;
    /// - LinearDriver.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(BaseInteractable))]
    public sealed class OneHandLinearProvider : MonoBehaviour
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        private const float MinimumRange =
            0.0001f;

        private const string AxisSpaceName =
            "AxisSpace";

        private const string OriginName =
            "Origin";

        private const string FixedPartName =
            "FixedPart";

        private const string MovingPartName =
            "MovingPart";

        private const string VisualName =
            "Visual";

        private const string GrabRegionName =
            "GrabRegion";

        private const string ControlAnchorName =
            "ControlAnchor";

        private const string ImplementationName =
            "Implementation";

        [Header("Linear Movement")]

        [Tooltip(
            "Ось движения в локальных координатах AxisSpace.\n\n" +
            "Например:\n" +
            "X = Vector3.right\n" +
            "Y = Vector3.up\n" +
            "Z = Vector3.forward")]
        [SerializeField]
        private Vector3 _localAxis =
            Vector3.right;

        [Tooltip(
            "Смещение MovingPart при Value = 0.")]
        [SerializeField]
        private float _minimumPosition =
            0f;

        [Tooltip(
            "Смещение MovingPart при Value = 1.\n\n" +
            "Для ящика это максимальная глубина выдвижения.")]
        [SerializeField]
        private float _maximumPosition =
            0.4f;

        [Tooltip(
            "Инвертирует управление рукой, " +
            "не меняя движение модели.")]
        [SerializeField]
        private bool _invertInput;

        [Header("Grab Region")]

        [Tooltip(
            "Допуск при определении точки захвата " +
            "внутри GrabRegion.")]
        [SerializeField]
        [Min(0f)]
        private float _classificationTolerance =
            0.005f;

        [Tooltip(
            "Создать стандартный BoxCollider " +
            "на GrabRegion.")]
        [SerializeField]
        private bool _createDefaultCollider =
            true;

        [Header("Stationary Root")]

        [Tooltip(
            "Зафиксировать корень механизма. " +
            "Для ящика, шкафа и шторы должно быть включено.")]
        [SerializeField]
        private bool _keepRootStationary =
            true;

        [Header("Editor")]

        [Tooltip(
            "Автоматически переносить настройки Provider " +
            "во внутренние компоненты.")]
        [SerializeField]
        private bool _autoApply =
            true;

        [Header("Generated References")]

        [SerializeField]
        [HideInInspector]
        private Transform _axisSpace;

        [SerializeField]
        [HideInInspector]
        private Transform _origin;

        [SerializeField]
        [HideInInspector]
        private Transform _fixedPart;

        [SerializeField]
        [HideInInspector]
        private Transform _fixedVisual;

        [SerializeField]
        [HideInInspector]
        private Transform _movingPart;

        [SerializeField]
        [HideInInspector]
        private Transform _movingVisual;

        [SerializeField]
        [HideInInspector]
        private Transform _grabRegion;

        [SerializeField]
        [HideInInspector]
        private Transform _controlAnchor;

        [SerializeField]
        [HideInInspector]
        private Transform _implementation;

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
        private GrabRegionControlInput _controlInput;

        [SerializeField]
        [HideInInspector]
        private LinearInteraction _linearInteraction;

        [SerializeField]
        [HideInInspector]
        private LinearDriver _linearDriver;

#if UNITY_EDITOR
        private bool _creationQueued;
        private bool _applyQueued;
#endif

        public Transform AxisSpace =>
            _axisSpace;

        public Transform Origin =>
            _origin;

        public Transform FixedPart =>
            _fixedPart;

        public Transform FixedVisual =>
            _fixedVisual;

        public Transform MovingPart =>
            _movingPart;

        public Transform MovingVisual =>
            _movingVisual;

        public Transform GrabRegion =>
            _grabRegion;

        public Transform ControlAnchor =>
            _controlAnchor;

        public GrabRegionControlInput ControlInput =>
            _controlInput;

        public LinearInteraction Interaction =>
            _linearInteraction;

        public LinearDriver Driver =>
            _linearDriver;

        public bool HasStructure =>
            _axisSpace != null &&
            _origin != null &&
            _fixedPart != null &&
            _fixedVisual != null &&
            _movingPart != null &&
            _movingVisual != null &&
            _grabRegion != null &&
            _controlAnchor != null &&
            _implementation != null &&
            _rigidbody != null &&
            _grabInteractable != null &&
            _interactable != null &&
            _controlInput != null &&
            _linearInteraction != null &&
            _linearDriver != null;

        private void Awake()
        {
            ResolveExistingReferences();
            ApplyRuntimeRootSettings();
        }

        private void OnEnable()
        {
            ApplyRuntimeRootSettings();
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

#if UNITY_EDITOR
            ResolveExistingReferences();

            if (_autoApply &&
                HasStructure &&
                !Application.isPlaying)
            {
                QueueApplyConfiguration();
            }
#endif
        }

        private void ApplyRuntimeRootSettings()
        {
            if (!_keepRootStationary)
                return;

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
                /*
                 * XRGrabInteractable используется для Select
                 * и получения attach point руки.
                 *
                 * Сам корень механизма двигаться не должен.
                 */
                _grabInteractable.trackPosition =
                    false;

                _grabInteractable.trackRotation =
                    false;

                _grabInteractable.trackScale =
                    false;

                _grabInteractable.addDefaultGrabTransformers =
                    false;
            }
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

            if (!IsFinite(_minimumPosition))
            {
                _minimumPosition =
                    0f;
            }

            if (!IsFinite(_maximumPosition))
            {
                _maximumPosition =
                    0.4f;
            }

            if (Mathf.Abs(
                    _maximumPosition -
                    _minimumPosition) <
                MinimumRange)
            {
                _maximumPosition =
                    _minimumPosition + 0.001f;
            }

            if (!IsFinite(
                    _classificationTolerance))
            {
                _classificationTolerance =
                    0.005f;
            }

            _classificationTolerance =
                Mathf.Max(
                    0f,
                    _classificationTolerance);
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
        }

#if UNITY_EDITOR

        [ContextMenu("Create Or Repair Structure")]
        public void CreateOrRepairStructure()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"{nameof(OneHandLinearProvider)} cannot create " +
                    "structure during Play Mode.",
                    this);

                return;
            }

            ValidateValues();

            int undoGroup =
                Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName(
                "Create One Hand Linear Structure");

            Undo.RecordObject(
                this,
                "Update One Hand Linear Provider");

            ResolveExistingStructure();
            EnsureRootComponents();

            _axisSpace =
                EnsureChild(
                    _axisSpace,
                    transform,
                    AxisSpaceName);

            _origin =
                EnsureChild(
                    _origin,
                    _axisSpace,
                    OriginName);

            _fixedPart =
                EnsureChild(
                    _fixedPart,
                    _axisSpace,
                    FixedPartName);

            _fixedVisual =
                EnsureChild(
                    _fixedVisual,
                    _fixedPart,
                    VisualName);

            _movingPart =
                EnsureChild(
                    _movingPart,
                    _axisSpace,
                    MovingPartName);

            _movingVisual =
                EnsureChild(
                    _movingVisual,
                    _movingPart,
                    VisualName);

            _grabRegion =
                EnsureChild(
                    _grabRegion,
                    _movingPart,
                    GrabRegionName);

            _controlAnchor =
                EnsureChild(
                    _controlAnchor,
                    _movingPart,
                    ControlAnchorName);

            _implementation =
                EnsureChild(
                    _implementation,
                    transform,
                    ImplementationName);

            EnsureDefaultCollider();

            _controlInput =
                EnsureComponent<GrabRegionControlInput>(
                    _implementation.gameObject);

            _linearInteraction =
                EnsureComponent<LinearInteraction>(
                    _implementation.gameObject);

            _linearDriver =
                EnsureComponent<LinearDriver>(
                    _implementation.gameObject);

            ApplyConfigurationInternal();

            EditorUtility.SetDirty(this);

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
                    $"{nameof(OneHandLinearProvider)} cannot change " +
                    "generated components during Play Mode.",
                    this);

                return;
            }

            ValidateValues();
            ResolveExistingStructure();

            if (!HasStructure)
            {
                Debug.LogError(
                    $"{nameof(OneHandLinearProvider)} on '{name}' " +
                    "does not have a complete structure. " +
                    "Use Create Or Repair Structure first.",
                    this);

                return;
            }

            Undo.RecordObjects(
                new Object[]
                {
                    this,
                    _rigidbody,
                    _grabInteractable,
                    _interactable,
                    _controlInput,
                    _linearInteraction,
                    _linearDriver
                },
                "Apply One Hand Linear Configuration");

            ApplyConfigurationInternal();
            MarkSceneDirty();
        }

        [ContextMenu("Refresh Grab Collider")]
        public void RefreshGrabCollider()
        {
            ResolveExistingStructure();

            if (_grabInteractable == null ||
                _grabRegion == null)
            {
                return;
            }

            Undo.RecordObject(
                _grabInteractable,
                "Refresh One Hand Linear Collider");

            UpdateGrabInteractableColliders();

            SetDirtyAndRecordPrefab(
                _grabInteractable);

            MarkSceneDirty();
        }

        public void SelectFixedVisual()
        {
            if (_fixedVisual != null)
            {
                Selection.activeGameObject =
                    _fixedVisual.gameObject;
            }
        }

        public void SelectMovingVisual()
        {
            if (_movingVisual != null)
            {
                Selection.activeGameObject =
                    _movingVisual.gameObject;
            }
        }

        public void SelectGrabRegion()
        {
            if (_grabRegion != null)
            {
                Selection.activeGameObject =
                    _grabRegion.gameObject;
            }
        }

        public void SelectControlAnchor()
        {
            if (_controlAnchor != null)
            {
                Selection.activeGameObject =
                    _controlAnchor.gameObject;
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

        private void ApplyConfigurationInternal()
        {
            if (!HasStructure)
                return;

            ApplyRuntimeRootSettings();

            Collider[] regionColliders =
                _grabRegion
                    .GetComponentsInChildren<Collider>(
                        true);

            ConfigureControlInput(
                regionColliders);

            ConfigureLinearInteraction();
            ConfigureLinearDriver();

            UpdateGrabInteractableColliders();

            SetDirtyAndRecordPrefab(
                _rigidbody);

            SetDirtyAndRecordPrefab(
                _grabInteractable);

            SetDirtyAndRecordPrefab(
                _interactable);

            SetDirtyAndRecordPrefab(
                _controlInput);

            SetDirtyAndRecordPrefab(
                _linearInteraction);

            SetDirtyAndRecordPrefab(
                _linearDriver);

            SetDirtyAndRecordPrefab(this);
        }

        private void ConfigureControlInput(
            Collider[] regionColliders)
        {
            SerializedObject serialized =
                new SerializedObject(
                    _controlInput);

            serialized.Update();

            SetObjectReference(
                serialized,
                "_interactable",
                _interactable);

            SetObjectReferenceArray(
                serialized,
                "_regionColliders",
                regionColliders);

            SetObjectReference(
                serialized,
                "_controlAnchor",
                _controlAnchor);

            SetFloat(
                serialized,
                "_classificationTolerance",
                _classificationTolerance);

            /*
             * Главное отличие от TwoHandLinearProvider:
             * дополнительная рука не требуется.
             */
            SetBool(
                serialized,
                "_requireAnotherManipulator",
                false);

            serialized.ApplyModifiedProperties();
        }

        private void ConfigureLinearInteraction()
        {
            SerializedObject serialized =
                new SerializedObject(
                    _linearInteraction);

            serialized.Update();

            /*
             * Поля BasePoseValueInteraction.
             */
            SetObjectReference(
                serialized,
                "_controlInput",
                _controlInput);

            /*
             * Reference отсутствует:
             * механизм управляется одной рукой
             * относительно AxisSpace.
             */
            SetObjectReference(
                serialized,
                "_referenceInteractable",
                null);

            /*
             * Поля LinearInteraction.
             */
            SetObjectReference(
                serialized,
                "_origin",
                _origin);

            SetObjectReference(
                serialized,
                "_axisSpace",
                _axisSpace);

            SetVector3(
                serialized,
                "_localAxis",
                _localAxis);

            SetFloat(
                serialized,
                "_minimumDistance",
                _minimumPosition);

            SetFloat(
                serialized,
                "_maximumDistance",
                _maximumPosition);

            SetBool(
                serialized,
                "_invertInput",
                _invertInput);

            serialized.ApplyModifiedProperties();
        }

        private void ConfigureLinearDriver()
        {
            SerializedObject serialized =
                new SerializedObject(
                    _linearDriver);

            serialized.Update();

            SetObjectReference(
                serialized,
                "_source",
                _linearInteraction);

            SetObjectReference(
                serialized,
                "_target",
                _movingPart);

            SetVector3(
                serialized,
                "_localAxis",
                _localAxis);

            SetFloat(
                serialized,
                "_minimumOffset",
                _minimumPosition);

            SetFloat(
                serialized,
                "_maximumOffset",
                _maximumPosition);

            serialized.ApplyModifiedProperties();
        }

        private void UpdateGrabInteractableColliders()
        {
            if (_grabInteractable == null ||
                _grabRegion == null)
            {
                return;
            }

            Collider[] regionColliders =
                _grabRegion
                    .GetComponentsInChildren<Collider>(
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
        }

        private void EnsureDefaultCollider()
        {
            if (!_createDefaultCollider ||
                _grabRegion == null)
            {
                return;
            }

            Collider[] existingColliders =
                _grabRegion
                    .GetComponentsInChildren<Collider>(
                        true);

            if (existingColliders.Length > 0)
                return;

            BoxCollider boxCollider =
                Undo.AddComponent<BoxCollider>(
                    _grabRegion.gameObject);

            boxCollider.center =
                Vector3.zero;

            boxCollider.size =
                new Vector3(
                    0.12f,
                    0.06f,
                    0.04f);
        }

        private void ResolveExistingStructure()
        {
            ResolveExistingReferences();

            if (_axisSpace == null)
            {
                _axisSpace =
                    FindDirectChild(
                        transform,
                        AxisSpaceName);
            }

            if (_origin == null &&
                _axisSpace != null)
            {
                _origin =
                    FindDirectChild(
                        _axisSpace,
                        OriginName);
            }

            if (_fixedPart == null &&
                _axisSpace != null)
            {
                _fixedPart =
                    FindDirectChild(
                        _axisSpace,
                        FixedPartName);
            }

            if (_fixedVisual == null &&
                _fixedPart != null)
            {
                _fixedVisual =
                    FindDirectChild(
                        _fixedPart,
                        VisualName);
            }

            if (_movingPart == null &&
                _axisSpace != null)
            {
                _movingPart =
                    FindDirectChild(
                        _axisSpace,
                        MovingPartName);
            }

            if (_movingVisual == null &&
                _movingPart != null)
            {
                _movingVisual =
                    FindDirectChild(
                        _movingPart,
                        VisualName);
            }

            if (_grabRegion == null &&
                _movingPart != null)
            {
                _grabRegion =
                    FindDirectChild(
                        _movingPart,
                        GrabRegionName);
            }

            if (_controlAnchor == null &&
                _movingPart != null)
            {
                _controlAnchor =
                    FindDirectChild(
                        _movingPart,
                        ControlAnchorName);
            }

            if (_implementation == null)
            {
                _implementation =
                    FindDirectChild(
                        transform,
                        ImplementationName);
            }

            if (_implementation == null)
                return;

            if (_controlInput == null)
            {
                _controlInput =
                    _implementation
                        .GetComponent<GrabRegionControlInput>();
            }

            if (_linearInteraction == null)
            {
                _linearInteraction =
                    _implementation
                        .GetComponent<LinearInteraction>();
            }

            if (_linearDriver == null)
            {
                _linearDriver =
                    _implementation
                        .GetComponent<LinearDriver>();
            }
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

                return current;
            }

            if (current.parent != parent)
            {
                Undo.SetTransformParent(
                    current,
                    parent,
                    $"Repair {childName} Parent");
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

        private static void SetObjectReferenceArray(
            SerializedObject serialized,
            string propertyName,
            Object[] values)
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

            int count =
                values != null
                    ? values.Length
                    : 0;

            property.arraySize =
                count;

            for (int i = 0;
                 i < count;
                 i++)
            {
                property
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue =
                    values[i];
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
                $"{nameof(OneHandLinearProvider)} could not find " +
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

    [CustomEditor(typeof(OneHandLinearProvider))]
    public sealed class OneHandLinearProviderEditor
        : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            OneHandLinearProvider provider =
                (OneHandLinearProvider)target;

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(
                "Structure",
                EditorStyles.boldLabel);

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

                if (GUILayout.Button(
                        "Refresh Grab Collider"))
                {
                    provider.RefreshGrabCollider();
                }
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Fixed Visual"))
                {
                    provider.SelectFixedVisual();
                }

                if (GUILayout.Button(
                        "Moving Visual"))
                {
                    provider.SelectMovingVisual();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Grab Region"))
                {
                    provider.SelectGrabRegion();
                }

                if (GUILayout.Button(
                        "Control Anchor"))
                {
                    provider.SelectControlAnchor();
                }
            }

            EditorGUILayout.Space(10f);

            if (!provider.HasStructure)
            {
                EditorGUILayout.HelpBox(
                    "Нажми Create / Repair Structure.",
                    MessageType.Info);

                return;
            }

            EditorGUILayout.HelpBox(
                "Настройка:\n" +
                "1. Неподвижную модель помести в FixedPart/Visual.\n" +
                "2. Выдвигаемую часть помести в MovingPart/Visual.\n" +
                "3. Настрой Collider внутри MovingPart/GrabRegion.\n" +
                "4. Поставь ControlAnchor около ручки захвата.\n" +
                "5. Совмести Local Axis с направлением движения.\n" +
                "6. Укажи Maximum Position.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField(
                "Generated Components",
                EditorStyles.boldLabel);

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
                    typeof(LinearInteraction),
                    true);

                EditorGUILayout.ObjectField(
                    "Driver",
                    provider.Driver,
                    typeof(LinearDriver),
                    true);
            }
        }
    }

#endif
}