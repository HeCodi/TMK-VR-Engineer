using System;
using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Inputs
{
    /// <summary>
    /// Универсальный источник управляющей Pose.
    ///
    /// Источником может быть:
    /// Select, Poke, физический контакт или их комбинация.
    /// </summary>
    public abstract class BaseControlInput : MonoBehaviour
    {
        private bool _isActive;

        public bool IsActive => _isActive;

        /// <summary>
        /// Объект, который сейчас управляет механизмом.
        /// Например IXRSelectInteractor или IXRHoverInteractor.
        /// Используется для распознавания смены источника.
        /// </summary>
        public abstract object ActiveSource { get; }

        public event Action Started;
        public event Action Ended;
        public event Action SourceChanged;

        public abstract bool TryGetControlPose(
            out Pose pose);

        protected virtual void OnDisable()
        {
            SetInputActive(false);
        }

        protected void SetInputActive(bool active)
        {
            if (_isActive == active)
                return;

            _isActive = active;

            if (_isActive)
                Started?.Invoke();
            else
                Ended?.Invoke();
        }

        protected void NotifySourceChanged()
        {
            SourceChanged?.Invoke();
        }
    }
}