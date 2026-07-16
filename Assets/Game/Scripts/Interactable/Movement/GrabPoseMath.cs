using UnityEngine;

namespace Assets.Game.Scripts.Interactable.Movement
{
    /// <summary>
    /// Чистая математика поз.
    ///
    /// Здесь нет:
    /// - MonoBehaviour;
    /// - событий;
    /// - Rigidbody;
    /// - зависимости от XR Interactor.
    /// </summary>
    public static class GrabPoseMath
    {
        private const float MinimumVectorSqrMagnitude =
            0.000001f;

        public static Pose GetPose(Transform transform)
        {
            return new Pose(
                transform.position,
                transform.rotation);
        }

        /// <summary>
        /// Сохраняет world pose относительно reference frame.
        /// </summary>
        public static void CaptureRelativePose(
            Pose referenceFrame,
            Pose worldPose,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            Quaternion inverseReferenceRotation =
                Quaternion.Inverse(referenceFrame.rotation);

            localPosition =
                inverseReferenceRotation *
                (worldPose.position - referenceFrame.position);

            localRotation =
                inverseReferenceRotation *
                worldPose.rotation;
        }

        /// <summary>
        /// Восстанавливает world pose из локального offset.
        /// </summary>
        public static Pose ApplyRelativePose(
            Pose referenceFrame,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            Vector3 worldPosition =
                referenceFrame.position +
                referenceFrame.rotation * localPosition;

            Quaternion worldRotation =
                referenceFrame.rotation *
                localRotation;

            return new Pose(
                worldPosition,
                worldRotation);
        }

        /// <summary>
        /// Создаёт общий frame двух рук.
        ///
        /// Position:
        /// середина между руками.
        ///
        /// Forward:
        /// направление от первой руки ко второй.
        ///
        /// Up:
        /// усреднённое направление up двух рук.
        /// </summary>
        public static bool TryCreateTwoHandFrame(
            Transform firstAttach,
            Transform secondAttach,
            Quaternion fallbackRotation,
            out Pose frame)
        {
            if (firstAttach == null ||
                secondAttach == null)
            {
                frame = default;
                return false;
            }

            Vector3 handDirection =
                secondAttach.position -
                firstAttach.position;

            if (handDirection.sqrMagnitude <
                MinimumVectorSqrMagnitude)
            {
                frame = default;
                return false;
            }

            Vector3 forward =
                handDirection.normalized;

            Vector3 up =
                firstAttach.up +
                secondAttach.up;

            up = Vector3.ProjectOnPlane(
                up,
                forward);

            if (up.sqrMagnitude <
                MinimumVectorSqrMagnitude)
            {
                up = Vector3.ProjectOnPlane(
                    fallbackRotation * Vector3.up,
                    forward);
            }

            if (up.sqrMagnitude <
                MinimumVectorSqrMagnitude)
            {
                up = Vector3.ProjectOnPlane(
                    Vector3.up,
                    forward);
            }

            if (up.sqrMagnitude <
                MinimumVectorSqrMagnitude)
            {
                up = Vector3.ProjectOnPlane(
                    Vector3.right,
                    forward);
            }

            up.Normalize();

            Vector3 center =
                (firstAttach.position +
                 secondAttach.position) * 0.5f;

            Quaternion rotation =
                Quaternion.LookRotation(
                    forward,
                    up);

            frame = new Pose(
                center,
                rotation);

            return true;
        }

        /// <summary>
        /// Вычисляет позу корневого объекта так,
        /// чтобы его object attach совпал с target attach.
        ///
        /// Используется для сокетов.
        /// </summary>
        public static Pose AlignRootToAttach(
            Pose currentRootPose,
            Pose currentObjectAttachPose,
            Pose targetAttachPose)
        {
            CaptureRelativePose(
                currentRootPose,
                currentObjectAttachPose,
                out Vector3 localAttachPosition,
                out Quaternion localAttachRotation);

            Quaternion targetRootRotation =
                targetAttachPose.rotation *
                Quaternion.Inverse(localAttachRotation);

            Vector3 targetRootPosition =
                targetAttachPose.position -
                targetRootRotation * localAttachPosition;

            return new Pose(
                targetRootPosition,
                targetRootRotation);
        }
    }
}