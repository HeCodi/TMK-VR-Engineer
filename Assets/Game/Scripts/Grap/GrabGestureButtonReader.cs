using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace UnityEngine.XR.Interaction.Toolkit.Samples.Hands
{
    public class GrabGestureButtonReader :
        MonoBehaviour,
        IXRInputButtonReader
    {
        public enum SelectMethod
        {
            Pinch,
            Fist
        }

        public enum HandSide
        {
            Left,
            Right
        }

        public event Action<SelectMethod>
            OnGrabedWithSelectMethod;

        [Header("Hand")]

        [SerializeField]
        private HandSide _handSide =
            HandSide.Right;

        [Header("Gesture Settings")]

        [SerializeField]
        [Range(0.005f, 0.1f)]
        private float _pinchDistance =
            0.035f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _performedThreshold =
            0.7f;

        [Header("Events")]

        [SerializeField]
        private UnityEvent _onGrabed =
            new UnityEvent();

        [SerializeField]
        private UnityEvent _onStopGrabed =
            new UnityEvent();

        private XRHandSubsystem _handSubsystem;

        private float _grabValue;

        private bool _performedFist;
        private bool _performed;
        private bool _previousPerformed;

        private SelectMethod _selectMethod =
            SelectMethod.Pinch;

        public HandSide Side =>
            _handSide;

        private void Start()
        {
            ResolveHandSubsystem();

            if (_handSubsystem == null)
            {
                Debug.LogError(
                    $"{nameof(GrabGestureButtonReader)} on '{name}' " +
                    "could not find an XRHandSubsystem.",
                    this);
            }
        }

        private void Update()
        {
            if (_handSubsystem == null)
            {
                ResolveHandSubsystem();

                if (_handSubsystem == null)
                {
                    ResetInputState();
                    return;
                }
            }

            _previousPerformed =
                _performed;

            _grabValue =
                CalculateGrab();

            _performed =
                _grabValue >
                _performedThreshold;

            if (ReadWasPerformedThisFrame())
            {
                _onGrabed?.Invoke();

                OnGrabedWithSelectMethod?.Invoke(
                    _selectMethod);
            }
            else if (ReadWasCompletedThisFrame())
            {
                _onStopGrabed?.Invoke();
            }
        }

        private float CalculateGrab()
        {
            if (CheckPinch())
            {
                _selectMethod =
                    SelectMethod.Pinch;

                return 1f;
            }

            if (_performedFist)
            {
                _selectMethod =
                    SelectMethod.Fist;

                return 1f;
            }

            return 0f;
        }

        private bool CheckPinch()
        {
            if (_handSubsystem == null)
                return false;

            XRHand hand =
                _handSide == HandSide.Left
                    ? _handSubsystem.leftHand
                    : _handSubsystem.rightHand;

            if (!hand.isTracked)
                return false;

            XRHandJoint thumb =
                hand.GetJoint(
                    XRHandJointID.ThumbTip);

            XRHandJoint index =
                hand.GetJoint(
                    XRHandJointID.IndexTip);

            if (!thumb.TryGetPose(
                    out Pose thumbPose))
            {
                return false;
            }

            if (!index.TryGetPose(
                    out Pose indexPose))
            {
                return false;
            }

            float distance =
                Vector3.Distance(
                    thumbPose.position,
                    indexPose.position);

            return distance <
                   _pinchDistance;
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
            return _performed &&
                   !_previousPerformed;
        }

        public bool ReadWasCompletedThisFrame()
        {
            return !_performed &&
                   _previousPerformed;
        }

        public float ReadValue()
        {
            return _grabValue;
        }

        public bool TryReadValue(
            out float value)
        {
            value = _grabValue;
            return true;
        }

        private void ResolveHandSubsystem()
        {
            List<XRHandSubsystem> subsystems =
                new List<XRHandSubsystem>();

            SubsystemManager.GetSubsystems(
                subsystems);

            _handSubsystem = null;

            for (int i = 0;
                 i < subsystems.Count;
                 i++)
            {
                XRHandSubsystem subsystem =
                    subsystems[i];

                if (subsystem == null)
                    continue;

                if (subsystem.running)
                {
                    _handSubsystem =
                        subsystem;

                    return;
                }

                if (_handSubsystem == null)
                {
                    _handSubsystem =
                        subsystem;
                }
            }
        }

        private void ResetInputState()
        {
            _grabValue = 0f;
            _previousPerformed = _performed;
            _performed = false;
            _performedFist = false;
        }
    }
}