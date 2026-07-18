using System;

namespace Assets.Game.Scripts.Interactable.Mechanics
{
    /// <summary>
    /// Универсальный источник логического состояния.
    /// Неважно, откуда состояние получено:
    /// порог, кнопка, ключ, код, переключатель и т.д.
    /// </summary>
    public interface IBooleanStateSource
    {
        bool IsActive { get; }

        event Action<bool> StateChanged;
    }
}