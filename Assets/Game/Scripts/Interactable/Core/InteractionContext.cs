using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Core
{
    /// <summary>
    /// Предоставляет интеракциям текущее состояние XRGrabInteractable.
    ///
    /// Класс:
    /// - ничего не перемещает;
    /// - не подписывается на события;
    /// - не хранит копию списка интеракторов;
    /// - всегда читает актуальное состояние непосредственно из XRI.
    /// </summary>
    public sealed class InteractionContext
    {
        private readonly Rigidbody _rigidbody;

        public InteractionContext(XRGrabInteractable grabInteractable)
        {
            GrabInteractable = grabInteractable;
            _rigidbody = grabInteractable.GetComponent<Rigidbody>();
        }

        public XRGrabInteractable GrabInteractable { get; }

        public Transform Root => GrabInteractable.transform;

        public Rigidbody Rigidbody => _rigidbody;

        /// <summary>
        /// Общее количество селекторов.
        ///
        /// Сюда могут входить:
        /// - руки;
        /// - NearFarInteractor;
        /// - RayInteractor;
        /// - SocketInteractor.
        /// </summary>
        public int SelectionCount =>
            GrabInteractable.interactorsSelecting.Count;

        /// <summary>
        /// Количество манипуляторов без учёта сокетов.
        /// </summary>
        public int ManipulatorCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < SelectionCount; i++)
                {
                    IXRSelectInteractor interactor =
                        GetSelectingInteractor(i);

                    if (!IsSocket(interactor))
                        count++;
                }

                return count;
            }
        }

        public bool IsSelected =>
            SelectionCount > 0;

        public bool IsManipulated =>
            ManipulatorCount > 0;

        public IXRSelectInteractor GetSelectingInteractor(int index)
        {
            if (index < 0 || index >= SelectionCount)
                return null;

            return GrabInteractable.interactorsSelecting[index];
        }

        /// <summary>
        /// Возвращает манипулятор по индексу,
        /// пропуская XRSocketInteractor.
        /// </summary>
        public IXRSelectInteractor GetManipulator(int manipulatorIndex)
        {
            if (manipulatorIndex < 0)
                return null;

            int currentManipulatorIndex = 0;

            for (int i = 0; i < SelectionCount; i++)
            {
                IXRSelectInteractor interactor =
                    GetSelectingInteractor(i);

                if (IsSocket(interactor))
                    continue;

                if (currentManipulatorIndex == manipulatorIndex)
                    return interactor;

                currentManipulatorIndex++;
            }

            return null;
        }

        public XRSocketInteractor GetFirstSocket()
        {
            for (int i = 0; i < SelectionCount; i++)
            {
                if (GetSelectingInteractor(i) is XRSocketInteractor socket)
                    return socket;
            }

            return null;
        }

        public bool Contains(IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            for (int i = 0; i < SelectionCount; i++)
            {
                if (ReferenceEquals(
                        GetSelectingInteractor(i),
                        interactor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attach Transform на стороне руки/интерактора.
        /// </summary>
        public Transform GetInteractorAttach(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return null;

            return interactor.GetAttachTransform(
                GrabInteractable);
        }

        /// <summary>
        /// Attach Transform на стороне объекта.
        /// При Dynamic Attach XRI может создать отдельный
        /// attach transform для каждого интерактора.
        /// </summary>
        public Transform GetInteractableAttach(
            IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return null;

            return GrabInteractable.GetAttachTransform(
                interactor);
        }

        /// <summary>
        /// Статическая точка объекта, используемая для сокета.
        /// </summary>
        public Transform GetStaticInteractableAttach()
        {
            return GrabInteractable.attachTransform != null
                ? GrabInteractable.attachTransform
                : Root;
        }

        public static bool IsSocket(
            IXRSelectInteractor interactor)
        {
            return interactor is XRSocketInteractor;
        }
    }
}