using Assets.Game.Scripts.Interactable.Abstract;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public abstract class BaseOneHandInteractable : BaseGrabHandler
{
    protected bool IsGrabbed;

    protected IXRInteractor CurrentHand;

    protected abstract void OnGrabStarted();

    protected abstract void ProcessInteraction();

    protected abstract void OnGrabEnded();

    protected override void WasSelect(IXRInteractor interactor)
    {
        if (IsGrabbed)
            return;

        CurrentHand = interactor;
        IsGrabbed = true;

        OnGrabStarted();
    }

    protected override void WasDetach(IXRInteractor interactor)
    {
        if (interactor != CurrentHand)
            return;

        IsGrabbed = false;

        OnGrabEnded();
    }

    protected virtual void Update()
    {
        if (!IsGrabbed)
            return;

        ProcessInteraction();
    }
}