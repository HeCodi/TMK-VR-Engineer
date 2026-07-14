using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Select
{
    public struct AttachPointData
    {
        public Transform AttachTransform;
        public IXRInteractor Interactor;

        public AttachPointData(Transform attachTransform, IXRInteractor interactor)
        {
            AttachTransform = attachTransform;
            Interactor = interactor;
        }
    }
}