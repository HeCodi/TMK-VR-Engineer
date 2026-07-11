using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Game.Scripts.Interactable
{
    public class ValueInteractableObject : MonoBehaviour
    {
        public UnityAction<object> ChangedValue;

        private object _value;

        public object Value 
        {
            get => _value; 
            set 
            { 
                _value = value;
                ChangedValue.Invoke(value);
            }
        }
    }
}