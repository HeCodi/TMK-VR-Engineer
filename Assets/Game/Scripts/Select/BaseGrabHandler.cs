using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Interactable.Abstract
{
    public abstract class BaseGrabHandler : MonoBehaviour
    {
        private List<IXRInteractor> _interactors = new List<IXRInteractor>();
        private IXRInteractable _thisInteractable;

        protected abstract void WasSelect();
        protected abstract void WasDetach();

        protected IReadOnlyList<IXRInteractor> Interactors => _interactors;
        protected IXRInteractable ThisInteractable => _thisInteractable ??= GetComponent<IXRInteractable>();

        public void OnSelect(IXRInteractor interactor)
        {
            _interactors.Add(interactor);

            WasSelect();
        }

        public void OnDetach(IXRInteractor interactor)
        {
            _interactors.Remove(interactor);

            WasDetach();
        }

        protected List<AttachPoint> GetAttachTransforms()
        {
            List<AttachPoint> attachTransforms = new List<AttachPoint>();

            foreach (var interactor in _interactors)
                attachTransforms.Add(new AttachPoint(interactor.GetAttachTransform(ThisInteractable), interactor));
            
            return attachTransforms;
        }

        protected List<AttachTargetPoint> GetDistancesAttachToTargetPoint(Transform targetPoint)
        {
            List<AttachPoint> attachPoints = GetAttachTransforms();

            List<AttachTargetPoint> Distances = new List<AttachTargetPoint>();

            foreach (var attachPoint in attachPoints)
            {
                float distance = Vector3.Distance(attachPoint.AttachTransform.position, targetPoint.position);

                AttachTargetPoint attachTarget = new(distance, attachPoint);

                Distances.Add(attachTarget);
            }

            return Distances;
        }

        public struct AttachTargetPoint 
        {
            public float DistanceToTarget;
            public AttachPoint attachPoint;

            public AttachTargetPoint(float distanceToTarget, AttachPoint attachPoint)
            {
                DistanceToTarget = distanceToTarget;
                this.attachPoint = attachPoint;
            }
        }
        public struct AttachPoint
        {
            public Transform AttachTransform;
            public IXRInteractor Interactor;

            public AttachPoint(Transform attachTransform, IXRInteractor interactor)
            {
                AttachTransform = attachTransform;
                Interactor = interactor;
            }
        }
    }
}