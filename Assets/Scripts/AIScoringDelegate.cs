using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIScoringDelegate
{
    private static readonly float[] weights = new float[] 
    {0.12715054f, 0.1525458f , -0.08701252f,  0.002568f  , -0.33270165f, -0.09744751f, 0.56407622f, -0.15250379f,  0.00622473f};
    
    private static readonly float[] scalerMeans = new float[] 
    {5.02322944f, 10.33501416f, 2.8924869f, 10.04240865f, 7.11457666f, 9.95079576f, 3.46423334f, 3.24512865f};
    
    private static readonly float[] scalerStds = new float[] 
    {2.54100181f, 6.27431431f, 0.92454804f, 6.17883368f, 0.90075378f, 6.13064176f, 1.22743165f, 1.76744962f};

    public class ScoreData
    {
        public float targetX;
        public float targetZ;
        public float opponent1X;
        public float opponent1Z;
        public float opponent2X;
        public float opponent2Z;
        public float targetDistToCenterOfHalfCourt;
        public float averageDistToOpponents;
    }

    public AIScoringDelegate()
    {
    }

    public float GetScore(ScoreData data)
    {
        float[] features =
        {
            data.targetX + 5f, data.targetZ + 10f, data.opponent1X + 5f, data.opponent1Z + 10f, data.opponent2X + 5f,
            data.opponent2Z + 10f, data.targetDistToCenterOfHalfCourt, data.averageDistToOpponents
        };
        
        for (int i = 0; i < features.Length; i++)
        {
            features[i] = (features[i] - scalerMeans[i]) / scalerStds[i];
        }
        
        // Compute the weighted sum: z = w0 + w1*x1 + w2*x2 + ... + wn*xn
        float z = weights[0]; // Bias term (w0)
        for (int i = 0; i < features.Length; i++)
        {
            z += weights[i + 1] * features[i];
        }

        // Apply the sigmoid function: 1 / (1 + exp(-z))
        return Sigmoid(z);
    }

    // Sigmoid function
    private float Sigmoid(float z)
    {
        return 1.0f / (1.0f + Mathf.Exp(-z));
    }
}
