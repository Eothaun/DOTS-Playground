using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class FreeCamMovement : MonoBehaviour
{
    [Tooltip("In units/sec")]
    public float movementSpeed = 10f;
    [Tooltip("In radians/pixel")]
    public float rotationSpeed = 0.4f;

    private Controls controls;
    

    void Start()
    {
        controls = new Controls();
        controls.DefaultActionMap.Enable();
    }
    
    void Update()
    {
        //Use the new input systems binding settings file
        Vector2 sidewaysMovement = controls.DefaultActionMap.Movement.ReadValue<Vector2>();
        float upDown = controls.DefaultActionMap.UpDown.ReadValue<float>();

        Vector3 translation = new Vector3(sidewaysMovement.x, upDown, sidewaysMovement.y).normalized * movementSpeed * Time.deltaTime;
        transform.Translate(translation, Space.Self);

        // Hardcoded mouse
        if (Mouse.current.rightButton.isPressed)
        {
            Vector3 currentEuler = transform.rotation.eulerAngles;
            Vector2Control mouseMovement = Mouse.current.delta;
            currentEuler.y += mouseMovement.x.ReadValue() * rotationSpeed;
            currentEuler.x += -mouseMovement.y.ReadValue() * rotationSpeed;
            // Clamp to -90(aka 270) to 90
            //TODO: Does someone have a cleaner solution than 2 branches?
            if (currentEuler.x > 180)
                currentEuler.x = Mathf.Max(currentEuler.x, (float) (360 - 90));
            else
                currentEuler.x = Mathf.Min(currentEuler.x, 90f);
            
            transform.rotation = Quaternion.Euler(currentEuler);
        }
    }
}
