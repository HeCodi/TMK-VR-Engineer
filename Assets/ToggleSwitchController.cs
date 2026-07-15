using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

public class ToggleSwitchController : MonoBehaviour
{
    public Boolean Status => _status;
    private Boolean _status = false;
    private Boolean _isBlocked = false;

    [SerializeField] private Vector3 _offAngles;
    [SerializeField] private Vector3 _onAngles;

    [SerializeField] private GameObject _togglerObject;
    [SerializeField] private Single _animationDuration = 0.25f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _simpleInteractable;
    private XRPokeFilter _pokeFilter;
    private PokeThresholdData thresholdData;

    private AudioSource _audioSource;

    private void Awake()
    {
        _simpleInteractable = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        _pokeFilter = GetComponentInChildren<XRPokeFilter>();
        _audioSource = GetComponentInChildren<AudioSource>();
    }

    void Start()
    {
        _togglerObject.transform.localRotation = Quaternion.Euler(_offAngles);
        thresholdData = _pokeFilter.pokeConfiguration.Value;

        thresholdData.pokeDirection = PokeAxis.NegativeX;


        _simpleInteractable.selectEntered.AddListener(OnHoverEnter);
    }

    public void OnHoverEnter(SelectEnterEventArgs args)
    {
        if(_isBlocked)
            return;

        _audioSource.Play();

        _status = !_status;

        if(_status)
            thresholdData.pokeDirection = PokeAxis.X;
        else
            thresholdData.pokeDirection = PokeAxis.NegativeX;

        AnimateToggle(_status, _animationDuration);
        BlockerTask(_animationDuration).Forget();
    }

    private async UniTask BlockerTask(Single time)
    {
        _isBlocked = true;
        await UniTask.WaitForSeconds(time);
        _isBlocked = false;
    }

    private void AnimateToggle(Boolean status, Single time)
    {
        
    }
}
