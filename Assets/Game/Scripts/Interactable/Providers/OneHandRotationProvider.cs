using Assets.Game.Scripts.Interactable.Core;
using Assets.Game.Scripts.Interactable.Drivers;
using Assets.Game.Scripts.Interactable.Inputs;
using Assets.Game.Scripts.Interactable.Interactions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Assets.Game.Scripts.Interactable.Providers
{
    /// <summary>
    /// Высокоуровневый Provider для паттерна:
    ///
    ///     одна рука вращает часть объекта вокруг оси.
    ///
    /// Создаёт структуру:
    ///
    /// Provider Root
    /// ├── AxisSpace
    /// │   └── Pivot
    /// │       ├── Visual
    /// │       ├── GrabRegion
    /// │       └── TrackingPoint
    /// └── Implementation
    ///
    /// На Implementation создаются:
    /// - GrabRegionControlInput;
    /// - RotationInteraction;
    /// - RotationDriver.
    ///
    /// Provider не содержит runtime-логики вращения.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OneHandRotationProvider : MonoBehaviour
    {
        private const float MinimumAxisSqrMagnitude =
            0.000001f;

        private const float MinimumAngleRange =
            0.001f;

        private const string AxisSpaceName =
            "AxisSpace";

        private const string PivotName =
            "Pivot";

        private const string VisualName =
            "Visual";

        private const string GrabRegionName =
            "GrabRegion";

        private const string TrackingPointName =
            "TrackingPoint";

        private const string ImplementationName =
            "Implementation";

        [Header("Interactable")]

        [Tooltip(
            "BaseInteractable, которому принадлежит механизм. " +
            "Обычно находится на корне двери или предмета.")]
        [SerializeField]
        private BaseInteractable _interactable;

        [Tooltip(
            "Необязательный объект, удерживаемый второй рукой. " +
            "Для обычной дверной ручки оставь пустым.")]
        [SerializeField]
        private BaseInteractable _referenceInteractable;

        [Header("Rotation")]

        [Tooltip(
            "Ось вращения в локальной системе AxisSpace.")]
        [SerializeField]
        private Vector3 _localAxis =
            Vector3.up;

        [Tooltip(
            "Минимальный угол вращения.")]
        [SerializeField]
        private float _minimumAngle =
            0f;

        [Tooltip(
            "Максимальный угол вращения. " +
            "Для дверной ручки, например, -35.")]
        [SerializeField]
        private float _maximumAngle =
            -35f;

        [Tooltip(
            "Инвертирует управление рукой, " +
            "не меняя визуальный диапазон.")]
        [SerializeField]
        private bool _invertInput;

        [Header("Grab Region")]

        [Tooltip(
            "Допуск при определении, была ли рука " +
            "захвачена внутри GrabRegion.")]
        [SerializeField]
        [Min(0f)]
        private float _classificationTolerance =
            0.005f;

        [Tooltip(
            "При создании структуры добавить BoxCollider " +
            "на GrabRegion, если там ещё нет коллайдеров.")]
        [SerializeField]
        private bool _createDefaultBoxCollider =
            true;

        [Header("Tracking Stability")]

        [Tooltip(
            "При приближении руки к оси ближе этого расстояния " +
            "отслеживание временно приостанавливается.")]
        [SerializeField]
        [Min(0f)]
        private float _minimumTrackingRadius =
            0.01f;

        [Tooltip(
            "Расстояние от оси, после которого отслеживание " +
            "возобновляется.")]
        [SerializeField]
        [Min(0f)]
        private float _resumeTrackingRadius =
            0.015f;

        [Tooltip(
            "Максимальное изменение угла за один кадр. " +
            "Больший скачок считается ошибкой трекинга. " +
            "0 отключает фильтр.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float _maximumDeltaAnglePerFrame =
            120f;

        [Header("Editor")]

        [Tooltip(
            "Автоматически переносить изменения Provider " +
            "во внутренние компоненты.")]
        [SerializeField]
        private bool _autoApply =
            true;

        [Header("Generated Structure")]

        [SerializeField]
        [HideInInspector]
        private Transform _axisSpace;

        [SerializeField]
        [HideInInspector]
        private Transform _pivot;

        [SerializeField]
        [HideInInspector]
        private Transform _visual;

        [SerializeField]
        [HideInInspector]
        private Transform _grabRegion;

        [SerializeField]
        [HideInInspector]
        private Transform _trackingPoint;

        [SerializeField]
        [HideInInspector]
        private Transform _implementation;

        [SerializeField]
        [HideInInspector]
        private GrabRegionControlInput _controlInput;

        [SerializeField]
        [HideInInspector]
        private RotationInteraction _rotationInteraction;

        [SerializeField]
        [HideInInspector]
        private RotationDriver _rotationDriver;

#if UNITY_EDITOR
        private bool _applyQueued;
        private bool _creationQueued;
#endif

        public Transform AxisSpace =>
            _axisSpace;

        public Transform Pivot =>
            _pivot;

        public Transform Visual =>
            _visual;

        public Transform GrabRegion =>
            _grabRegion;

        public Transform TrackingPoint =>
            _trackingPoint;

        public GrabRegionControlInput ControlInput =>
            _controlInput;

        public RotationInteraction Interaction =>
            _rotationInteraction;

        public RotationDriver Driver =>
            _rotationDriver;

        public bool HasStructure =>
            _axisSpace != null &&
            _pivot != null &&
            _visual != null &&
            _grabRegion != null &&
            _trackingPoint != null &&
            _implementation != null &&
            _controlInput != null &&
            _rotationInteraction != null &&
            _rotationDriver != null;

        private void Reset()
        {
            if (_interactable == null)
            {
                _interactable =
                    GetComponentInParent<BaseInteractable>();
            }

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
                    Vector3.up;
            }

            _classificationTolerance =
                Mathf.Max(
                    0f,
                    _classificationTolerance);

            if (!IsFinite(_minimumAngle))
                _minimumAngle = 0f;

            if (!IsFinite(_maximumAngle))
                _maximumAngle = -35f;

            if (Mathf.Abs(
                    _maximumAngle -
                    _minimumAngle) <
                MinimumAngleRange)
            {
                _maximumAngle =
                    _minimumAngle + 1f;
            }

            if (!IsFinite(_minimumTrackingRadius))
                _minimumTrackingRadius = 0.01f;

            if (!IsFinite(_resumeTrackingRadius))
                _resumeTrackingRadius = 0.015f;

            _minimumTrackingRadius =
                Mathf.Max(
                    0f,
                    _minimumTrackingRadius);

            _resumeTrackingRadius =
                Mathf.Max(
                    _minimumTrackingRadius,
                    _resumeTrackingRadius);

            if (!IsFinite(
                    _maximumDeltaAnglePerFrame))
            {
                _maximumDeltaAnglePerFrame =
                    120f;
            }

            _maximumDeltaAnglePerFrame =
                Mathf.Clamp(
                    _maximumDeltaAnglePerFrame,
                    0f,
                    180f);
        }

#if UNITY_EDITOR

        [ContextMenu("Create Or Repair Structure")]
        public void CreateOrRepairStructure()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"{nameof(OneHandRotationProvider)} cannot create " +
                    "structure during Play Mode.",
                    this);

                return;
            }

            ValidateValues();

            int undoGroup =
                Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName(
                "Create One Hand Rotation Structure");

            Undo.RecordObject(
                this,
                "Update One Hand Rotation Provider");

            ResolveExistingStructure();

            _axisSpace =
                EnsureChild(
                    _axisSpace,
                    transform,
                    AxisSpaceName,
                    Vector3.zero);

            _pivot =
                EnsureChild(
                    _pivot,
                    _axisSpace,
                    PivotName,
                    Vector3.zero);

            _visual =
                EnsureChild(
                    _visual,
                    _pivot,
                    VisualName,
                    Vector3.zero);

            _grabRegion =
                EnsureChild(
                    _grabRegion,
                    _pivot,
                    GrabRegionName,
                    Vector3.zero);

            _trackingPoint =
                EnsureChild(
                    _trackingPoint,
                    _pivot,
                    TrackingPointName,
                    GetDefaultTrackingPointPosition());

            _implementation =
                EnsureChild(
                    _implementation,
                    transform,
                    ImplementationName,
                    Vector3.zero);

            EnsureDefaultCollider();

            _controlInput =
                EnsureComponent<GrabRegionControlInput>(
                    _implementation.gameObject);

            _rotationInteraction =
                EnsureComponent<RotationInteraction>(
                    _implementation.gameObject);

            _rotationDriver =
                EnsureComponent<RotationDriver>(
                    _implementation.gameObject);

            if (_interactable == null)
            {
                _interactable =
                    GetComponentInParent<BaseInteractable>();
            }

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
                    $"{nameof(OneHandRotationProvider)} cannot change " +
                    "generated components during Play Mode.",
                    this);

                return;
            }

            ValidateValues();
            ResolveExistingStructure();

            if (!HasStructure)
            {
                Debug.LogError(
                    $"{nameof(OneHandRotationProvider)} on '{name}' " +
                    "does not have a complete structure. " +
                    "Use Create Or Repair Structure first.",
                    this);

                return;
            }

            Undo.RecordObjects(
                new Object[]
                {
                    this,
                    _controlInput,
                    _rotationInteraction,
                    _rotationDriver
                },
                "Apply One Hand Rotation Configuration");

            ApplyConfigurationInternal();

            MarkSceneDirty();
        }

        [ContextMenu("Select Visual Folder")]
        public void SelectVisualFolder()
        {
            if (_visual != null)
                Selection.activeGameObject = _visual.gameObject;
        }

        [ContextMenu("Select Grab Region")]
        public void SelectGrabRegion()
        {
            if (_grabRegion != null)
                Selection.activeGameObject = _grabRegion.gameObject;
        }

        [ContextMenu("Select Pivot")]
        public void SelectPivot()
        {
            if (_pivot != null)
                Selection.activeGameObject = _pivot.gameObject;
        }

        private void ApplyConfigurationInternal()
        {
            if (!HasStructure)
                return;

            Collider[] regionColliders =
                _grabRegion.GetComponentsInChildren<Collider>(
                    true);

            ConfigureControlInput(
                regionColliders);

            ConfigureRotationInteraction();
            ConfigureRotationDriver();

            EditorUtility.SetDirty(
                _controlInput);

            EditorUtility.SetDirty(
                _rotationInteraction);

            EditorUtility.SetDirty(
                _rotationDriver);

            EditorUtility.SetDirty(
                this);

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    _controlInput);

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    _rotationInteraction);

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    _rotationDriver);

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    this);
        }

        private void ConfigureControlInput(
            Collider[] colliders)
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
                colliders);

            SetObjectReference(
                serialized,
                "_controlAnchor",
                _trackingPoint);

            SetFloat(
                serialized,
                "_classificationTolerance",
                _classificationTolerance);

            /*
             * Это Provider конкретно для управления одной рукой.
             * Вторая рука для активации механизма не требуется.
             */
            SetBool(
                serialized,
                "_requireAnotherManipulator",
                false);

            serialized.ApplyModifiedProperties();
        }

        private void ConfigureRotationInteraction()
        {
            SerializedObject serialized =
                new SerializedObject(
                    _rotationInteraction);

            serialized.Update();

            /*
             * Поля BasePoseValueInteraction.
             */
            SetObjectReference(
                serialized,
                "_controlInput",
                _controlInput);

            SetObjectReference(
                serialized,
                "_referenceInteractable",
                _referenceInteractable);

            /*
             * Поля RotationInteraction.
             */
            SetObjectReference(
                serialized,
                "_pivot",
                _pivot);

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

            SetFloatIfPresent(
                serialized,
                "_minimumTrackingRadius",
                _minimumTrackingRadius);

            SetFloatIfPresent(
                serialized,
                "_resumeTrackingRadius",
                _resumeTrackingRadius);

            SetFloatIfPresent(
                serialized,
                "_maximumDeltaAnglePerFrame",
                _maximumDeltaAnglePerFrame);

            serialized.ApplyModifiedProperties();
        }

        private void ConfigureRotationDriver()
        {
            SerializedObject serialized =
                new SerializedObject(
                    _rotationDriver);

            serialized.Update();

            SetObjectReference(
                serialized,
                "_source",
                _rotationInteraction);

            SetObjectReference(
                serialized,
                "_target",
                _pivot);

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

            serialized.ApplyModifiedProperties();
        }

        private void ResolveExistingStructure()
        {
            if (_axisSpace == null)
            {
                _axisSpace =
                    FindDirectChild(
                        transform,
                        AxisSpaceName);
            }

            if (_pivot == null &&
                _axisSpace != null)
            {
                _pivot =
                    FindDirectChild(
                        _axisSpace,
                        PivotName);
            }

            if (_visual == null &&
                _pivot != null)
            {
                _visual =
                    FindDirectChild(
                        _pivot,
                        VisualName);
            }

            if (_grabRegion == null &&
                _pivot != null)
            {
                _grabRegion =
                    FindDirectChild(
                        _pivot,
                        GrabRegionName);
            }

            if (_trackingPoint == null &&
                _pivot != null)
            {
                _trackingPoint =
                    FindDirectChild(
                        _pivot,
                        TrackingPointName);
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

            if (_rotationInteraction == null)
            {
                _rotationInteraction =
                    _implementation
                        .GetComponent<RotationInteraction>();
            }

            if (_rotationDriver == null)
            {
                _rotationDriver =
                    _implementation
                        .GetComponent<RotationDriver>();
            }
        }

        private Transform EnsureChild(
            Transform current,
            Transform expectedParent,
            string childName,
            Vector3 defaultLocalPosition)
        {
            if (current == null)
            {
                current =
                    FindDirectChild(
                        expectedParent,
                        childName);
            }

            if (current == null)
            {
                GameObject child =
                    new GameObject(childName);

                Undo.RegisterCreatedObjectUndo(
                    child,
                    $"Create {childName}");

                current =
                    child.transform;

                current.SetParent(
                    expectedParent,
                    false);

                current.localPosition =
                    defaultLocalPosition;

                current.localRotation =
                    Quaternion.identity;

                current.localScale =
                    Vector3.one;

                return current;
            }

            if (current.parent != expectedParent)
            {
                Undo.SetTransformParent(
                    current,
                    expectedParent,
                    $"Repair {childName} Parent");
            }

            return current;
        }

        private void EnsureDefaultCollider()
        {
            if (!_createDefaultBoxCollider ||
                _grabRegion == null)
            {
                return;
            }

            Collider[] existing =
                _grabRegion.GetComponentsInChildren<Collider>(
                    true);

            if (existing.Length > 0)
                return;

            BoxCollider box =
                Undo.AddComponent<BoxCollider>(
                    _grabRegion.gameObject);

            box.center =
                Vector3.zero;

            box.size =
                new Vector3(
                    0.15f,
                    0.05f,
                    0.05f);
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

        private Vector3 GetDefaultTrackingPointPosition()
        {
            Vector3 axis =
                _localAxis;

            if (axis.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                axis =
                    Vector3.up;
            }

            axis.Normalize();

            Vector3 perpendicular =
                Vector3.Cross(
                    axis,
                    Vector3.forward);

            if (perpendicular.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                perpendicular =
                    Vector3.Cross(
                        axis,
                        Vector3.up);
            }

            if (perpendicular.sqrMagnitude <
                MinimumAxisSqrMagnitude)
            {
                perpendicular =
                    Vector3.right;
            }

            return
                perpendicular.normalized *
                0.15f;
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

            if (this == null ||
                Application.isPlaying)
            {
                return;
            }

            CreateOrRepairStructure();
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

            if (this == null ||
                Application.isPlaying ||
                !_autoApply ||
                !HasStructure)
            {
                return;
            }

            ApplyConfiguration();
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

            if (property == null)
                return;

            property.objectReferenceValue =
                value;
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

            if (property == null)
                return;

            if (!property.isArray)
            {
                Debug.LogError(
                    $"{serialized.targetObject.GetType().Name}." +
                    $"{propertyName} is not an array.",
                    serialized.targetObject);

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

            if (property == null)
                return;

            property.floatValue =
                value;
        }

        private static void SetFloatIfPresent(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                serialized.FindProperty(
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

            if (property == null)
                return;

            property.boolValue =
                value;
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

            if (property == null)
                return;

            property.vector3Value =
                value;
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
                $"{nameof(OneHandRotationProvider)} could not find " +
                $"field '{propertyName}' on " +
                $"{serialized.targetObject.GetType().Name}. " +
                "The field may have been renamed.",
                serialized.targetObject);

            return null;
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

    [CustomEditor(typeof(OneHandRotationProvider))]
    public sealed class OneHandRotationProviderEditor
        : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            OneHandRotationProvider provider =
                (OneHandRotationProvider)target;

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
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           provider.Pivot == null))
                {
                    if (GUILayout.Button("Select Pivot"))
                        provider.SelectPivot();
                }

                using (new EditorGUI.DisabledScope(
                           provider.Visual == null))
                {
                    if (GUILayout.Button("Select Visual"))
                        provider.SelectVisualFolder();
                }

                using (new EditorGUI.DisabledScope(
                           provider.GrabRegion == null))
                {
                    if (GUILayout.Button("Select Grab Region"))
                        provider.SelectGrabRegion();
                }
            }

            EditorGUILayout.Space(10f);

            if (!provider.HasStructure)
            {
                EditorGUILayout.HelpBox(
                    "Добавь Provider и нажми Create / Repair Structure.",
                    MessageType.Info);

                return;
            }

            EditorGUILayout.HelpBox(
                "1. Перемести вращаемую модель внутрь Visual.\n" +
                "2. Настрой коллайдеры внутри GrabRegion.\n" +
                "3. Поставь Pivot точно на ось вращения.\n" +
                "4. Поверни AxisSpace так, чтобы Local Axis совпала " +
                "с физической осью.\n" +
                "5. Поставь TrackingPoint возле места захвата.",
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
                    typeof(RotationInteraction),
                    true);

                EditorGUILayout.ObjectField(
                    "Driver",
                    provider.Driver,
                    typeof(RotationDriver),
                    true);
            }
        }
    }

#endif
}