using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Assets.Game.Scripts.Interactable
{
    public class TwoHandGrabTransformer : XRBaseGrabTransformer
    {
        private bool _initialized;
        private Vector3 _localGrabOffset;

        [SerializeField] private Vector3 offset;

        public override bool canProcess => true;

        public override void Process(
            XRGrabInteractable grabInteractable,
            XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose,
            ref Vector3 localScale)
        {

            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                return;

            var interactors = grabInteractable.interactorsSelecting;

            if (interactors.Count == 0)
            {
                _initialized = false;
                return;
            }

            var firstInteractor = interactors[0];
            Transform firstAttach = firstInteractor.GetAttachTransform(grabInteractable);

            if (interactors.Count < 2)
            {
                _initialized = false;
                return;
            }

            var secondInteractor = interactors[1];
            Transform secondAttach = secondInteractor.GetAttachTransform(grabInteractable);

            Vector3 dir = secondAttach.position - firstAttach.position;

            if (dir.sqrMagnitude < 0.001f)
                return;

            Quaternion rotation = Quaternion.LookRotation(
                dir.normalized,
                grabInteractable.transform.up) * Quaternion.Euler(offset.x, offset.y, offset.z);

            // Один раз запоминаем смещение Pivot относительно первой руки
            if (!_initialized)
            {
                _localGrabOffset =
                    Quaternion.Inverse(rotation) *
                    (grabInteractable.transform.position - firstAttach.position);

                _initialized = true;
            }

            targetPose.rotation = rotation;

            targetPose.position = firstAttach.position + rotation * _localGrabOffset;
        }
    }
}