using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.Animations;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;
using Vector2 = UnityEngine.Vector2;
using Vector4 = UnityEngine.Vector4;

public class InvalidPadelShotException : Exception
{
    public InvalidPadelShotException(string message) : base(message) { }
}

public class BallController : MonoBehaviour
{
    public float ballMass = 0.057f;
    public float gravity = 9.81f;
    public float frictionCoefficient = 0.5f;
    public float dragCoefficient = 0.5f;
    public float liftCoefficient = 0.5f;
    public float airDensity = 1.2f;
    public float spinDecay = 0.9f;
    public float restitutionCoefficient = 0.8f;

    public Transform cameraTransform;
    public GameObject cameraOffset;
    public Transform selfTransform;
    public BotAnimController ally;
    public BotAnimController adversary1;
    public BotAnimController adversary2;
    
    public UIController uiController;

    public Material trailMat;
    public Material enemyPathMat;
    public Material alliedPathMat;
    public Material expertPathMat;
    public LineRenderer additionalLineRenderer;
    
    public GameObject rightControllerVisual;
    public XRRayInteractor interactor;
    public NearFarInteractor nearFarInteractor;
    public InputActionAsset inputActionAsset;
    
    private Vector3 velocity;
    private Vector3 spin; // angular velocity

    private SphereCollider ballCollider;

    // racket
    private Transform racket;
    private Collider racketCollider;
    private Vector3 racketLinearVelocity;
    private Vector3 racketAngularVelocity;
    
    // enqueued shots: {startingPosition, startingVelocity, startingSpin}
    private Queue<Vector3[]> shotsQueue = new Queue<Vector3[]>();
    private PathDBDelegate.GameSituation currentGameSituation = new PathDBDelegate.GameSituation();
    
    private float timeScale = 1.0f;
    
    // path database
    [System.Serializable]
    public class Path
    {
        public List<Vector3> coordinates;
        public Dictionary<int, Vector3> bounceIndexToCollisionCoordinates;
        
        public Vector3 startingPosition;
        public Vector3 startingVelocity;
        public Vector3 startingSpin;
    };
    
    // delegates
    private PathDBDelegate pathDBDelegate;
    private TrailDelegate trailDelegate;
    private InputDelegate inputDelegate;
    private AIScoringDelegate aiScoringDelegate;
    
    // state
    private enum State
    {
        WaitingForUI,
        PathSimulation,
        LiveGameplay
    };
    State state = State.WaitingForUI;
    
    void Start()
    {
        velocity = Vector3.zero;
        spin = Vector3.zero;

        ballCollider = GetComponent<SphereCollider>();
        
        pathDBDelegate = new PathDBDelegate(this);
        trailDelegate = new TrailDelegate(GetComponent<TrailRenderer>(), GetComponent<LineRenderer>(), additionalLineRenderer, trailMat, alliedPathMat, enemyPathMat, expertPathMat);
        inputDelegate = new InputDelegate(interactor, nearFarInteractor, inputActionAsset, this, rightControllerVisual);
        aiScoringDelegate = new AIScoringDelegate();
    }

    public void SetRacket(Transform racketTransform)
    {
        racket = racketTransform;
        inputDelegate.SetRacket(racket);
        racketCollider = racket.GetComponent<Collider>();
    }

    public void UpdateRacketVelocity(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        racketLinearVelocity = linearVelocity;
        racketAngularVelocity = angularVelocity;
    }

