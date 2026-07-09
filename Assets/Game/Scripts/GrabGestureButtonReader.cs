using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace UnityEngine.XR.Interaction.Toolkit.Samples.Hands
{
    public class GrabGestureButtonReader : MonoBehaviour, IXRInputButtonReader
    {
        public enum SelectMethod
        {
            Pinch,
            Fist
        }

        public event Action<SelectMethod> OnGrabed;

        private XRHandSubsystem _handSubsystem;

        [Range(0, 1)]
        private float _grabValue;

        private bool _performedFist;
        private bool _performed;
        private bool _previousPerformed;

        private SelectMethod _selectMethod = SelectMethod.Pinch;

        [Header("Gesture Settings")]
        [SerializeField]
        private float _pinchDistance = 0.035f;

        [SerializeField]
        private float _fistCurlThreshold = 0.65f;


        void Start()
        {
            List<XRHandSubsystem> subsystems = new();

            SubsystemManager.GetSubsystems(subsystems);

            if (subsystems.Count > 0)
                _handSubsystem = subsystems[0];
            else
                Debug.LogError("Subsystem not found!");
        }


        public void Update()
        {
            _previousPerformed = _performed;

            _grabValue = CalculateGrab();

            _performed = _grabValue > 0.7f;
            Debug.Log(_performed);

            if (_performed)
                OnGrabed?.Invoke(_selectMethod);

        }

        float CalculateGrab()
        {
            if (CheckPinch())
            {
                _selectMethod = SelectMethod.Pinch;
                return 1f;
            }
                

            if (_performedFist)
            {
                _selectMethod = SelectMethod.Fist;
                return 1f;
            }

            return 0f;
        }

        bool CheckPinch()
        {
            if (_handSubsystem == null)
                return false;


            XRHand hand = _handSubsystem.rightHand;


            if (!hand.isTracked)
                return false;


            var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
            var index = hand.GetJoint(XRHandJointID.IndexTip);


            if (!thumb.TryGetPose(out Pose thumbPose))
                return false;


            if (!index.TryGetPose(out Pose indexPose))
                return false;


            float distance = Vector3.Distance(
                thumbPose.position,
                indexPose.position
            );


            return distance < _pinchDistance;
        }

        public void OnFistPerformed()
        {
            _performedFist = true;
        }

        public void OnFistEnded()
        {
            _performedFist = false;
        }

        public bool ReadIsPerformed()
        {
            return _performed;
        }


        public bool ReadWasPerformedThisFrame()
        {
            return _performed && !_previousPerformed;
        }


        public bool ReadWasCompletedThisFrame()
        {
            return !_performed && _previousPerformed;
        }


        public float ReadValue()
        {
            return _grabValue;
        }


        public bool TryReadValue(out float value)
        {
            value = _grabValue;
            return true;
        }
    }
}