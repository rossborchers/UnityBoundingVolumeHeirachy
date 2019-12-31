using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataStructures
{
    public interface ISphere
    {
        Vector3 Position
        {
            get;
        }

        float Radius
        {
            get;
        }
    }

    public class BVHParticleDataAdapter : IBVHNodeAdapter<ISphere>
    {
        private BVH<ISphere> _bvh;
        Dictionary<ISphere, BVHNode<ISphere>> gameObjectToLeafMap = new Dictionary<ISphere, BVHNode<ISphere>>();
        private event Action<ISphere> _onPositionOrSizeChanged;

        BVH<ISphere> IBVHNodeAdapter<ISphere>.BVH
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
        public void CheckMap(ISphere particle)
        {
            if (!gameObjectToLeafMap.ContainsKey(particle))
            {
                throw new Exception("missing map for shuffled child");
            }
        }

        public BVHNode<ISphere> GetLeaf(ISphere particle)
        {
            return gameObjectToLeafMap[particle];
        }

        public Vector3 GetObjectPos(ISphere particle)
        {
            return particle.Position;
        }

        public float GetRadius(ISphere particle)
        {
           return particle.Radius;
        }

        private Bounds GetBounds(ISphere particle)
        {
            Bounds bounds = new Bounds
            {
                min = new Vector3(particle.Position.x - particle.Radius, particle.Position.y - particle.Radius, particle.Position.z - particle.Radius),
                max = new Vector3(particle.Position.x + particle.Radius, particle.Position.y + particle.Radius, particle.Position.z + particle.Radius)
            };
            return bounds;
        }

        public void MapObjectToBVHLeaf(ISphere particle, BVHNode<ISphere> leaf)
        {       
            gameObjectToLeafMap[particle] = leaf;
        }

        // this allows us to be notified when an object moves, so we can adjust the BVH
        public void OnPositionOrSizeChanged(ISphere changed)
        {
            // the SSObject has changed, so notify the BVH leaf to refit for the object
            gameObjectToLeafMap[changed].RefitObjectChanged(this, changed);
        }

        public void UnmapObject(ISphere particle)
        {
            gameObjectToLeafMap.Remove(particle);
        }
    }
}