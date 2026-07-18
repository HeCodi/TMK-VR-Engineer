using System.Collections.Generic;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Inputs
{
    /// <summary>
    /// Объединяет несколько BaseControlInput в один источник.
    ///
    /// Первый активный элемент массива получает управление.
    /// Активный источник удерживает управление до своего завершения.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class CompositeControlInput : BaseControlInput
    {
        [Header("Input Priority")]

        [Tooltip(
            "Источники в порядке приоритета. " +
            "Например Poke первым, Select вторым.")]
        [SerializeField]
        private BaseControlInput[] _inputs;

        private BaseControlInput _activeInput;
        private object _activeSource;

        public override object ActiveSource =>
            _activeSource;

        public BaseControlInput ActiveInput =>
            _activeInput;

        private void OnEnable()
        {
            EvaluateActiveInput();
        }

        private void Update()
        {
            EvaluateActiveInput();
        }

        protected override void OnDisable()
        {
            _activeInput = null;
            _activeSource = null;

            NotifySourceChanged();

            base.OnDisable();
        }

        private void Reset()
        {
            BaseControlInput[] found =
                GetComponents<BaseControlInput>();

            List<BaseControlInput> filtered =
                new List<BaseControlInput>();

            for (int i = 0; i < found.Length; i++)
            {
                BaseControlInput input = found[i];

                if (input == null || input == this)
                    continue;

                filtered.Add(input);
            }

            _inputs = filtered.ToArray();
        }

        public override bool TryGetControlPose(
            out Pose pose)
        {
            pose = default;

            EvaluateActiveInput();

            if (!IsInputUsable(_activeInput))
                return false;

            return _activeInput.TryGetControlPose(
                out pose);
        }

        private void EvaluateActiveInput()
        {
            BaseControlInput nextInput =
                FindNextInput();

            object nextSource =
                nextInput != null
                    ? nextInput.ActiveSource
                    : null;

            bool inputChanged =
                !ReferenceEquals(
                    _activeInput,
                    nextInput);

            bool sourceChanged =
                !ReferenceEquals(
                    _activeSource,
                    nextSource);

            if (!inputChanged && !sourceChanged)
            {
                SetInputActive(nextInput != null);
                return;
            }

            _activeInput = nextInput;
            _activeSource = nextSource;

            NotifySourceChanged();
            SetInputActive(_activeInput != null);
        }

        private BaseControlInput FindNextInput()
        {
            // Уже активный источник не перехватываем,
            // пока он сам не закончит взаимодействие.
            if (IsInputUsable(_activeInput))
                return _activeInput;

            if (_inputs == null)
                return null;

            for (int i = 0; i < _inputs.Length; i++)
            {
                BaseControlInput candidate =
                    _inputs[i];

                if (!IsInputUsable(candidate))
                    continue;

                return candidate;
            }

            return null;
        }

        private bool IsInputUsable(
            BaseControlInput input)
        {
            if (input == null)
                return false;

            if (input == this)
                return false;

            if (!input.isActiveAndEnabled)
                return false;

            return input.IsActive;
        }
    }
}