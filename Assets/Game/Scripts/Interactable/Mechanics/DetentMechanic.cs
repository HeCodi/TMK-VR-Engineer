using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Game.Scripts.Interactable.Mechanics
{
    [Serializable]
    public sealed class DetentIndexUnityEvent
        : UnityEvent<int>
    {
    }

    /// <summary>
    /// Пока пользователь управляет Source,
    /// значение свободно следует за ним.
    ///
    /// После отпускания значение защёлкивается
    /// в ближайшее разрешённое положение.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(240)]
    public sealed class DetentMechanic
        : BaseValueMechanic
    {
        private const float MinimumSnapSpeed = 0.0001f;
        private const float SnapEpsilon = 0.0001f;

        [Header("Detents")]

        [Tooltip(
            "Разрешённые положения в диапазоне 0..1.")]
        [SerializeField]
        private float[] _detents =
        {
            0f,
            1f
        };

        [SerializeField]
        [Min(MinimumSnapSpeed)]
        private float _snapSpeed = 6f;

        [Tooltip(
            "Защёлкнуть механизм при включении сцены.")]
        [SerializeField]
        private bool _snapOnEnable = true;

        [Header("Detent Events")]

        [SerializeField]
        private UnityEvent _onSnapStarted =
            new UnityEvent();

        [SerializeField]
        private UnityEvent _onSnapCompleted =
            new UnityEvent();

        [SerializeField]
        private DetentIndexUnityEvent _onDetentChanged =
            new DetentIndexUnityEvent();

        private bool _isSnapping;

        private int _targetDetentIndex = -1;
        private int _currentDetentIndex = -1;

        public event Action SnapStarted;
        public event Action SnapCompleted;
        public event Action<int> DetentChanged;

        public bool IsSnapping => _isSnapping;

        public int CurrentDetentIndex =>
            _currentDetentIndex;

        public int TargetDetentIndex =>
            _targetDetentIndex;

        public int DetentCount =>
            _detents != null
                ? _detents.Length
                : 0;

        protected override void OnEnable()
        {
            ValidateDetents();

            _isSnapping = false;
            _targetDetentIndex = -1;
            _currentDetentIndex = -1;

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _isSnapping = false;
            _targetDetentIndex = -1;

            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            _snapSpeed =
                Mathf.Max(
                    MinimumSnapSpeed,
                    _snapSpeed);

            ValidateDetents();
        }

        protected override void OnSourceSynchronized()
        {
            _currentDetentIndex =
                FindExactDetentIndex(Value);

            if (Source == null ||
                Source.IsInteracting)
            {
                return;
            }

            if (_snapOnEnable)
                SnapToNearest();
        }

        protected override void OnSourceValueChanged(float value)
        {
            if (Source != null &&
                Source.IsInteracting)
            {
                CancelSnap();

                TrySetValueFromInteraction(value);
                return;
            }

            TrySetValueFromInteraction(value);

            SnapToNearest();
        }

        protected override void OnSourceInteractionStarted()
        {
            CancelSnap();

            /*
             * Source должен начать новую манипуляцию
             * из фактического положения после предыдущего snap.
             */
            WriteValueBackToSource(Value);

            SetInteractionActive(true);
        }

        protected override void OnSourceInteractionEnded()
        {
            SetInteractionActive(false);

            SnapToNearest();
        }

        private void Update()
        {
            if (!_isSnapping)
                return;

            if (!IsValidDetentIndex(
                    _targetDetentIndex))
            {
                CancelSnap();
                return;
            }

            float targetValue =
                _detents[_targetDetentIndex];

            float nextValue =
                Mathf.MoveTowards(
                    Value,
                    targetValue,
                    _snapSpeed * Time.deltaTime);

            TrySetValueFromInteraction(nextValue);

            if (Mathf.Abs(
                    Value - targetValue) >
                SnapEpsilon)
            {
                return;
            }

            CompleteSnap();
        }

        public void SnapToNearest()
        {
            int nearestIndex =
                FindNearestDetentIndex(Value);

            BeginSnapToIndex(nearestIndex);
        }

        public bool SnapToIndex(int index)
        {
            return BeginSnapToIndex(index);
        }

        public bool SetDetentImmediately(int index)
        {
            if (!IsValidDetentIndex(index))
                return false;

            if (Source != null &&
                Source.IsInteracting)
            {
                return false;
            }

            _isSnapping = false;
            _targetDetentIndex = index;

            CompleteSnap();

            return true;
        }

        public float GetDetentValue(int index)
        {
            if (!IsValidDetentIndex(index))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }

            return _detents[index];
        }

        private bool BeginSnapToIndex(int index)
        {
            if (!IsValidDetentIndex(index))
                return false;

            if (Source != null &&
                Source.IsInteracting)
            {
                return false;
            }

            _targetDetentIndex = index;

            float targetValue =
                _detents[_targetDetentIndex];

            if (Mathf.Abs(
                    Value - targetValue) <=
                SnapEpsilon)
            {
                CompleteSnap();
                return true;
            }

            if (_isSnapping)
                return true;

            _isSnapping = true;

            SnapStarted?.Invoke();
            _onSnapStarted?.Invoke();

            return true;
        }

        private void CancelSnap()
        {
            _isSnapping = false;
            _targetDetentIndex = -1;
        }

        private void CompleteSnap()
        {
            if (!IsValidDetentIndex(
                    _targetDetentIndex))
            {
                CancelSnap();
                return;
            }

            int completedIndex =
                _targetDetentIndex;

            float completedValue =
                _detents[completedIndex];

            TrySetValueFromInteraction(
                completedValue);

            WriteValueBackToSource(
                completedValue);

            _isSnapping = false;
            _targetDetentIndex = -1;

            bool indexChanged =
                _currentDetentIndex != completedIndex;

            _currentDetentIndex =
                completedIndex;

            SnapCompleted?.Invoke();
            _onSnapCompleted?.Invoke();

            if (!indexChanged)
                return;

            DetentChanged?.Invoke(
                _currentDetentIndex);

            _onDetentChanged?.Invoke(
                _currentDetentIndex);
        }

        private int FindNearestDetentIndex(float value)
        {
            if (_detents == null ||
                _detents.Length == 0)
            {
                return -1;
            }

            int nearestIndex = 0;

            float nearestDistance =
                Mathf.Abs(
                    value - _detents[0]);

            for (int i = 1;
                 i < _detents.Length;
                 i++)
            {
                float distance =
                    Mathf.Abs(
                        value - _detents[i]);

                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestIndex = i;
            }

            return nearestIndex;
        }

        private int FindExactDetentIndex(float value)
        {
            if (_detents == null)
                return -1;

            for (int i = 0;
                 i < _detents.Length;
                 i++)
            {
                if (Mathf.Abs(
                        value - _detents[i]) <=
                    SnapEpsilon)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsValidDetentIndex(int index)
        {
            return _detents != null &&
                   index >= 0 &&
                   index < _detents.Length;
        }

        private void ValidateDetents()
        {
            if (_detents == null ||
                _detents.Length == 0)
            {
                _detents = new[]
                {
                    0f,
                    1f
                };
            }

            for (int i = 0;
                 i < _detents.Length;
                 i++)
            {
                _detents[i] =
                    Mathf.Clamp01(
                        _detents[i]);
            }

            Array.Sort(_detents);
        }
    }
}