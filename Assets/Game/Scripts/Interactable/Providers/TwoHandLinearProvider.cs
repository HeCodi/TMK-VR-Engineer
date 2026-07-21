using Assets.Game.Scripts.Interactable.Core;
using Assets.Game.Scripts.Interactable.Drivers;
using Assets.Game.Scripts.Interactable.Inputs;
using Assets.Game.Scripts.Interactable.Interactions;
using Assets.Game.Scripts.Interactable.Movement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Assets.Game.Scripts.Interactable.Providers
{
    /// <summary>
    /// Готовая конфигурация для переносимого линейного механизма.
    ///
    /// Одна рука держит корпус объекта.
    /// Вторая рука двигает MovingPart вдоль заданной оси.
    ///
    /// Пример: штангенциркуль.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(BaseInteractable))]
    [RequireComponent(typeof(UniversalGrabTransformer))]
    public sealed class TwoHandLinearProvider : MonoBehaviour
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
            "Ось движения в локальных координатах AxisSpace.")]
        [SerializeField]
        private Vector3 _localAxis =
            Vector3.right;

        [Tooltip(
            "Смещение MovingPart при Value = 0.")]
        [SerializeField]
        private float _minimumPosition;

        [Tooltip(
            "Смещение MovingPart при Value = 1.")]
        [SerializeField]
        private float _maximumPosition =
            0.15f;

        [Tooltip(
            "Инвертирует управление рукой, " +
            "не разворачивая движение модели.")]
        [SerializeField]
        private bool _invertInput;

        [Header("Grab Region")]

        [SerializeField]
        [Min(0f)]
        private float _classificationTolerance =
            0.005f;

        [Tooltip(
            "Создать начальные коллайдеры корпуса и каретки.")]
        [SerializeField]
        private bool _createDefaultColliders =
            true;

        [Header("Editor")]

        [SerializeField]
        private bool _autoApply =
            true;

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
        private Transform _fixedGrabRegion;

        [SerializeField]
        [HideInInspector]
        private Transform _movingPart;

        [SerializeField]
        [HideInInspector]
        private Transform _movingVisual;

        [SerializeField]
        [HideInInspector]
        private Transform _movingGrabRegion;

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
        private UniversalGrabTransformer _grabTransformer;

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

        public Transform FixedVisual =>
            _fixedVisual;

        public Transform MovingVisual =>
            _movingVisual;

        public Transform FixedGrabRegion =>
            _fixedGrabRegion;

        public Transform MovingGrabRegion =>
            _movingGrabRegion;

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
            _fixedGrabRegion != null &&
            _movingPart != null &&
            _movingVisual != null &&
            _movingGrabRegion != null &&
            _controlAnchor != null &&
            _implementation != null &&
            _rigidbody != null &&
            _grabInteractable != null &&
            _interactable != null &&
            _grabTransformer != null &&
            _controlInput != null &&
            _linearInteraction != null &&
            _linearDriver != null;

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
            ResolveExistingStructure();

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

            if (!IsFinite(_minimumPosition))
                _minimumPosition = 0f;

            if (!IsFinite(_maximumPosition))
                _maximumPosition = 0.15f;

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

