using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GrabHandler : MonoBehaviour
{
    public IXRInteractor[] _interactors = new IXRInteractor[2];

    public void OnWasSelect(IXRInteractor interactor)
    {
        if (_interactors[0] is null)
            _interactors[0] = interactor;
        else
            _interactors[1] = interactor;
    }
    public void OnWasDetach(IXRInteractor interactor)
    {
        for (int i = 0; i < _interactors.Length; i++)
        {
            if (_interactors[i] is IXRInteractor)
            {
                _interactors[i] = null;
                return;
            }
        }
    }
}
