// Copyright(C) David W. Jeske, 2014, and released to the public domain. 
//
// Dynamic BVH (Bounding Volume Hierarchy) using incremental refit and tree-rotations
//
// initial BVH build based on: Bounding Volume Hierarchies (BVH) – A brief tutorial on what they are and how to implement them
//              http://www.3dmuve.com/3dmblog/?p=182
//
// Dynamic Updates based on: "Fast, Effective BVH Updates for Animated Scenes" (Kopta, Ize, Spjut, Brunvand, David, Kensler)
//              https://github.com/jeske/SimpleScene/blob/master/SimpleScene/Util/ssBVH/docs/BVH_fast_effective_updates_for_animated_scenes.pdf
//
// see also:  Space Partitioning: Octree vs. BVH
//            http://thomasdiewald.com/blog/?p=1488

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// TODO: handle merge/split when LEAF_OBJ_MAX > 1 and objects move
// TODO: add sphere traversal

namespace DataStructures
{
	public enum Axis
	{
		X, Y, Z,
	}

    public delegate bool NodeTraversalTest(Bounds box);

    public class BVHHelper
    {
        public static NodeTraversalTest RadialNodeTraversalTest(Vector3 center, float radius)
        {
            return (Bounds bounds) =>
            {
                //find the closest point inside the bounds
                //Then get the difference between the point and the circle center
                float deltaX = center.x - Mathf.Max(bounds.min.x, Mathf.Min(center.x, bounds.max.x));
                float deltaY = center.y - Mathf.Max(bounds.min.y, Mathf.Min(center.y, bounds.max.y));
                float deltaZ = center.z - Mathf.Max(bounds.min.z, Mathf.Min(center.z, bounds.max.z));

                //sqr magnitude < sqr radius = inside bounds!
                return (deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ) < (radius * radius);
            };
        }
    }

    public class BVH<T>
    {
        private Material _debugRenderMaterial = null;

        public BVHNode<T> rootBVH;
        public IBVHNodeAdapter<T> nAda;
        public readonly int LEAF_OBJ_MAX;
        public int nodeCount = 0;
        public int maxDepth = 0;

        public HashSet<BVHNode<T>> refitNodes = new HashSet<BVHNode<T>>(); 

        // internal functional traversal...
        private void _traverse(BVHNode<T> curNode, NodeTraversalTest hitTest, List<BVHNode<T>> hitlist)
		{
			if (curNode == null) { return; }
			if (hitTest(curNode.Box))
			{
				hitlist.Add(curNode);
				_traverse(curNode.Left, hitTest, hitlist);
				_traverse(curNode.Right, hitTest, hitlist);
			}
		}

		// public interface to traversal..
		public List<BVHNode<T>> Traverse(NodeTraversalTest hitTest)
		{
			var hits = new List<BVHNode<T>>();
			this._traverse(rootBVH, hitTest, hits);
			return hits;
		}

	    /*	
        public List<BVHNode<T> Traverse(Ray ray)
		{
			float tnear = 0f, tfar = 0f;

			return Traverse(box => OpenTKHelper.intersectRayAABox1(ray, box, ref tnear, ref tfar));
		}
		public List<BVHNode<T>> Traverse(Bounds volume)
		{
			return Traverse(box => box.IntersectsAABB(volume));
		}
		*/

		/// <summary>
		/// Call this to batch-optimize any object-changes notified through 
		/// ssBVHNode.refit_ObjectChanged(..). For example, in a game-loop, 
		/// call this once per frame.
		/// </summary>
		public void Optimize()
		{
			if (LEAF_OBJ_MAX != 1)
			{
				throw new Exception("In order to use optimize, you must set LEAF_OBJ_MAX=1");
			}

			while (refitNodes.Count > 0)
			{
				int maxdepth = refitNodes.Max(n => n.Depth);

				var sweepNodes = refitNodes.Where(n => n.Depth == maxdepth).ToList();
				sweepNodes.ForEach(n => refitNodes.Remove(n));

				sweepNodes.ForEach(n => n.TryRotate(this));
			}
		}

