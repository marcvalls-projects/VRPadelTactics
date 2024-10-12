using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class KDTree<TKey, TValue>
{
    public delegate float DimensionSelector(TKey key, int dimension);
    public delegate float DistanceCalculator(TKey key1, TKey key2);

    [System.Serializable]
    private class KDNode
    {
        public TKey Key;
        public TValue Value;
        public KDNode Left;
        public KDNode Right;

        public KDNode(TKey key, TValue value)
        {
            Key = key;
            Value = value;
            Left = null;
            Right = null;
        }
    }
    
    [JsonProperty]
    private KDNode root;
    [JsonProperty]
    private readonly int dimensions;
    private readonly DimensionSelector dimensionSelector;
    private readonly DistanceCalculator distanceCalculator;

    public KDTree(int dimensions, DimensionSelector dimensionSelector, DistanceCalculator distanceCalculator)
    {
        if (dimensions < 1) throw new ArgumentException("Dimensions must be greater than 0.");
        this.dimensions = dimensions;
        this.dimensionSelector = dimensionSelector ?? throw new ArgumentNullException(nameof(dimensionSelector));
        this.distanceCalculator = distanceCalculator ?? throw new ArgumentNullException(nameof(distanceCalculator));
    }

    public void Build(List<KeyValuePair<TKey, TValue>> items)
    {
        root = Build(items, 0);
    }

    private KDNode Build(List<KeyValuePair<TKey, TValue>> items, int depth)
    {
        if (items == null || items.Count == 0)
            return null;

        int axis = depth % dimensions;

        // Find the median using Quickselect
        int medianIndex = items.Count / 2;
        Quickselect(items, 0, items.Count - 1, medianIndex, axis);

        KDNode node = new KDNode(items[medianIndex].Key, items[medianIndex].Value);

        // Recursively build left and right subtrees
        node.Left = Build(items.GetRange(0, medianIndex), depth + 1);
        node.Right = Build(items.GetRange(medianIndex + 1, items.Count - (medianIndex + 1)), depth + 1);

        return node;
    }

    private void Quickselect(List<KeyValuePair<TKey, TValue>> items, int left, int right, int k, int axis)
    {
        while (left < right)
        {
            int pivotIndex = Partition(items, left, right, axis);
            if (pivotIndex == k)
            {
                return;
            }
            else if (k < pivotIndex)
            {
                right = pivotIndex - 1;
            }
            else
            {
                left = pivotIndex + 1;
            }
        }
    }

    private int Partition(List<KeyValuePair<TKey, TValue>> items, int left, int right, int axis)
    {
        float pivotValue = dimensionSelector(items[right].Key, axis);
        int storeIndex = left;

        for (int i = left; i < right; i++)
        {
            if (dimensionSelector(items[i].Key, axis) < pivotValue)
            {
                Swap(items, i, storeIndex);
                storeIndex++;
            }
        }

        Swap(items, storeIndex, right);
        return storeIndex;
    }

    private void Swap(List<KeyValuePair<TKey, TValue>> items, int i, int j)
    {
        var temp = items[i];
        items[i] = items[j];
        items[j] = temp;
    }

    public TValue FindNearest(TKey target) => FindNearest(root, target, 0, null).Value;

    private KDNode FindNearest(KDNode node, TKey target, int depth, KDNode best)
    {
        if (node == null)
            return best;

        if (best == null || distanceCalculator(target, node.Key) < distanceCalculator(target, best.Key))
            best = node;

        int axis = depth % dimensions;

        KDNode nextNode = dimensionSelector(target, axis) < dimensionSelector(node.Key, axis) ? node.Left : node.Right;
        KDNode otherNode = dimensionSelector(target, axis) < dimensionSelector(node.Key, axis) ? node.Right : node.Left;

        best = FindNearest(nextNode, target, depth + 1, best);

        if (Math.Abs(dimensionSelector(target, axis) - dimensionSelector(node.Key, axis)) < distanceCalculator(target, best.Key))
            best = FindNearest(otherNode, target, depth + 1, best);

        return best;
    }
}