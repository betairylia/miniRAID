using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using miniRAID.Backend;
using UnityEngine;

namespace miniRAID.Buff
{
    public class GridEffectComponent : MonoBehaviour, IStateRenderer
    {
        public GameObject gridFxPrefab;
        private HashSet<Vector3Int> shape = new();
        private readonly Dictionary<Vector3Int, GameObject> renderedGrids = new();
        // Report only cells whose current presentation is actually enabled. This is not an AI/hazard prediction.
        public IEnumerable<Vector3Int> VisibleCells => renderedGrids
            .Where(pair => pair.Value != null && pair.Value.activeInHierarchy &&
                pair.Value.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy))
            .Select(pair => pair.Key);
        private HashSet<Vector3Int> incomingShape = new();
        private HashSet<Vector3Int> buffer = new();

        private void Awake()
        {
            shape = new();
        }

        public GameObject AddGrid(Vector3 position)
        {
            return Instantiate(
                gridFxPrefab,
                new Vector3(position.x, position.y, position.z),
                Quaternion.identity,
                transform
            );
        }

        public void SetShape(HashSet<Vector3Int> shape)
        {
            incomingShape = shape ?? new HashSet<Vector3Int>();
            Refresh();
        }

        public void Refresh()
        {
            buffer.Clear();
            buffer.UnionWith(incomingShape);
            buffer.ExceptWith(shape);
            foreach (var p in buffer)
            {
                renderedGrids[p] = AddGrid(Globals.backend.GridToBackendFloorPos(p));
            }
            
            shape.UnionWith(buffer);
        }

        public void Destroy()
        {
            GameObject.Destroy(gameObject);
        }
    }
}
