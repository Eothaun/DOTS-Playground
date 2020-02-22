// GENERATED AUTOMATICALLY FROM 'Assets/IO/Controls.inputactions'

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class @Controls : IInputActionCollection, IDisposable
{
    public InputActionAsset asset { get; }
    public @Controls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""Controls"",
    ""maps"": [
        {
            ""name"": ""DefaultActionMap"",
            ""id"": ""caff23fa-91dd-49fd-9dc6-8e1b8b3862bc"",
            ""actions"": [
                {
                    ""name"": ""Movement"",
                    ""type"": ""Value"",
                    ""id"": ""696e13d2-d6e9-459a-935d-b262c8be430d"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""UpDown"",
                    ""type"": ""Button"",
                    ""id"": ""e349a0a4-0f7e-4d69-a9aa-ec8c083695ef"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""Wasd"",
                    ""id"": ""37886eee-5b9d-4cb7-808a-6ed36089bfa1"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""804fe2fd-5f65-441a-9691-17c133907365"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Default"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""b55585e2-2d58-463e-810b-ad00806f7eea"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Default"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""bfb8116d-3027-4f82-be61-b5b1d9852c3f"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Default"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""a993db45-0654-415b-84db-6db238cdf546"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Default"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""UpDown"",
                    ""id"": ""7d4ce0e3-f086-47db-94b9-290bf0a127ca"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""UpDown"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""0ddd8bfa-be21-4216-bedb-66c9f1c73229"",
                    ""path"": ""<Keyboard>/q"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Default"",
                    ""action"": ""UpDown"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""d80c6068-a452-483e-b18a-8fd578c7183d"",
                    ""path"": ""<Keyboard>/e"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Default"",
                    ""action"": ""UpDown"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                }
            ]
        }
    ],
    ""controlSchemes"": [
        {
            ""name"": ""Default"",
            ""bindingGroup"": ""Default"",
            ""devices"": []
        }
    ]
}");
        // DefaultActionMap
        m_DefaultActionMap = asset.FindActionMap("DefaultActionMap", throwIfNotFound: true);
        m_DefaultActionMap_Movement = m_DefaultActionMap.FindAction("Movement", throwIfNotFound: true);
        m_DefaultActionMap_UpDown = m_DefaultActionMap.FindAction("UpDown", throwIfNotFound: true);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Enable()
    {
        asset.Enable();
    }

    public void Disable()
    {
        asset.Disable();
    }

    // DefaultActionMap
    private readonly InputActionMap m_DefaultActionMap;
    private IDefaultActionMapActions m_DefaultActionMapActionsCallbackInterface;
    private readonly InputAction m_DefaultActionMap_Movement;
    private readonly InputAction m_DefaultActionMap_UpDown;
    public struct DefaultActionMapActions
    {
        private @Controls m_Wrapper;
        public DefaultActionMapActions(@Controls wrapper) { m_Wrapper = wrapper; }
        public InputAction @Movement => m_Wrapper.m_DefaultActionMap_Movement;
        public InputAction @UpDown => m_Wrapper.m_DefaultActionMap_UpDown;
        public InputActionMap Get() { return m_Wrapper.m_DefaultActionMap; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(DefaultActionMapActions set) { return set.Get(); }
        public void SetCallbacks(IDefaultActionMapActions instance)
        {
            if (m_Wrapper.m_DefaultActionMapActionsCallbackInterface != null)
            {
                @Movement.started -= m_Wrapper.m_DefaultActionMapActionsCallbackInterface.OnMovement;
                @Movement.performed -= m_Wrapper.m_DefaultActionMapActionsCallbackInterface.OnMovement;
                @Movement.canceled -= m_Wrapper.m_DefaultActionMapActionsCallbackInterface.OnMovement;
                @UpDown.started -= m_Wrapper.m_DefaultActionMapActionsCallbackInterface.OnUpDown;
                @UpDown.performed -= m_Wrapper.m_DefaultActionMapActionsCallbackInterface.OnUpDown;
                @UpDown.canceled -= m_Wrapper.m_DefaultActionMapActionsCallbackInterface.OnUpDown;
            }
            m_Wrapper.m_DefaultActionMapActionsCallbackInterface = instance;
            if (instance != null)
            {
                @Movement.started += instance.OnMovement;
                @Movement.performed += instance.OnMovement;
                @Movement.canceled += instance.OnMovement;
                @UpDown.started += instance.OnUpDown;
                @UpDown.performed += instance.OnUpDown;
                @UpDown.canceled += instance.OnUpDown;
            }
        }
    }
    public DefaultActionMapActions @DefaultActionMap => new DefaultActionMapActions(this);
    private int m_DefaultSchemeIndex = -1;
    public InputControlScheme DefaultScheme
    {
        get
        {
            if (m_DefaultSchemeIndex == -1) m_DefaultSchemeIndex = asset.FindControlSchemeIndex("Default");
            return asset.controlSchemes[m_DefaultSchemeIndex];
        }
    }
    public interface IDefaultActionMapActions
    {
        void OnMovement(InputAction.CallbackContext context);
        void OnUpDown(InputAction.CallbackContext context);
    }
}