		public void Add(T newOb)
		{
			Bounds box = BoundsFromSphere(nAda.GetObjectPos(newOb), nAda.GetRadius(newOb));
			float boxSAH = BVHNode<T>.SA(ref box);
			rootBVH.Add(nAda, newOb, ref box, boxSAH);
		}

        /// <summary>
        /// Call this when you wish to update an object. This does not update straight away, but marks it for update when Optimize() is called
        /// </summary>
        /// <param name="toUpdate"></param>
        public void MarkForUpdate(T toUpdate)
        {
            nAda.OnPositionOrSizeChanged(toUpdate);
        }

		//Modified from https://github.com/jeske/SimpleScene/blob/master/SimpleScene/Core/SSAABB.cs
		public static Bounds BoundsFromSphere(Vector3 pos, float radius)
		{
			Bounds bounds = new Bounds
			{
				min = new Vector3(pos.x - radius, pos.y - radius, pos.z - radius),
				max = new Vector3(pos.x + radius, pos.y + radius, pos.z + radius)
			};
			return bounds;
		}

		public void Remove(T newObj)
		{
			var leaf = nAda.GetLeaf(newObj);
			leaf.Remove(nAda, newObj);
		}

		public int CountBVHNodes()
		{
			return rootBVH.CountBVHNodes();
		}

		/// <summary>
		/// initializes a BVH with a given nodeAdaptor, and object list.
		/// </summary>
		/// <param name="nodeAdaptor"></param>
		/// <param name="objects"></param>
		/// <param name="LEAF_OBJ_MAX">WARNING! currently this must be 1 to use dynamic BVH updates</param>
		public BVH(IBVHNodeAdapter<T> nodeAdaptor, List<T> objects, int LEAF_OBJ_MAX = 1)
		{
			this.LEAF_OBJ_MAX = LEAF_OBJ_MAX;
			nodeAdaptor.BVH = this;
			this.nAda = nodeAdaptor;

			if (objects.Count > 0)
			{
				rootBVH = new BVHNode<T>(this, objects);
			}
			else
			{
				rootBVH = new BVHNode<T>(this);
				rootBVH.GObjects = new List<T>(); // it's a leaf, so give it an empty object list
			}
		}

        //Bounds rendering ( for debugging )
        //=======\/=======\/=======\/=======\/=======\/=======\/=======\/=======\/=======\/=======\/=======\/=======\/

        private static readonly List<Vector3> vertices = new List<Vector3>
        {
            new Vector3 (-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3 (-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
        };

        private static readonly int[] indices =
        {
            0, 1, 1, 2, 2, 3, 3, 0, // face1
            4, 5, 5, 6, 6, 7, 7, 4, // face2
            0, 4, 1, 5, 2, 6, 3, 7  // interconnects
        };

        public void GetAllNodeMatriciesRecursive(BVHNode<T> n, ref List<Matrix4x4> matricies, int depth)
        {
            //rotate not required since AABB
            Matrix4x4 matrix = Matrix4x4.Translate(n.Box.center) * Matrix4x4.Scale(n.Box.size);
            matricies.Add(matrix);

            if (n.Right != null) GetAllNodeMatriciesRecursive(n.Right, ref matricies, depth + 1);
            if (n.Left != null) GetAllNodeMatriciesRecursive(n.Left, ref matricies, depth + 1);
        }

        public void RenderDebug()
        {
            if (!SystemInfo.supportsInstancing)
            {
                Debug.LogError("[BVH] Cannot render BVH. Mesh instancing not supported by system");
            }
            else
            {
                List<Matrix4x4> matricies = new List<Matrix4x4>();

                GetAllNodeMatriciesRecursive(rootBVH, ref matricies, 0);

                Mesh mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetIndices(indices, MeshTopology.Lines, 0);

                if(_debugRenderMaterial == null)
                {
                    _debugRenderMaterial = new Material(Shader.Find("Standard"))
                    {
                        enableInstancing = true
                    };
                }
                Graphics.DrawMeshInstanced(mesh, 0, _debugRenderMaterial, matricies);
            }
        }
    }
}
