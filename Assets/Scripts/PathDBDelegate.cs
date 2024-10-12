using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Newtonsoft.Json;

public class PathDBDelegate
{
    enum ShotClassificationByBounces
    {
        NoBounce,
        SingleBounce,
        SideBounce,
        BackBounce,
        SideBackBounces,
        BackSideBounces
    };
    
    [System.Serializable]
    class ShotsKdTree : KDTree<UnityEngine.Vector4, int>
    {
        public ShotsKdTree()
            : base  (
                4,
                (point, dimension) => dimension switch
                {
                    0 => point.x,
                    1 => point.y,
                    2 => point.z,
                    3 => point.w,
                    _ => throw new ArgumentException("Invalid dimension")
                },
                (point1, point2) =>
                    Mathf.Sqrt
                    (
                        Mathf.Pow(point1.x - point2.x, 2) +
                        Mathf.Pow(point1.y - point2.y, 2) +
                        Mathf.Pow(point1.z - point2.z, 2) +
                        Mathf.Pow(point1.w - point2.w, 2)
                    )
            )
        {
        }
        
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings{ReferenceLoopHandling = ReferenceLoopHandling.Ignore});
        }
    
        public static ShotsKdTree Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<ShotsKdTree>(json);
        }
    };
    
    [System.Serializable]
    public struct DataRow
    {
        public int id;
        public int frame;
        public float time;
        public string role1;
        public float x1;
        public float y1;
        public string role2;
        public float x2;
        public float y2;
        public string role3;
        public float x3;
        public float y3;
        public string role4;
        public float x4;
        public float y4;
        public float ballx;
        public float bally;
        public float speed1x;
        public float speed1y;
        public float speed2x;
        public float speed2y;
        public float speed3x;
        public float speed3y;
        public float speed4x;
        public float speed4y;
        public string shot;
        public float targetx;
        public float targety;
        public float fromx;
        public float fromy;
        public float duration;
        public string shot_full;
        public char lastHit;
        public int rally;
    }
    
    [System.Serializable]
    public class PathArray
    {
        public BallController.Path[] paths;
    }

    public class GameSituation
    {
        public Vector3[] startingPosition;
        public Vector3[] startingVelocity;
        public Vector3[] startingSpin;

        public Vector2[] selfPosition;
        public Vector2[] allyPosition;
        public Vector2[] adversary1Position;
        public Vector2[] adversary2Position;
    }
    
    private BallController ballController;
    
    // Precomputed data
    private PathArray precomputedPaths;
    private ShotsKdTree noBounceShotPathIndices;
    private ShotsKdTree singleBounceShotPathIndices;
    private ShotsKdTree sideShotPathIndices;
    private ShotsKdTree backShotPathIndices;
    private ShotsKdTree sideBackShotPathIndices;
    private ShotsKdTree backSideShotPathIndices;
    
    // CSV 2D amateur match data
    public List<DataRow> dataRows = new List<DataRow>();
    private static readonly Dictionary<string, ShotClassificationByBounces> acronymToShotType = 
        new Dictionary<string, ShotClassificationByBounces>
    {
        { "SPD", ShotClassificationByBounces.BackBounce },
        { "SPR", ShotClassificationByBounces.BackBounce },
        { "BPD", ShotClassificationByBounces.BackBounce },
        { "BPR", ShotClassificationByBounces.BackBounce },
        { "CD", ShotClassificationByBounces.BackBounce },
        { "CR", ShotClassificationByBounces.BackBounce },

        { "DPA", ShotClassificationByBounces.SideBackBounces },
        { "DPAG", ShotClassificationByBounces.SideBackBounces },

        { "DPC", ShotClassificationByBounces.BackSideBounces },
        { "DPCG", ShotClassificationByBounces.BackSideBounces },

        { "D", ShotClassificationByBounces.SingleBounce },
        { "R", ShotClassificationByBounces.SingleBounce },
        { "R1", ShotClassificationByBounces.SingleBounce },
        { "R2", ShotClassificationByBounces.SingleBounce },

        { "AD", ShotClassificationByBounces.SideBounce },
        { "AR", ShotClassificationByBounces.SideBounce },
        { "PLD", ShotClassificationByBounces.SideBounce },
        { "PLR", ShotClassificationByBounces.SideBounce },

        { "VD", ShotClassificationByBounces.NoBounce },
        { "VR", ShotClassificationByBounces.NoBounce },
        { "B", ShotClassificationByBounces.NoBounce },
        { "DJD", ShotClassificationByBounces.NoBounce }
    };

    public PathDBDelegate(BallController ballController)
    {
        this.ballController = ballController;
        
        ReadCSV();

        try
        {
            string json = File.ReadAllText(Application.dataPath + "/../PathDBData/PrecomputedPaths.json");
            precomputedPaths = JsonUtility.FromJson<PathArray>(json);

            json = File.ReadAllText(Application.dataPath + "/../PathDBData/NoBounceShotsKDTree.json");
            noBounceShotPathIndices = ShotsKdTree.Deserialize(json);
            
            json = File.ReadAllText(Application.dataPath + "/../PathDBData/SingleBounceShotsKDTree.json");
            singleBounceShotPathIndices = ShotsKdTree.Deserialize(json);
            
            json = File.ReadAllText(Application.dataPath + "/../PathDBData/SideShotsKDTree.json");
            sideShotPathIndices = ShotsKdTree.Deserialize(json);
            
            json = File.ReadAllText(Application.dataPath + "/../PathDBData/BackShotsKDTree.json");
            backShotPathIndices = ShotsKdTree.Deserialize(json);
            
            json = File.ReadAllText(Application.dataPath + "/../PathDBData/SideBackShotsKDTree.json");
            sideBackShotPathIndices = ShotsKdTree.Deserialize(json);
            
            json = File.ReadAllText(Application.dataPath + "/../PathDBData/BackSideShotsKDTree.json");
            backSideShotPathIndices = ShotsKdTree.Deserialize(json);
        }
        catch (Exception)
        {
            PrecomputePaths();
        }
    }

    private void AssignPositionAccordingToRole(string role, Vector2 position, ref Vector2 self, ref Vector2 ally,
        ref Vector2 adversary)
    {
        switch (role)
        {
            case "receiver":
                self = position;
                break;
            
            case "teammate":
                ally = position;
                break;
            
            case "opponent":
                adversary = position;
                break;
            
            default:
                throw new Exception("Unknown role: " + role);
        }
    }
    
    private bool isOkay(float x, float y)
    {
        return x >= -4.5f && x <= 4.5f && y >= -9.5f && y <= 9.5f;
    }

    private bool isSuitableToBeSimulated(DataRow dataRow)
    {
        return acronymToShotType.ContainsKey(dataRow.shot_full) &&
               isOkay(dataRow.x1, dataRow.y1) &&
               isOkay(dataRow.x2, dataRow.y2) &&
               isOkay(dataRow.x3, dataRow.y3) &&
               isOkay(dataRow.x4, dataRow.y4);
    }

    private BallController.Path GetMostSimilarPath(float fromx, float fromz, float targetx, float targetz, ShotClassificationByBounces shotType)
    {
        var kdTreeSearchInput = new Vector4(fromx, fromz, targetx, targetz);
        switch (shotType)
        {
            case ShotClassificationByBounces.NoBounce:
                return precomputedPaths.paths[noBounceShotPathIndices.FindNearest(kdTreeSearchInput)];
            
            case ShotClassificationByBounces.SingleBounce:
                return precomputedPaths.paths[singleBounceShotPathIndices.FindNearest(kdTreeSearchInput)];
            
            case ShotClassificationByBounces.SideBounce:
                return precomputedPaths.paths[sideShotPathIndices.FindNearest(kdTreeSearchInput)];
            
            case ShotClassificationByBounces.BackBounce:
                return precomputedPaths.paths[backShotPathIndices.FindNearest(kdTreeSearchInput)];
            
            case ShotClassificationByBounces.SideBackBounces:
                return precomputedPaths.paths[sideBackShotPathIndices.FindNearest(kdTreeSearchInput)];
            
            case ShotClassificationByBounces.BackSideBounces:
                return precomputedPaths.paths[backSideShotPathIndices.FindNearest(kdTreeSearchInput)];
            
            default:
                // should never be reached
                throw new Exception("Unknown shot type: " + shotType);
        }
    }

    private BallController.Path GetMostSimilarPath(DataRow dataRow)
    {
        BallController.Path path = GetMostSimilarPath(dataRow.fromx, dataRow.fromy, dataRow.targetx, dataRow.targety, acronymToShotType[dataRow.shot_full]);
        BallController.Path pathXSymmetry = GetMostSimilarPath(-dataRow.fromx, dataRow.fromy, -dataRow.targetx, dataRow.targety, acronymToShotType[dataRow.shot_full]);
        BallController.Path pathZSymmetry = GetMostSimilarPath(dataRow.fromx, -dataRow.fromy, dataRow.targetx, -dataRow.targety, acronymToShotType[dataRow.shot_full]);
        BallController.Path pathXZSymmetry = GetMostSimilarPath(-dataRow.fromx, -dataRow.fromy, -dataRow.targetx, -dataRow.targety, acronymToShotType[dataRow.shot_full]);

        pathXSymmetry.startingPosition.x *= -1;
        pathXSymmetry.startingVelocity.x *= -1;
        for (int i = 0; i < pathXSymmetry.coordinates.Count; i++)
        {
            pathXSymmetry.coordinates[i] = new Vector3(-pathXSymmetry.coordinates[i].x, pathXSymmetry.coordinates[i].y, pathXSymmetry.coordinates[i].z);
        }
        
        pathZSymmetry.startingPosition.z *= -1;
        pathZSymmetry.startingVelocity.z *= -1;
        for (int i = 0; i < pathZSymmetry.coordinates.Count; i++)
        {
            pathZSymmetry.coordinates[i] = new Vector3(pathZSymmetry.coordinates[i].x, pathZSymmetry.coordinates[i].y, -pathZSymmetry.coordinates[i].z);
        }
        
        pathXZSymmetry.startingPosition.x *= -1;
        pathXZSymmetry.startingVelocity.x *= -1;
        pathXZSymmetry.startingPosition.z *= -1;
        pathXZSymmetry.startingVelocity.z *= -1;
        for (int i = 0; i < pathXZSymmetry.coordinates.Count; i++)
        {
            pathXZSymmetry.coordinates[i] = new Vector3(-pathXZSymmetry.coordinates[i].x, pathXZSymmetry.coordinates[i].y, -pathXZSymmetry.coordinates[i].z);
        }

        ref BallController.Path resultPath = ref path;
        
        Func<BallController.Path, DataRow, float > getDistanceSum =
            (BallController.Path p, DataRow row) => Vector2.Distance(new Vector2(row.fromx, row.fromy), new Vector2(p.coordinates[0].x, p.coordinates[0].z)) +
                                                    Vector2.Distance(new Vector2(row.targetx, row.targety), new Vector2(p.coordinates[p.coordinates.Count - 1].x, p.coordinates[p.coordinates.Count - 1].z));
        
        if (getDistanceSum(resultPath, dataRow) > getDistanceSum(pathXSymmetry, dataRow)) resultPath = pathXSymmetry;
        if (getDistanceSum(resultPath, dataRow) > getDistanceSum(pathZSymmetry, dataRow)) resultPath = pathZSymmetry;
        if (getDistanceSum(resultPath, dataRow) > getDistanceSum(pathXZSymmetry, dataRow)) resultPath = pathXZSymmetry;
        
        return resultPath;
    }
    
    public GameSituation GetRandomSituation()
    {
        GameSituation situation = new GameSituation();
        situation.startingPosition = new Vector3[2];
        situation.startingVelocity = new Vector3[2];
        situation.startingSpin = new Vector3[2];
        situation.selfPosition = new Vector2[2];
        situation.allyPosition = new Vector2[2];
        situation.adversary1Position = new Vector2[2];
        situation.adversary2Position = new Vector2[2];
        
        bool validSituation = false;
        while (!validSituation)
        {
            try
            { 
                int i;
                while (true)
                {
                    i = UnityEngine.Random.Range(0, dataRows.Count - 1);

                    if (isSuitableToBeSimulated(dataRows[i]) &&
                        isSuitableToBeSimulated(dataRows[i + 1]))
                    {
                        break;
                    }
                }
                
                var path = GetMostSimilarPath(dataRows[i]);
                ballController.SimulatePath(path.startingPosition, path.startingVelocity, path.startingSpin);
                
                situation.startingPosition[0] = path.startingPosition;
                situation.startingVelocity[0] = path.startingVelocity;
                situation.startingSpin[0] = path.startingSpin;
                
                AssignPositionAccordingToRole(dataRows[i].role1, new Vector2(dataRows[i].x1, dataRows[i].y1), 
                    ref situation.selfPosition[0], ref situation.allyPosition[0], ref situation.adversary1Position[0]);
                AssignPositionAccordingToRole(dataRows[i].role2, new Vector2(dataRows[i].x2, dataRows[i].y2), 
                    ref situation.selfPosition[0], ref situation.allyPosition[0], ref situation.adversary2Position[0]);
                AssignPositionAccordingToRole(dataRows[i].role3, new Vector2(dataRows[i].x3, dataRows[i].y3), 
                    ref situation.selfPosition[0], ref situation.allyPosition[0], ref situation.adversary1Position[0]);
                AssignPositionAccordingToRole(dataRows[i].role4, new Vector2(dataRows[i].x4, dataRows[i].y4), 
                    ref situation.selfPosition[0], ref situation.allyPosition[0], ref situation.adversary2Position[0]);
                
                path = GetMostSimilarPath(dataRows[i + 1]);
                ballController.SimulatePath(path.startingPosition, path.startingVelocity, path.startingSpin);

                situation.startingPosition[1] = path.startingPosition;
                situation.startingVelocity[1] = path.startingVelocity;
                situation.startingSpin[1] = path.startingSpin;
                
                AssignPositionAccordingToRole(dataRows[i].role1, new Vector2(dataRows[i+1].x1, dataRows[i+1].y1), 
                    ref situation.selfPosition[1], ref situation.allyPosition[1], ref situation.adversary1Position[1]);
                AssignPositionAccordingToRole(dataRows[i].role2, new Vector2(dataRows[i+1].x2, dataRows[i+1].y2), 
                    ref situation.selfPosition[1], ref situation.allyPosition[1], ref situation.adversary2Position[1]);
                AssignPositionAccordingToRole(dataRows[i].role3, new Vector2(dataRows[i+1].x3, dataRows[i+1].y3), 
                    ref situation.selfPosition[1], ref situation.allyPosition[1], ref situation.adversary1Position[1]);
                AssignPositionAccordingToRole(dataRows[i].role4, new Vector2(dataRows[i+1].x4, dataRows[i+1].y4), 
                    ref situation.selfPosition[1], ref situation.allyPosition[1], ref situation.adversary2Position[1]);

                validSituation = true;
            }
            catch (Exception)
            {
            }
        }
        
        return situation;
    }
    
    void ReadCSV()
    {
        try
        {
            var reader = new StreamReader("./PathDBData/dataMen.csv");
            bool isHeader = true;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (isHeader)
                {
                    isHeader = false;
                    continue; // Skip the header line
                }

                var values = line.Split(',');

                // Parse the values into the struct
                DataRow dataRow = new DataRow
                {
                    id = int.Parse(values[0]),
                    frame = int.Parse(values[1]),
                    time = float.Parse(values[2], CultureInfo.InvariantCulture.NumberFormat),
                    role1 = values[3],
                    x1 = float.Parse(values[4], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    y1 = float.Parse(values[5], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    role2 = values[6],
                    x2 = float.Parse(values[7], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    y2 = float.Parse(values[8], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    role3 = values[9],
                    x3 = float.Parse(values[10], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    y3 = float.Parse(values[11], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    role4 = values[12],
                    x4 = float.Parse(values[13], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    y4 = float.Parse(values[14], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    ballx = float.Parse(values[15], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    bally = float.Parse(values[16], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    speed1x = float.Parse(values[17], CultureInfo.InvariantCulture.NumberFormat),
                    speed1y = float.Parse(values[18], CultureInfo.InvariantCulture.NumberFormat),
                    speed2x = float.Parse(values[19], CultureInfo.InvariantCulture.NumberFormat),
                    speed2y = float.Parse(values[20], CultureInfo.InvariantCulture.NumberFormat),
                    speed3x = float.Parse(values[21], CultureInfo.InvariantCulture.NumberFormat),
                    speed3y = float.Parse(values[22], CultureInfo.InvariantCulture.NumberFormat),
                    speed4x = float.Parse(values[23], CultureInfo.InvariantCulture.NumberFormat),
                    speed4y = float.Parse(values[24], CultureInfo.InvariantCulture.NumberFormat),
                    shot = values[25],
                    targetx = float.Parse(values[26], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    targety = float.Parse(values[27], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    fromx = float.Parse(values[28], CultureInfo.InvariantCulture.NumberFormat) - 5f,
                    fromy = float.Parse(values[29], CultureInfo.InvariantCulture.NumberFormat) - 10f,
                    duration = float.Parse(values[30], CultureInfo.InvariantCulture.NumberFormat),
                    shot_full = values[31],
                    lastHit = char.Parse(values[32]),
                    rally = int.Parse(values[33])
                };

                if (dataRow.duration > 0.0f)
                {
                    dataRows.Add(dataRow);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error reading CSV file: " + e.Message);
        }
    }

    ShotClassificationByBounces UpdateShotStage(ShotClassificationByBounces stage, Vector3 collisionPosition)
    {
        switch (stage)
        {
            case ShotClassificationByBounces.NoBounce:
                return ShotClassificationByBounces.SingleBounce;
            
            case ShotClassificationByBounces.SingleBounce:
                if (Mathf.Abs(collisionPosition.z) > 9.9f)
                {
                    return ShotClassificationByBounces.BackBounce;
                }
                return ShotClassificationByBounces.SideBounce;
            
            case ShotClassificationByBounces.SideBounce:
                return ShotClassificationByBounces.SideBackBounces;
            
            case ShotClassificationByBounces.BackBounce:
                return ShotClassificationByBounces.BackSideBounces;
            
            default:
                // shouldn't be called by this current stage because it is terminal
                return stage;
        }
    }
    
    void PrecomputePaths()
    {
        const int numPaths = 10000;
        precomputedPaths = new PathArray();
        precomputedPaths.paths = new BallController.Path[numPaths];
        
        noBounceShotPathIndices = new ShotsKdTree();
        singleBounceShotPathIndices = new ShotsKdTree();
        sideShotPathIndices = new ShotsKdTree();
        backShotPathIndices = new ShotsKdTree();
        sideBackShotPathIndices = new ShotsKdTree();
        backSideShotPathIndices = new ShotsKdTree();
        
        var noBounceShotsKeyValuePairs = new List<KeyValuePair<UnityEngine.Vector4, int>>();
        var singleBounceShotsKeyValuePairs = new List<KeyValuePair<UnityEngine.Vector4, int>>();
        var sideShotsKeyValuePairs = new List<KeyValuePair<UnityEngine.Vector4, int>>();
        var backShotsKeyValuePairs = new List<KeyValuePair<UnityEngine.Vector4, int>>();
        var sideBackShotsKeyValuePairs = new List<KeyValuePair<UnityEngine.Vector4, int>>();
        var backSideShotsKeyValuePairs = new List<KeyValuePair<UnityEngine.Vector4, int>>();
        
        if (ballController != null)
        {
            for (int i = 0; i < numPaths; i++)
            {
                bool successfulSimulation = false;

                while (!successfulSimulation)
                {
                    try
                    {
                        Vector3 randomPosition = new Vector3(
                            UnityEngine.Random.Range(-4.5f, 4.5f),
                            UnityEngine.Random.Range(0.3f, 2.0f),
                            UnityEngine.Random.Range(-9.5f, 9.5f)
                        );

                        Vector3 randomVelocity = new Vector3(
                            UnityEngine.Random.Range(-6f, 6f),
                            UnityEngine.Random.Range(-0.5f, 6f),
                            -(randomPosition.z/Mathf.Abs(randomPosition.z)) * UnityEngine.Random.Range(5f, 12f)
                        );

                        Vector3 randomSpin = new Vector3(
                            UnityEngine.Random.Range(-5f, 5f),
                            UnityEngine.Random.Range(-5f, 5f),
                            UnityEngine.Random.Range(-5f, 5f)
                        );

                        precomputedPaths.paths[i] = ballController.SimulatePath(randomPosition, randomVelocity, randomSpin);
                        successfulSimulation = true;

                        ShotClassificationByBounces shotStage = ShotClassificationByBounces.NoBounce;
                        ref List<KeyValuePair<UnityEngine.Vector4, int>> shotsKdTreeValuePairs = ref noBounceShotsKeyValuePairs;
                        for (int j = 0; j < precomputedPaths.paths[i].coordinates.Count; j++)
                        {
                            Vector3 position = precomputedPaths.paths[i].coordinates[j];

                            if (precomputedPaths.paths[i].bounceIndexToCollisionCoordinates.ContainsKey(j))
                            {
                                shotStage = UpdateShotStage(shotStage, precomputedPaths.paths[i].bounceIndexToCollisionCoordinates[j]);
                                switch (shotStage)
                                {
                                    case ShotClassificationByBounces.NoBounce:
                                        shotsKdTreeValuePairs = ref noBounceShotsKeyValuePairs;
                                        break;
                                    case ShotClassificationByBounces.SingleBounce:
                                        shotsKdTreeValuePairs = ref singleBounceShotsKeyValuePairs;
                                        break;
                                    case ShotClassificationByBounces.SideBounce:
                                        shotsKdTreeValuePairs = ref sideShotsKeyValuePairs;
                                        break;
                                    case ShotClassificationByBounces.BackBounce:
                                        shotsKdTreeValuePairs = ref backShotsKeyValuePairs;
                                        break;
                                    case ShotClassificationByBounces.SideBackBounces:
                                        shotsKdTreeValuePairs = ref sideBackShotsKeyValuePairs;
                                        break;
                                    case ShotClassificationByBounces.BackSideBounces:
                                        shotsKdTreeValuePairs = ref backSideShotsKeyValuePairs;
                                        break;
                                }
                            }
                            
                            // Filter out unreachable positions for the player
                            if (position.y >= 0.2f && position.y <= 2.2f)
                            {
                                shotsKdTreeValuePairs.Add(new KeyValuePair<UnityEngine.Vector4, int>(
                                    new Vector4(precomputedPaths.paths[i].coordinates[0].x, precomputedPaths.paths[i].coordinates[0].z, position.x, position.z),
                                    i));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //successfulSimulation = true;
                        File.WriteAllText(Application.dataPath + "/../PathDBData/Debug.json", $"An exception occurred: {e.Message}. Retrying simulation for path {i}...");
                        Debug.Log($"An exception occurred: {e.Message}. Retrying simulation for path {i}...");
                    }
                }
            }
            
            noBounceShotPathIndices.Build(noBounceShotsKeyValuePairs);
            singleBounceShotPathIndices.Build(singleBounceShotsKeyValuePairs);
            sideShotPathIndices.Build(sideShotsKeyValuePairs);
            backShotPathIndices.Build(backShotsKeyValuePairs);
            sideBackShotPathIndices.Build(sideBackShotsKeyValuePairs);
            backSideShotPathIndices.Build(backSideShotsKeyValuePairs);

            // Serialize and save to JSON files
            string json = JsonUtility.ToJson(precomputedPaths, true);
            File.WriteAllText(Application.dataPath + "/../PathDBData/PrecomputedPaths.json", json);

            json = noBounceShotPathIndices.Serialize();
            File.WriteAllText(Application.dataPath + "/../PathDBData/NoBounceShotsKDTree.json", json);
            
            json = singleBounceShotPathIndices.Serialize();
            File.WriteAllText(Application.dataPath + "/../PathDBData/SingleBounceShotsKDTree.json", json);
            
            json = sideShotPathIndices.Serialize();
            File.WriteAllText(Application.dataPath + "/../PathDBData/SideShotsKDTree.json", json);
            
            json = backShotPathIndices.Serialize();
            File.WriteAllText(Application.dataPath + "/../PathDBData/BackShotsKDTree.json", json);

            json = sideBackShotPathIndices.Serialize();
            File.WriteAllText(Application.dataPath + "/../PathDBData/SideBackShotsKDTree.json", json);

            json = backSideShotPathIndices.Serialize();
            File.WriteAllText(Application.dataPath + "/../PathDBData/BackSideShotsKDTree.json", json);
        }
    }
}