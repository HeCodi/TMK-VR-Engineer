using Assets.Game.Scripts.Select;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Abstract
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public abstract class BaseGrabHandler : MonoBehaviour, IGrabHandler
    {
        private List<IXRInteractor> _interactors = new List<IXRInteractor>();
        private IXRInteractable _thisInteractable;

        protected abstract void WasSelect(IXRInteractor interactor);
        protected abstract void WasDetach(IXRInteractor interactor);

        protected IReadOnlyList<IXRInteractor> Interactors => _interactors;
        protected IXRInteractable ThisInteractable => _thisInteractable ??= GetComponent<IXRInteractable>();

        public void OnSelect(IXRInteractor interactor)
        {
            _interactors.Add(interactor);

            WasSelect(interactor);
        }

        public void OnDetach(IXRInteractor interactor)
        {
            _interactors.Remove(interactor);

            WasDetach(interactor);
        }

        protected List<AttachPointData> GetAttachTransforms()
        {
            List<AttachPointData> attachTransforms = new List<AttachPointData>();

            foreach (var interactor in _interactors)
                attachTransforms.Add(new AttachPointData(interactor.GetAttachTransform(ThisInteractable), interactor));
            
            return attachTransforms;
        }

        protected List<AttachTargetPointData> GetDistancesAttachToTargetPoint(Transform targetPoint)
        {
            List<AttachPointData> attachPoints = GetAttachTransforms();

            List<AttachTargetPointData> Distances = new List<AttachTargetPointData>();

            foreach (var attachPoint in attachPoints)
            {
                float distance = Vector3.Distance(attachPoint.AttachTransform.position, targetPoint.position);

                AttachTargetPointData attachTarget = new(distance, attachPoint);

                Distances.Add(attachTarget);
            }

            return Distances;
        }
    }
}