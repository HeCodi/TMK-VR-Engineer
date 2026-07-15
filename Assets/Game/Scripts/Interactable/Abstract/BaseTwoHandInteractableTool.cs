using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Assets.Game.Scripts.Interactable.Abstract
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public abstract class BaseTwoHandInteractableTool : BaseInteractable
    {
        [SerializeField] protected float DeadZone = 0.02f;

        private int _handCountLast = 0;

        protected bool IsTwoHandGraped()
        {
            return Interactors.Count >= 2;
        }

        protected override void WasDetach(IXRInteractor interactor)
        {
            if (_handCountLast >= 2 && !IsTwoHandGraped())
                WasDetachTwoHand();

            _handCountLast = Interactors.Count;
        }

        protected override void WasSelect(IXRInteractor interactor)
        {
            if (IsTwoHandGraped() && _handCountLast < 2)
                WasSelectTwoHand();

            _handCountLast = Interactors.Count;
        }

        protected abstract void WasSelectTwoHand();
        protected abstract void WasDetachTwoHand();
    }
}