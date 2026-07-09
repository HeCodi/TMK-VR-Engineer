using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

public class HandGrabAttachSwitcher : MonoBehaviour
{
    [SerializeField] private NearFarInteractor interactor;

    [SerializeField] private InteractionAttachController pinchAttach;
    [SerializeField] private InteractionAttachController fistAttach;

    [SerializeField] private GrabGestureButtonReader fistGesture;

    private void OnEnable()
    {
        fistGesture.OnGrabed += ChangeAttach;
    }

    private void OnDisable()
    {
        fistGesture.OnGrabed -= ChangeAttach;
    }

    private void ChangeAttach(GrabGestureButtonReader.SelectMethod selectMethod)
    {
        if (fistGesture.ReadIsPerformed())
            interactor.interactionAttachController = fistAttach;
        else
            interactor.interactionAttachController = pinchAttach;
        
    }
}