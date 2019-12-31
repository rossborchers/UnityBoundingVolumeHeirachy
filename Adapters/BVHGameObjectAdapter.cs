using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataStructures
{
    public class BVHGameObjectAdapter : IBVHNodeAdapter<GameObject>
    {
        private BVH<GameObject> _bvh;
        Dictionary<GameObject, BVHNode<GameObject>> gameObjectToLeafMap = new Dictionary<GameObject, BVHNode<GameObject>>();
        private event Action<GameObject> _onPositionOrSizeChanged;

        BVH<GameObject> IBVHNodeAdapter<GameObject>.BVH
        {
            get
            {
                return _bvh;
            }
            set
            {
                _bvh = value;
            }
        }

        //TODO: this is not used?
        public void CheckMap(GameObject obj)
        {
            if (!gameObjectToLeafMap.ContainsKey(obj))
            {
                throw new Exception("missing map for shuffled child");
            }
        }

        public BVHNode<GameObject> GetLeaf(GameObject obj)
        {
            return gameObjectToLeafMap[obj];
        }

        public Vector3 GetObjectPos(GameObject obj)
        {
            return obj.transform.position;
        }

        public float GetRadius(GameObject obj)
        {
            Bounds encapsulatingBounds = GetBounds(obj);
            return Mathf.Max(Mathf.Max(encapsulatingBounds.extents.x, encapsulatingBounds.extents.y), encapsulatingBounds.extents.z);
        }

        private Bounds GetBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            Bounds encapsulatingBounds = new Bounds(GetObjectPos(obj), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                encapsulatingBounds.Encapsulate(renderer.bounds.max);
                encapsulatingBounds.Encapsulate(renderer.bounds.min);
            }
            return encapsulatingBounds;
        }

        public void MapObjectToBVHLeaf(GameObject obj, BVHNode<GameObject> leaf)
        {       
            gameObjectToLeafMap[obj] = leaf;
        }

        // this allows us to be notified when an object moves, so we can adjust the BVH
        public void OnPositionOrSizeChanged(GameObject changed)
        {
            // the SSObject has changed, so notify the BVH leaf to refit for the object
            gameObjectToLeafMap[changed].RefitObjectChanged(this, changed);
        }

        public void UnmapObject(GameObject obj)
        {
            gameObjectToLeafMap.Remove(obj);
        }
    }
}