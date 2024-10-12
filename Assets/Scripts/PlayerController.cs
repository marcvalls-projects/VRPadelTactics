using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class PlayerController : MonoBehaviour
{
    public GameObject racketPrefab; // Assign the racket prefab in the inspector
    public Transform rightHandTransform; // Assign the right hand transform from XR Rig
    public BallController ballController;

    private GameObject instantiatedRacket;

    void Start()
    {
        InstantiateRacket();
    }

    void InstantiateRacket()
    {
        if (racketPrefab != null && rightHandTransform != null)
        {
            // Instantiate the racket prefab
            instantiatedRacket = Instantiate(racketPrefab, rightHandTransform);

            // Set the racket as a child of the right hand transform
            instantiatedRacket.transform.localPosition = new Vector3(0.05f, 0.25f, 0.1f);
            instantiatedRacket.transform.localRotation = Quaternion.AngleAxis(40, new Vector3(1, 0, 0)) *Quaternion.AngleAxis(-90, new Vector3(0, 0, 1));

            // Set collider and tag for collisions
            MeshCollider meshCollider = instantiatedRacket.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = instantiatedRacket.GetComponent<MeshFilter>().mesh;
            instantiatedRacket.tag = "Racket";

            // Adding the racket velocity tracker
            RacketTracker racketTracker = instantiatedRacket.AddComponent<RacketTracker>();
            racketTracker.ballController = ballController; // Assign the BallController reference
        }
        else
        {
            Debug.LogError("Racket Prefab or Right Hand Transform is not assigned.");
        }
    }
}
