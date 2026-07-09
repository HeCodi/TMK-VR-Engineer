using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class GestureSnapTurn : LocomotionProvider
{
    [SerializeField] private float _turnAmount = 45f;
    [SerializeField] private float _debounceTime = 0.5f;

    private float _lastTurnTime;
    private XRBodyYawRotation _transformation = new XRBodyYawRotation();

    public void TurnLeft()
    {
        TryTurn(_turnAmount);
    }

    public void TurnRight()
    {
        TryTurn(-_turnAmount);
    }

    public void TurnAround()
    {
        TryTurn(180f);
    }

    private void TryTurn(float amount)
    {
        if (Time.time - _lastTurnTime < _debounceTime)
            return;

        if (locomotionState == LocomotionState.Idle)
            TryStartLocomotionImmediately();

        _transformation.angleDelta = amount;
        TryQueueTransformation(_transformation);
        _lastTurnTime = Time.time;

        TryEndLocomotion();
    }
}