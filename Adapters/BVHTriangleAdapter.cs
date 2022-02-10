using UnityEngine;
using System;
using System.Collections.Generic;


namespace DataStructures {
    public struct Triangle {
        public Triangle(Vector3 _a, Vector3 _b, Vector3 _c) {
            a = _a;
            b = _b;
            c = _c;
        }
        public Vector3 a, b, c;
    }

    public class BVHRayHitTest {
        public BVHRayHitTest(Ray _ray) {
            ray = _ray;
        }
        public bool NodeTraversalTest(Bounds box) => box.IntersectRay(ray);
        public Ray ray { get; set; }
    }

    public class BVHTriangleAdapter : IBVHNodeAdapter<Triangle> {
        private BVH<Triangle> _bvh;
        private Dictionary<Triangle, BVHNode<Triangle>> triangeToLeafMap = new Dictionary<Triangle, BVHNode<Triangle>>();

    
        BVH<Triangle> IBVHNodeAdapter<Triangle>.BVH { get => _bvh; set { _bvh = value; }}

        public void CheckMap(Triangle triangle) {
            if (!triangeToLeafMap.ContainsKey(triangle)) {
                throw new Exception("missing map for shuffled child!");
            }
        }

        public BVHNode<Triangle> GetLeaf(Triangle triangle) => triangeToLeafMap[triangle];
        public Vector3 GetObjectPos(Triangle triangle) {
            float x = (triangle.a.x + triangle.b.x + triangle.c.x) / 3.0f;
            float y = (triangle.a.y + triangle.b.y + triangle.c.y) / 3.0f;
            float z = (triangle.a.z + triangle.b.z + triangle.c.z) / 3.0f;
            return new Vector3(x, y, z);
        }

        public float GetRadius(Triangle triangle) {
            Vector3 centroid = new Vector3(
                (triangle.a.x + triangle.b.x + triangle.c.x) / 3.0f,
                (triangle.a.y + triangle.b.y + triangle.c.y) / 3.0f,
                (triangle.a.z + triangle.b.z + triangle.c.z) / 3.0f
            );
            return Mathf.Max(
                Mathf.Max(Vector3.Distance(centroid, triangle.a), Vector3.Distance(centroid, triangle.b),
                Vector3.Distance(centroid, triangle.c))); 
        }
        
        public void MapObjectToBVHLeaf(Triangle triangle, BVHNode<Triangle> node) {
            triangeToLeafMap[triangle] = node;
        }

        public void OnPositionOrSizeChanged(Triangle changed)
        {
            // the SSObject has changed, so notify the BVH leaf to refit for the object
            triangeToLeafMap[changed].RefitObjectChanged(this, changed);
        }

        public void UnmapObject(Triangle triangle) {
            triangeToLeafMap.Remove(triangle);
        }
    }
}