    public Path SimulatePath(Vector3 startingPosition, Vector3 startingVelocity, Vector3 startingSpin)
    {
        ally.gameObject.SetActive(false);
        adversary1.gameObject.SetActive(false);
        adversary2.gameObject.SetActive(false);
        
        State stateBeforeSimulation = state;
        Vector3 positionBeforeSimulation = transform.position;
        
        state = State.PathSimulation;
        transform.position = startingPosition;
        bool racketWasPreviouslyActive = false;
        if (racket != null)
        {
            racketWasPreviouslyActive = racket.gameObject.activeSelf;
            racket.gameObject.SetActive(false);
        }

        Action restoreState = () =>
        {
            if (racket != null && racketWasPreviouslyActive)
            {
                racket.gameObject.SetActive(true);
            }

            transform.position = positionBeforeSimulation;
            state = stateBeforeSimulation;
            
            ally.gameObject.SetActive(true);
            adversary1.gameObject.SetActive(true);
            adversary2.gameObject.SetActive(true);
        };

        Path result = new Path();
        result.startingPosition = startingPosition;
        result.startingVelocity = startingVelocity;
        result.startingSpin = startingSpin;
        result.bounceIndexToCollisionCoordinates = new Dictionary<int, Vector3>();
        result.coordinates = new List<Vector3> { startingPosition };
        
        const float samplingPeriod = 0.01f;
        bool isServingFromRightSide = startingPosition.z > 0;
        uint numBounces = 0;
        bool hasHitOpponentSide = false;

        try
        {
            while (numBounces < 4)
            {
                PerformPhysicsStep(transform, ref startingVelocity, ref startingSpin, samplingPeriod);
                
                if (transform.position.y < -1.0f)
                {
                    throw new InvalidPadelShotException("Invalid shot: Ball has escaped the court.");
                }
                
                Vector3 positionBeforeCollisionAdjusting = transform.position;
                
                // Check for collisions and retrieve the tag of the hit object
                if (CheckForCollisions(result.coordinates[result.coordinates.Count - 1], ref startingVelocity, out string collisionTag))
                {
                    ++numBounces;
                    if (collisionTag == "Floor")
                    {
                        if (!hasHitOpponentSide)
                        {
                            // Ensure first bounce is on opponent's side
                            if (IsBounceOnOpponentSide(transform.position, isServingFromRightSide))
                            {
                                result.bounceIndexToCollisionCoordinates[result.coordinates.Count] = positionBeforeCollisionAdjusting;
                                hasHitOpponentSide = true;
                            }
                            else
                            {
                                throw new InvalidPadelShotException("Invalid shot: Ball bounced on the same side as the serve.");
                            }
                        }
                        else if (numBounces > 1)
                        {
                                break;
                        }
                    }
                    else if (collisionTag == "Wall" || collisionTag == "Grid")
                    {
                        // Wall collision is allowed after bouncing on the floor
                        if (!hasHitOpponentSide)
                        {
                            throw new InvalidPadelShotException("Invalid shot: Ball hit the wall before bouncing on the opponent's side.");
                        }
                        
                        result.bounceIndexToCollisionCoordinates[result.coordinates.Count] = positionBeforeCollisionAdjusting;
                    }
                    else if (collisionTag == "Net")
                    {
                        throw new InvalidPadelShotException("Invalid shot: Ball hit the net.");
                    }
                    else
                    {
                        throw new InvalidPadelShotException($"Invalid shot: Ball collided with an invalid object '{collisionTag}'.");
                    }
                }

                result.coordinates.Add(transform.position);
            }
        }
        catch (Exception e)
        {
            throw e;
        }
        finally
        {
            restoreState();
        }

        restoreState();
        
        return result;
    }

