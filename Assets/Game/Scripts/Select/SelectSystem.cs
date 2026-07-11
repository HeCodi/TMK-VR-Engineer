using Assets.Game.Scripts.Interactable.Abstract;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SelectSystem : MonoBehaviour
{
    [SerializeField] private NearFarInteractor _rightHand;
    [SerializeField] private NearFarInteractor _leftHand;

    private void OnEnable()
    {
        _rightHand.selectEntered.AddListener(Select);
        _leftHand.selectEntered.AddListener(Select);

        _rightHand.selectExited.AddListener(Detach);
        _leftHand.selectExited.AddListener(Detach);
    }
    private void OnDisable()
    {
        _rightHand.selectEntered.RemoveListener(Select);
        _leftHand.selectEntered.RemoveListener(Select);

        _rightHand.selectExited.RemoveListener(Detach);
        _leftHand.selectExited.RemoveListener(Detach);
    }

    private void Select(SelectEnterEventArgs args)
    {
        IXRInteractable interactable = args.interactableObject;
        IXRInteractor interactor = args.interactorObject;

        GameObject gameObjectInteractable = interactable.transform.gameObject;

        if (gameObjectInteractable.TryGetComponent(out BaseGrabHandler grabHandler))
        {
            grabHandler.OnSelect(interactor);
        }
    }
    
    private void Detach(SelectExitEventArgs args)
    {
        IXRInteractable interactable = args.interactableObject;
        IXRInteractor interactor = args.interactorObject;

        GameObject gameObjectInteractable = interactable.transform.gameObject;

        if (gameObjectInteractable.TryGetComponent(out BaseGrabHandler grabHandler))
        {
            grabHandler.OnDetach(interactor);
        }
    }
}
