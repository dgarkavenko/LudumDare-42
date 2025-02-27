﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldObjects : MonoBehaviour
{
    private static WorldObjects _instance;
    public static WorldObjects Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<WorldObjects>();
                if (_instance == null)
                {
                    var go = new GameObject("___worldObjects");
                    _instance = go.AddComponent<WorldObjects>();
                }
            }
            return _instance;
        }
    }

    [SerializeField] private ParticleSystem _cutEffect;

    public void CutEffectAt(Vector3 pos, Vector3 direction)
    {
        _cutEffect.transform.position = pos - Vector3.up / 2f;
        _cutEffect.transform.LookAt(_cutEffect.transform.position + direction + Vector3.up);
        _cutEffect.Emit(20);
    }

    private List<Tree> _trees = new List<Tree>();
    private readonly Dictionary<System.Type, List<MonoBehaviour>> _worldObjects = new Dictionary<System.Type, List<MonoBehaviour>>();
    private readonly Dictionary<System.Type, System.Func<MonoBehaviour, bool>> _additionalCheck = new Dictionary<System.Type, System.Func<MonoBehaviour, bool>>();

    public List<Tree> GetTreesInRadius(Vector3 position, float radius)
    {
        List<Tree> trees = new List<Tree>();

        foreach (var tree in _worldObjects[typeof(Tree)])
        {
            var treepos = new Vector2(tree.transform.position.x, tree.transform.position.z);
            var pos = new Vector2(position.x, position.z);
            if (Vector2.Distance(treepos, pos) < radius)
                trees.Add(tree as Tree);
        }

        return trees;
    }

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        if (_instance != this)
        {
            Destroy(this);
            return;
        }

        _trees = FindObjectsOfType<Tree>().ToList();
        _worldObjects[typeof(Tree)] = _trees.Cast<MonoBehaviour>().ToList();
        _worldObjects[typeof(Ark)] = new List<MonoBehaviour>() { FindObjectOfType<Ark>() };

        foreach (var tree in _trees)
            tree.OnDead += TreeOnDeadHandler;

        _additionalCheck[typeof(Tree)] = (monoTree) =>
        {
            var tree = monoTree as Tree;
            if (tree == null)
                return false;

            return tree.IsAlive;
        };
        _additionalCheck[typeof(TreeTrunk)] = (monoTrunk) =>
        {
            var trunk = monoTrunk as TreeTrunk;
            if (trunk == null)
                return false;

            return !trunk.IsCarring && !trunk.IsLoaded;
        };
    }

    public void Remove<T>(T behaviour) where T : MonoBehaviour
    {
        List<MonoBehaviour> list;
        if (!_worldObjects.TryGetValue(typeof(T), out list) || list.Count == 0)
            return;

        list.Remove(behaviour);
    }

    private void TreeOnDeadHandler(object sender, System.EventArgs e)
    {
        var tree = sender as Tree;
        if (tree == null)
            return;

        tree.OnDead -= TreeOnDeadHandler;
        _worldObjects[typeof(Tree)].Remove(tree);

        var trunk = tree.GetComponentInChildren<TreeTrunk>();
        if (trunk == null)
            return;

        if (!_worldObjects.ContainsKey(typeof(TreeTrunk)))
            _worldObjects.Add(typeof(TreeTrunk), new List<MonoBehaviour>());

        _worldObjects[typeof(TreeTrunk)].Add(trunk);
    }

    private void Start()
    {
    }

    public T GetFirstItem<T>() where T : MonoBehaviour
    {
        List<MonoBehaviour> list;
        if (!_worldObjects.TryGetValue(typeof(T), out list) || list.Count == 0)
            return null;

        return list.First() as T;
    }

    public GameObject GetRandomObject<T>()
    {
        List<MonoBehaviour> list;
        if (!_worldObjects.TryGetValue(typeof(T), out list) || list.Count == 0)
            return null;

        var index = Random.Range(0, list.Count);
        var maxCycles = list.Count;
        while ((list[index] == null || !_additionalCheck[typeof(T)](list[index])) && maxCycles-- >= 0)
        {
            index = (++index) % list.Count;
        }

        return maxCycles >= 0 ? list[index].gameObject : null;
    }

    public GameObject GetOneOfClosest<T>(Vector3 position, int betterCount)
    {
        List<MonoBehaviour> list;
        if (!_worldObjects.TryGetValue(typeof(T), out list) || list.Count == 0)
            return null;

        var ordered = list.Where(_ => _additionalCheck[typeof(T)](_)).OrderBy(_ => Vector3.Distance(position, _.transform.position)).ToArray();
        if (ordered.Length == 0)
            return null;

        var index = betterCount >= ordered.Length - 1 ? ordered.Length : betterCount + 1;
        index = Random.Range(0, index);
        return ordered[index].gameObject;
    }

    public GameObject GetClosestObject<T>(Vector3 position)
    {
        List<MonoBehaviour> list;
        if (!_worldObjects.TryGetValue(typeof(T), out list) || list.Count == 0)
            return null;

        var minDistance = float.MaxValue;
        MonoBehaviour closestObject = null;
        for (int i = 0; i < list.Count; i++)
        {
            if (!_additionalCheck[typeof(T)](list[i]))
                continue;
            var currentDistance = Vector3.Distance(position, list[i].transform.position);
            if (currentDistance >= minDistance)
                continue;

            minDistance = currentDistance;
            closestObject = list[i];
        }

        return closestObject == null ? null : closestObject.gameObject;
    }

    public void RemoveEverythingLoweerThan(float positionY)
    {
        foreach (var _list in _worldObjects)
        {
            for (int i = _list.Value.Count - 1; i > -1; i--)
            {
                if (_list.Value[i] == null || _list.Value[i].transform.position.y < positionY)
                    _list.Value.RemoveAt(i);
            }
        }
    }
}
