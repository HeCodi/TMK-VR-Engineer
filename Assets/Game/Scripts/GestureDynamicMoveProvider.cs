using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class GestureDynamicMoveProvider : DynamicMoveProvider
{
    /// <summary>
    /// Чувствительность жеста
    /// </summary>
    [SerializeField] private float _gestureSensitivity = 1f;

    private Vector2 _gestureLeftMove;
    private Vector2 _gestureRightMove;

    public void SetGestureMovement(Vector2 vectorMove , Handedness interactorHandedness)
    {
        if (interactorHandedness == Handedness.Left)
            _gestureLeftMove = vectorMove * _gestureSensitivity;
        else if (interactorHandedness == Handedness.Right)
            _gestureRightMove = vectorMove * _gestureSensitivity;
        else
            throw new System.Exception("Передача перемещения без указания руки!");
    }

    protected override Vector3 ComputeDesiredMove(Vector2 input)
    {
        Vector2 gestureInput = _gestureLeftMove + _gestureRightMove;

        if (gestureInput == Vector2.zero)
            return Vector3.zero;

        return base.ComputeDesiredMove(gestureInput);
    }
}
