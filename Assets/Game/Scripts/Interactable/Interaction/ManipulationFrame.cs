using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Interactions
{
    public readonly struct ManipulationFrame
    {
        public Pose ControlPose { get; }

        public Pose ReferencePose { get; }

        public bool HasReference { get; }

        public ManipulationFrame(
            Pose controlPose,
            Pose referencePose,
            bool hasReference)
        {
            ControlPose = controlPose;
            ReferencePose = referencePose;
            HasReference = hasReference;
        }

        public static ManipulationFrame WithoutReference(
            Pose controlPose)
        {
            return new ManipulationFrame(
                controlPose,
                default,
                false);
        }

        public static ManipulationFrame WithReference(
            Pose controlPose,
            Pose referencePose)
        {
            return new ManipulationFrame(
                controlPose,
                referencePose,
                true);
        }
    }
}