#if UNITY_EDITOR

        [ContextMenu("Create Or Repair Structure")]
        public void CreateOrRepairStructure()
        {
            if (Application.isPlaying)
                return;

            ValidateValues();

            int undoGroup =
                Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName(
                "Create Two Hand Linear Structure");

            Undo.RecordObject(
                this,
                "Update Two Hand Linear Provider");

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

            _fixedGrabRegion =
                EnsureChild(
                    _fixedGrabRegion,
                    _fixedPart,
                    GrabRegionName);

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

            _movingGrabRegion =
                EnsureChild(
                    _movingGrabRegion,
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

            EnsureDefaultColliders();

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
                return;

            ValidateValues();
            ResolveExistingStructure();

            if (!HasStructure)
            {
                Debug.LogError(
                    $"{nameof(TwoHandLinearProvider)} on '{name}' " +
                    "does not have a complete structure.",
                    this);

                return;
            }

            Undo.RecordObjects(
                new Object[]
                {
                    this,
                    _grabInteractable,
                    _controlInput,
                    _linearInteraction,
                    _linearDriver
                },
                "Apply Two Hand Linear Configuration");

            ApplyConfigurationInternal();
            MarkSceneDirty();
        }

        [ContextMenu("Refresh Grab Colliders")]
        public void RefreshGrabColliders()
        {
            ResolveExistingStructure();

            if (_grabInteractable == null)
                return;

            Undo.RecordObject(
                _grabInteractable,
                "Refresh Grab Colliders");

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

        public void SelectFixedGrabRegion()
        {
            if (_fixedGrabRegion != null)
            {
                Selection.activeGameObject =
                    _fixedGrabRegion.gameObject;
            }
        }

        public void SelectMovingGrabRegion()
        {
            if (_movingGrabRegion != null)
            {
                Selection.activeGameObject =
                    _movingGrabRegion.gameObject;
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

            _grabTransformer =
                EnsureComponent<UniversalGrabTransformer>(
                    gameObject);
        }

        private void ApplyConfigurationInternal()
        {
            if (!HasStructure)
                return;

            Collider[] movingRegionColliders =
                _movingGrabRegion
                    .GetComponentsInChildren<Collider>(
                        true);

            ConfigureControlInput(
                movingRegionColliders);

            ConfigureLinearInteraction();
            ConfigureLinearDriver();

            UpdateGrabInteractableColliders();

            SetDirtyAndRecordPrefab(
                _grabInteractable);

            SetDirtyAndRecordPrefab(
                _controlInput);

            SetDirtyAndRecordPrefab(
                _linearInteraction);

            SetDirtyAndRecordPrefab(
                _linearDriver);

            SetDirtyAndRecordPrefab(this);
        }

        private void ConfigureControlInput(
            Collider[] movingRegionColliders)
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
                movingRegionColliders);

            SetObjectReference(
                serialized,
                "_controlAnchor",
                _controlAnchor);

            SetFloat(
                serialized,
                "_classificationTolerance",
                _classificationTolerance);

            // Управление кареткой включается
            // только при наличии второй руки.
            SetBool(
                serialized,
                "_requireAnotherManipulator",
                true);

            serialized.ApplyModifiedProperties();
        }

        private void ConfigureLinearInteraction()
        {
            SerializedObject serialized =
                new SerializedObject(
                    _linearInteraction);

            serialized.Update();

            SetObjectReference(
                serialized,
                "_controlInput",
                _controlInput);

            // Reference — этот же переносимый объект.
            // Другая рука на корпусе становится опорной.
            SetObjectReference(
                serialized,
                "_referenceInteractable",
                _interactable);

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
            Collider[] allColliders =
                GetComponentsInChildren<Collider>(
                    true);

            _grabInteractable.colliders.Clear();

            for (int i = 0;
                 i < allColliders.Length;
                 i++)
            {
                Collider collider =
                    allColliders[i];

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

        private void EnsureDefaultColliders()
        {
            if (!_createDefaultColliders)
                return;

            Collider[] fixedColliders =
                _fixedGrabRegion
                    .GetComponentsInChildren<Collider>(
                        true);

            if (fixedColliders.Length == 0)
            {
                BoxCollider fixedCollider =
                    Undo.AddComponent<BoxCollider>(
                        _fixedGrabRegion.gameObject);

                fixedCollider.center =
                    new Vector3(
                        -0.05f,
                        0f,
                        0f);

                fixedCollider.size =
                    new Vector3(
                        0.14f,
                        0.04f,
                        0.04f);
            }

            Collider[] movingColliders =
                _movingGrabRegion
                    .GetComponentsInChildren<Collider>(
                        true);

            if (movingColliders.Length == 0)
            {
                BoxCollider movingCollider =
                    Undo.AddComponent<BoxCollider>(
                        _movingGrabRegion.gameObject);

                movingCollider.size =
                    new Vector3(
                        0.05f,
                        0.07f,
                        0.04f);
            }
        }

        private void ResolveExistingStructure()
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

            if (_grabTransformer == null)
            {
                _grabTransformer =
                    GetComponent<UniversalGrabTransformer>();
            }

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

            if (_fixedGrabRegion == null &&
                _fixedPart != null)
            {
                _fixedGrabRegion =
                    FindDirectChild(
                        _fixedPart,
                        GrabRegionName);
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

            if (_movingGrabRegion == null &&
                _movingPart != null)
            {
                _movingGrabRegion =
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
                property.floatValue = value;
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
                property.boolValue = value;
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
                property.vector3Value = value;
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
                $"{nameof(TwoHandLinearProvider)} could not find " +
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

    [CustomEditor(typeof(TwoHandLinearProvider))]
    public sealed class TwoHandLinearProviderEditor
        : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            TwoHandLinearProvider provider =
                (TwoHandLinearProvider)target;

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
                        "Refresh Grab Colliders"))
                {
                    provider.RefreshGrabColliders();
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
                        "Fixed Grab"))
                {
                    provider.SelectFixedGrabRegion();
                }

                if (GUILayout.Button(
                        "Moving Grab"))
                {
                    provider.SelectMovingGrabRegion();
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
                "Штангенциркуль:\n" +
                "1. Корпус модели -> FixedPart/Visual.\n" +
                "2. Каретка и подвижная губка -> MovingPart/Visual.\n" +
                "3. Настрой FixedPart/GrabRegion.\n" +
                "4. Настрой MovingPart/GrabRegion.\n" +
                "5. Совмести Local Axis с направлением движения.\n" +
                "6. Задай Maximum Position.",
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