    private void LookAt(Transform mannequin, Vector3 target)
    {
        Vector3 direction = target - mannequin.position;
        direction.y = 0;
        
        if (direction.magnitude > 0)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            mannequin.rotation = lookRotation;
        }
    }

    private void EnqueueShot(Vector3 position, Vector3 velocity, Vector3 spin)
    {
        shotsQueue.Enqueue(new Vector3[] { position, velocity, spin });
    }

    private void ClearShotQueue()
    {
        shotsQueue.Clear();
    }

    private void ServeEnqueuedShot()
    {
        if (shotsQueue.Count > 0)
        {
            transform.position = shotsQueue.Peek()[0];
            velocity = shotsQueue.Peek()[1];
            spin = shotsQueue.Peek()[2];
            
            shotsQueue.Dequeue();

            trailDelegate.ActivateTrail();
        }
    }

    private void PrepareCurrentGameSituation()
    {
        var situation = currentGameSituation;
        
        EnqueueShot(new Vector3(situation.startingPosition[0].x, 10f, situation.startingPosition[0].z), Vector3.zero, Vector3.zero);
        EnqueueShot(situation.startingPosition[0], situation.startingVelocity[0], situation.startingSpin[0]);
        EnqueueShot(situation.startingPosition[1], situation.startingVelocity[1], situation.startingSpin[1]);
        
        selfTransform.position = new Vector3(situation.selfPosition[0].x, 0, situation.selfPosition[0].y);
        LookAt(selfTransform, new Vector3(selfTransform.position.x, 1.6f, 0f));
            
        ally.transform.position = new Vector3(situation.allyPosition[0].x, 0, situation.allyPosition[0].y);
        LookAt(ally.transform, new Vector3(ally.transform.position.x, 1.6f, 0f));
        ally.StayIdle();
        
        adversary1.transform.position = new Vector3(situation.adversary1Position[0].x, 0, situation.adversary1Position[0].y);
        LookAt(adversary1.transform, new Vector3(adversary1.transform.position.x, 1.6f, 0f));
        adversary1.StayIdle();
        
        adversary2.transform.position = new Vector3(situation.adversary2Position[0].x, 0, situation.adversary2Position[0].y);
        LookAt(adversary2.transform, new Vector3(adversary2.transform.position.x, 1.6f, 0f));
        adversary2.StayIdle();
    }

    public void SetupRandomGame()
    {
        currentGameSituation = pathDBDelegate.GetRandomSituation();
        PrepareCurrentGameSituation();
    }

    public void DrawPath(Vector3[] path, Color color)
    {
        trailDelegate.ShowPath(path, color);
    }

    private IEnumerator RunGameSequence()
    {
        ServeEnqueuedShot();
        
        while (!(shotsQueue.Count > 0 && Vector3.Distance(shotsQueue.Peek()[0], transform.position) < 6f))
            yield return null;
        
        if (Vector3.Distance(adversary1.transform.position, transform.position) <
            Vector3.Distance(adversary2.transform.position, transform.position))
        {
            adversary1.Hit();
        }
        else
        {
            adversary2.Hit();
        }

        while (!(shotsQueue.Count > 0 && Vector3.Distance(shotsQueue.Peek()[0], transform.position) < 0.4f))
            yield return null;

        Vector3 startingPosition = shotsQueue.Peek()[0];
        
        trailDelegate.Clear();
        trailDelegate.ShowPath(SimulatePath(shotsQueue.Peek()[0], shotsQueue.Peek()[1], shotsQueue.Peek()[2]).coordinates.ToArray(), Color.red);
        
        // the shot is launched
        ServeEnqueuedShot();
        ally.RunTo(new Vector3(currentGameSituation.allyPosition[1].x, 0, currentGameSituation.allyPosition[1].y));
        adversary1.RunTo(new Vector3(currentGameSituation.adversary1Position[1].x, 0, currentGameSituation.adversary1Position[1].y));
        adversary2.RunTo(new Vector3(currentGameSituation.adversary2Position[1].x, 0, currentGameSituation.adversary2Position[1].y));
        
        // First input: response location select
        inputDelegate.SetInputType(InputDelegate.InputType.TeleportInteractor);
        while (!inputDelegate.HasNewInput())
        {
            timeScale = Mathf.Max(0f, (transform.position.z + Mathf.Sign(startingPosition.z))* 0.5f/ (startingPosition.z + Mathf.Sign(startingPosition.z)));
            yield return null;
        }

        Vector3 tpTarget = inputDelegate.GetNewInput();
        Teleport(new Vector3(tpTarget.x, 0, tpTarget.z));
        inputDelegate.SetInputType(InputDelegate.InputType.Racket);
        yield return new WaitForSeconds(1.5f);
        
        timeScale = 0.2f;
        
        // Wait for the racket to go near the ball
        while (Vector3.Distance(transform.position, racket.position) > 0.5f)
        {
            timeScale = (Vector3.Distance(transform.position, racket.position) - 0.5f) * 0.4f / 3f;
            yield return null;
        }
        
        timeScale = 0f;
        
        // Second input: target location select
        inputDelegate.SetTargetInteractorData(transform.position, gravity);
        inputDelegate.SetInputType(InputDelegate.InputType.TargetInteractor);
        while (!inputDelegate.HasNewInput())
        {
            yield return null;
        }
        Vector3 target = inputDelegate.GetNewInput();

        yield return new WaitForSeconds(1f);
        
        // Third input: max height select
        inputDelegate.SetHeightInteractorData(transform.position, target, gravity);
        inputDelegate.SetInputType(InputDelegate.InputType.HeightInteractor);
        Vector3 responseVelocity = Vector3.zero;
        
        Path path = new Path();
        bool validPath = false;
        while (!validPath)
        {
            while (!inputDelegate.HasNewInput())
            {
                yield return null;
            }
            responseVelocity = -inputDelegate.GetNewInput();

            try
            {
                path = SimulatePath(transform.position, responseVelocity, Vector3.zero);
                validPath = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        // Serve the described shot
        timeScale = 1f;
        inputDelegate.SetInputType(InputDelegate.InputType.NearFar);
        ClearPaths(Color.blue);
        DrawPath(path.coordinates.ToArray(), Color.blue);
        ClearShotQueue();
        EnqueueShot(transform.position, responseVelocity, Vector3.zero);
        trailDelegate.ActivateTrail();
        ServeEnqueuedShot();

        yield return new WaitForSeconds(7);
        trailDelegate.Clear();

        ClearShotQueue();
        state = State.WaitingForUI;

        AIScoringDelegate.ScoreData scoringData = new AIScoringDelegate.ScoreData();
        scoringData.targetX = target.x;
        scoringData.targetZ = target.z;
        scoringData.opponent1X = currentGameSituation.adversary1Position[1].x;
        scoringData.opponent1Z = currentGameSituation.adversary1Position[1].y;
        scoringData.opponent2X = currentGameSituation.adversary2Position[1].x;
        scoringData.opponent2Z = currentGameSituation.adversary2Position[1].y;
        scoringData.averageDistToOpponents = (Vector2.Distance(currentGameSituation.adversary1Position[1], new Vector2(target.x, target.z)) +
                                             Vector2.Distance(currentGameSituation.adversary2Position[1], new Vector2(target.x, target.z)))/2f;
        scoringData.targetDistToCenterOfHalfCourt = Mathf.Min(Vector3.Distance(target, new Vector3(0, 0, -5f)), Vector3.Distance(target, new Vector3(0, 0, 5f)));
        uiController.LaunchFinishUI(cameraTransform.position, (int) (100 * aiScoringDelegate.GetScore(scoringData)));

        while (true)
        {
            while (!uiController.HasWatchExpertBeenRequested()) yield return null;
            uiController.SetWatchExpertRequested(false);
            uiController.HideUI();
            
            
            Teleport(currentGameSituation.selfPosition[0]);
            yield return new WaitForSeconds(0.75f);
            PrepareCurrentGameSituation();
            
            ServeEnqueuedShot();
            state = State.LiveGameplay;
            
            while (!(shotsQueue.Count > 0 && Vector3.Distance(shotsQueue.Peek()[0], transform.position) < 6f))
                yield return null;
        
            if (Vector3.Distance(adversary1.transform.position, transform.position) <
                Vector3.Distance(adversary2.transform.position, transform.position))
            {
                adversary1.Hit();
            }
            else
            {
                adversary2.Hit();
            }

            while (!(shotsQueue.Count > 0 && Vector3.Distance(shotsQueue.Peek()[0], transform.position) < 0.4f))
                yield return null;
        
            trailDelegate.Clear();
            path = SimulatePath(shotsQueue.Peek()[0], shotsQueue.Peek()[1], shotsQueue.Peek()[2]);
            trailDelegate.ShowPath(path.coordinates.ToArray(), Color.red);
        
            // the shot is relaunched for the expert to answer
            ServeEnqueuedShot();
            ally.RunTo(new Vector3(currentGameSituation.allyPosition[1].x, 0, currentGameSituation.allyPosition[1].y));
            adversary1.RunTo(new Vector3(currentGameSituation.adversary1Position[1].x, 0, currentGameSituation.adversary1Position[1].y));
            adversary2.RunTo(new Vector3(currentGameSituation.adversary2Position[1].x, 0, currentGameSituation.adversary2Position[1].y));

            Vector3 triggerCoordinates = Vector3.zero;
            foreach (Vector3 point in path.coordinates)
            {
                if (Vector3.Distance(shotsQueue.Peek()[0], point) <
                    Vector3.Distance(shotsQueue.Peek()[0], triggerCoordinates))
                {
                    triggerCoordinates = point;
                }
            }

            while (!(shotsQueue.Count > 0 && Vector3.Distance(triggerCoordinates, transform.position) < 0.4f))
            {
                yield return null;
            }

            timeScale = 0.6f;
            trailDelegate.ShowPath(SimulatePath(shotsQueue.Peek()[0], shotsQueue.Peek()[1], shotsQueue.Peek()[2]).coordinates.ToArray(), Color.green);
            ServeEnqueuedShot();
            
            yield return new WaitForSeconds(7);
            timeScale = 1f;
            trailDelegate.Clear();

            ClearShotQueue();
            state = State.WaitingForUI;
            
            uiController.LaunchFinishUI(cameraTransform.position);
        }
    }
    
    public void Teleport(Vector3 position)
    {
        StartCoroutine(uiController.ExecuteFadeAction(() =>
        {
            selfTransform.position = position;
            cameraTransform.localPosition = Vector3.zero;
        }));
    }
    
    public void StartLiveGame()
    {
        StartCoroutine(RunGameSequence());
        state = State.LiveGameplay;
    }

    public void ClearPaths(Color colour)
    {
        trailDelegate.ClearPaths(colour);
    }

    void FixedUpdate()
    {
        if (state == State.LiveGameplay)
        {
            inputDelegate.Update();
                
            Vector3 previousPosition = transform.position;
        
            PerformPhysicsStep(transform, ref velocity, ref spin, Time.fixedDeltaTime * timeScale);
            
            CheckForCollisions(previousPosition, ref velocity, out _);
        }
    }

    void PerformPhysicsStep(Transform ballTransform, ref Vector3 currentVelocity, ref Vector3 currentSpin, float deltaTime)
    {
        // Calculate gravity force
        Vector3 gravityForce = Vector3.down * gravity * ballMass;

        // Calculate air resistance force
        Vector3 dragForce = -currentVelocity.normalized * (0.5f * dragCoefficient * airDensity * Mathf.PI * ballCollider.radius * ballCollider.radius * currentVelocity.sqrMagnitude);

        // Calculate Magnus force
        Vector3 magnusForce = 0.5f * liftCoefficient * airDensity * Mathf.PI * ballCollider.radius * ballCollider.radius * currentVelocity.magnitude * currentVelocity.magnitude * Vector3.Cross(currentSpin.normalized, currentVelocity.normalized);

        // Calculate total force
        Vector3 totalForce = gravityForce + dragForce + magnusForce;

        // Update velocity
        currentVelocity += totalForce / ballMass * deltaTime;

        // Update spin
        currentSpin *= spinDecay; // decay spin over time

        // Update the ball's position and rotation
        ballTransform.position += currentVelocity * deltaTime;
        ballTransform.rotation *= Quaternion.Euler(currentSpin * deltaTime);
    }

    bool CheckForCollisions(Vector3 previousPosition, ref Vector3 currentVelocity, out string collisionTag)
    {
        collisionTag = string.Empty;
        RaycastHit hit;
        Vector3 direction = transform.position - previousPosition;

        // Check for any collisions
        if (Physics.Raycast(previousPosition, direction, out hit, direction.magnitude + ballCollider.radius))
        {
            collisionTag = hit.collider.tag;
            
            Vector3 contactPoint = hit.point;
            Vector3 normal = hit.normal;

            // Calculate racket velocity at the point of impact
            Vector3 velocityAtImpact = Vector3.zero;
            if (racketCollider != null && racketCollider.bounds.Intersects(ballCollider.bounds))
            {
                velocityAtImpact = GetRacketVelocityAtPoint(contactPoint);
            }

            // Handle the collision
            transform.position = contactPoint + ballCollider.radius * normal;
            OnCollision(contactPoint, normal, velocityAtImpact, ref currentVelocity, collisionTag);

            return true;
        }

        return false;
    }

    Vector3 GetRacketVelocityAtPoint(Vector3 point)
    {
        Vector3 racketRotationOriginWorld = cameraTransform.TransformPoint(new Vector3(0.15f, -0.15f, 0));

        Vector3 radiusVector = point - racketRotationOriginWorld;
        Vector3 angularContribution = Vector3.Cross(racketAngularVelocity, radiusVector);
        return racketLinearVelocity + angularContribution;
    }
    
    float NormalDistribution(float mean, float stdDev)
    {
        float u1 = UnityEngine.Random.value;
        float u2 = UnityEngine.Random.value;
        float z0 = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Cos(2.0f * Mathf.PI * u2);
        return z0 * stdDev + mean;
    }

    void OnCollision(Vector3 contactPoint, Vector3 normal, Vector3 velocityAtContactPoint, ref Vector3 currentVelocity, string collisionTag)
    {
        Vector3 relativeVelocity = currentVelocity - velocityAtContactPoint;

        currentVelocity = Vector3.Reflect(relativeVelocity, normal);
        if (collisionTag == "Grid")
        {
            // Generate normally distributed noise
            float noiseMagnitude = 0.1f; 
            Vector3 noise = new Vector3(
                Mathf.Clamp(NormalDistribution(0, noiseMagnitude), -noiseMagnitude, noiseMagnitude),
                Mathf.Clamp(NormalDistribution(0, noiseMagnitude), -noiseMagnitude, noiseMagnitude),
                Mathf.Clamp(NormalDistribution(0, noiseMagnitude), -noiseMagnitude, noiseMagnitude)
            );

            currentVelocity += noise; 
        }
        
        currentVelocity *= restitutionCoefficient;
        if (collisionTag == "Net")
        {
            currentVelocity /= 4f;
        }

        Vector3 radiusVector = contactPoint - transform.position;

        Vector3 tangent = Vector3.Cross(normal, radiusVector).normalized;
        Vector3 frictionImpulse = frictionCoefficient * tangent * Vector3.Dot(relativeVelocity.normalized, tangent);

        if (radiusVector.magnitude > 0)
        {
            spin += frictionImpulse / radiusVector.magnitude;
        }
    }
    
    bool IsBounceOnOpponentSide(Vector3 ballPosition, bool isServingFromRightSide)
    {
        if (isServingFromRightSide)
        {
            return ballPosition.z < 0;
        }
        else
        {
            return ballPosition.z > 0;
        }
    }
}
