using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

public class MoveGestureDetection : MonoBehaviour
{
    private static List<XRHandSubsystem> _subsystemsReuseList = new List<XRHandSubsystem>();

    [Header("Рука")]
    [SerializeField] private Handedness _handedness = Handedness.Left;

    [SerializeField] private GestureDynamicMoveProvider _moveProvider;
    [SerializeField] private GestureSnapTurn _snapTurnProvider;

    private bool _moveState;

    private void Update()
    {
        if (!_moveState)
            return;

        if (!TryGetSubsystem(out var subsystem))
            return;

        var hand = _handedness == Handedness.Left ? subsystem.leftHand : subsystem.rightHand;

        var calculated = hand.CalculateFingerShape(XRHandFingerID.Thumb, XRFingerShapeTypes.Spread | XRFingerShapeTypes.BaseCurl);

        calculated.TryGetSpread(out float spread);
        calculated.TryGetBaseCurl(out float baseCurl);

        spread = Mathf.InverseLerp(0.5f, 0.3f, spread);
        baseCurl = Mathf.InverseLerp(0.3f, 1, baseCurl);

        Vector2 speed = new Vector2(0, spread);

        if (baseCurl > 0.5f)
        {
            if (_handedness == Handedness.Left)
                _snapTurnProvider.TurnLeft();
            else if (_handedness == Handedness.Right)
                _snapTurnProvider.TurnRight();

            speed = Vector2.zero;
        }

        _moveProvider.SetGestureMovement(new Vector2(0, spread), _handedness);
    }

    public void OnMoveGestureFound() => _moveState = true;
    public void OnMoveGestureLost()
    {
        _moveProvider.SetGestureMovement(new Vector2(0, 0), _handedness);
        _moveState = false;
    }

    static bool TryGetSubsystem(out XRHandSubsystem system)
    {
        system = null;

        if (_subsystemsReuseList.Count == 0)
            SubsystemManager.GetSubsystems(_subsystemsReuseList);

        if (_subsystemsReuseList.Count > 0)
        {
            system = _subsystemsReuseList[0];
            return true;
        }
        return false;
    }
}
