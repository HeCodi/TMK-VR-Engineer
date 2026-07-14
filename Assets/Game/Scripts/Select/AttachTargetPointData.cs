using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Select
{
    public struct AttachTargetPointData
    {
        public float DistanceToTarget;
        public AttachPointData attachPoint;

        public AttachTargetPointData(float distanceToTarget, AttachPointData attachPoint)
        {
            DistanceToTarget = distanceToTarget;
            this.attachPoint = attachPoint;
        }
    }
}