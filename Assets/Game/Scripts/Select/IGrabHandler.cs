using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Assets.Game.Scripts.Select
{
    public interface IGrabHandler
    {
        void OnSelect(IXRInteractor interactor);
        void OnDetach(IXRInteractor interactor);
    }
}