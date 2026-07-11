using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Assets.Game.Scripts.Interactable.Abstract
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(TwoHandGrabTransformer))]
    public abstract class BaseTwoHandInteractableTool : BaseInteractableTool
    {
        private int _handCountLast = 0;

        protected bool IsTwoHandGraped()
        {
            return Interactors.Count >= 2;
        }

        protected override void WasDetach()
        {
            if (_handCountLast >= 2 && !IsTwoHandGraped())
                WasDetachTwoHand();

            _handCountLast = Interactors.Count;
        }

        protected override void WasSelect()
        {
            if (IsTwoHandGraped() && _handCountLast < 2)
                WasSelectTwoHand();

            _handCountLast = Interactors.Count;
        }

        protected abstract void WasSelectTwoHand();
        protected abstract void WasDetachTwoHand();
    }
}