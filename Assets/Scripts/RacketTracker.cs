using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RacketTracker : MonoBehaviour
{
    public BallController ballController;

    private Vector3 lastRacketPosition;
    private Quaternion lastRacketRotation;
    private float lastUpdateTime;

    void Start()
    {
        lastRacketPosition = transform.position;
        lastRacketRotation = transform.rotation;
        lastUpdateTime = Time.time;

        ballController.SetRacket(transform);
    }

    // Update is called once per frame
    void Update()
    {
        // Calculate linear velocity
        Vector3 racketPosition = transform.position;
        Vector3 racketVelocity = (racketPosition - lastRacketPosition) / (Time.time - lastUpdateTime);

        // Calculate angular velocity
        Quaternion currentRotation = transform.rotation;
        Quaternion rotationDifference = currentRotation * Quaternion.Inverse(lastRacketRotation);
        float angle;
        Vector3 axis;
        rotationDifference.ToAngleAxis(out angle, out axis);
        Vector3 racketAngularVelocity = axis * angle * Mathf.Deg2Rad / (Time.time - lastUpdateTime);

        // Pass the racket's position and velocity to the BallController
        ballController.UpdateRacketVelocity(racketVelocity, racketAngularVelocity);

        // Update last frame data
        lastRacketPosition = racketPosition;
        lastRacketRotation = currentRotation;
        lastUpdateTime = Time.time;
    }
}
