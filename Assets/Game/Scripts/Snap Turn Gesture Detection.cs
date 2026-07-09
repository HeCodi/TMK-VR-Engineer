using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class SnapTurnGestureDetection : MonoBehaviour
{
    [Header("Рука")]
    [SerializeField] private Handedness _handedness = Handedness.Left;

    [Header("Snap Turn")]
    [SerializeField] private GestureSnapTurn _snapTurnProvider;
    //[SerializeField] private float _debounceTime = 0.5f;

    public void OnTurnGestureFound()
    {
        if(_handedness == Handedness.Left)
            _snapTurnProvider.TurnLeft();
        else if (_handedness == Handedness.Right)
            _snapTurnProvider.TurnRight();
    }

    public void OnTurnGestureLost()
    {
    
    }
}
