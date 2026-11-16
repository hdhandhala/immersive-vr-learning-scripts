using UnityEngine;

[DisallowMultipleComponent]
public class EnvironmentResettable : MonoBehaviour
{
    [Header("Reset Options")]
    [SerializeField] private bool resetPosition = true;
    [SerializeField] private bool resetRotation = true;
    [SerializeField] private bool resetScale = false;
    [SerializeField] private bool resetRigidbody = true;
    [SerializeField] private bool resetActiveState = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private bool initialActiveState;
    private Rigidbody rb;

    private void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
        initialActiveState = gameObject.activeSelf;
        rb = GetComponent<Rigidbody>();
    }

    public void ResetToInitialState()
    {
        if (resetRigidbody && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        if (resetPosition)
            transform.position = initialPosition;

        if (resetRotation)
            transform.rotation = initialRotation;

        if (resetScale)
            transform.localScale = initialScale;

        if (resetActiveState)
            gameObject.SetActive(initialActiveState);
    }
}