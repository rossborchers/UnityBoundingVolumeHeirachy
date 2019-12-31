// Copyright(C) David W. Jeske, 2014, and released to the public domain. 
//
// Dynamic BVH (Bounding Volume Hierarchy) using incremental refit and tree-rotations
//
// initial BVH build based on: Bounding Volume Hierarchies (BVH) – A brief tutorial on what they are and how to implement them
//              http://www.3dmuve.com/3dmblog/?p=182
//
// Dynamic Updates based on: "Fast, Effective BVH Updates for Animated Scenes" (Kopta, Ize, Spjut, Brunvand, David, Kensler)
//              http://www.cs.utah.edu/~thiago/papers/rotations.pdf
//
// see also:  Space Partitioning: Octree vs. BVH
//            http://thomasdiewald.com/blog/?p=1488
//
// TODO: pick the best axis to split based on SAH, instead of the biggest
// TODO: Switch SAH comparisons to use (SAH(A) * itemCount(A)) currently it just uses SAH(A)
// TODO: when inserting, compare parent node SAH(A) * itemCount to sum of children, to see if it is better to not split at all
// TODO: implement node merge/split, to handle updates when LEAF_OBJ_MAX > 1
// 
// TODO: implement SBVH spacial splits
//        http://www.nvidia.com/docs/IO/77714/sbvh.pdf

//Note there are a few Expanding methods in the original implementation
//void ExpandToFit(SSAABB b)
//void ExpandBy(SSAABB b)
//SSAABB ExpandedBy(SSAABB b)
//They all do the same thing but some return coppies, some assing to the bounds directly, or have different access modifiers
//They have all been turned into Encapsulate and refactored appropriatley

using System;
using System.Collections.Generic;
using System.Linq;

using System.Diagnostics;
using UnityEngine;

namespace DataStructures
{
	public class BVHNode<T>
	{
		public Bounds Box;

		public BVHNode<T> Parent;
		public BVHNode<T> Left;
		public BVHNode<T> Right;

		public int Depth;
		public int NodeNumber; // for debugging

		public List<T> GObjects;  // only populated in leaf nodes

		public override string ToString()
		{
			return string.Format("BVHNode<{0}>:{1}", typeof(T), this.NodeNumber);
		}

		private Axis PickSplitAxis()
		{
			float axis_x = Box.max.x - Box.max.x;
			float axis_y = Box.max.y - Box.max.y;
			float axis_z = Box.max.z - Box.max.z;

			// return the biggest axis
			if (axis_x > axis_y)
			{
				if (axis_x > axis_z)
				{
					return Axis.X;
				}
				else
				{
					return Axis.Z;
				}
			}
			else
			{
				if (axis_y > axis_z)
				{
					return Axis.Y;
				}
				else
				{
					return Axis.Z;
				}
			}
		}
		public bool IsLeaf
		{
			get
			{
				bool isLeaf = (this.GObjects != null);
				// if we're a leaf, then both left and right should be null..
				if (isLeaf && ((Right != null) || (Left != null)))
				{
					throw new Exception("BVH Leaf has objects and left/right pointers!");
				}
				return isLeaf;

			}
		}

		private Axis NextAxis(Axis cur)
		{
			switch (cur)
			{
				case Axis.X: return Axis.Y;
				case Axis.Y: return Axis.Z;
				case Axis.Z: return Axis.X;
				default: throw new NotSupportedException();
			}
		}

		public void RefitObjectChanged(IBVHNodeAdapter<T> nAda, T obj)
		{
			if (GObjects == null)
			{
				throw new Exception("dangling leaf!");
			}
			if (RefitVolume(nAda))
			{
				// add our parent to the optimize list...
				if (Parent != null)
				{
					nAda.BVH.refitNodes.Add(Parent);

					// you can force an optimize every time something moves, but it's not very efficient
					// instead we do this per-frame after a bunch of updates.
					// nAda.BVH.Optimize();                    
				}
			}
		}

		private void ExpandVolume(IBVHNodeAdapter<T> nAda, Vector3 objectpos, float radius)
		{
			bool expanded = false;

			// test min X and max X against the current bounding volume
			if ((objectpos.x - radius) < Box.min.x)
			{
				Box.min = new Vector3(objectpos.x - radius, Box.min.y, Box.min.z);
				expanded = true;
			}
			if ((objectpos.x + radius) > Box.max.x)
			{
				Box.max = new Vector3(objectpos.x + radius, Box.max.y, Box.max.z);
				expanded = true;
			}
			// test min Y and max Y against the current bounding volume
			if ((objectpos.y - radius) < Box.min.y)
			{
				Box.min = new Vector3(Box.min.x, (objectpos.y - radius), Box.min.z);
				expanded = true;
			}
			if ((objectpos.y + radius) > Box.max.y)
			{
				Box.max = new Vector3(Box.max.x, (objectpos.y + radius), Box.max.z);
				expanded = true;
			}
			// test min Z and max Z against the current bounding volume
			if ((objectpos.z - radius) < Box.min.z)
			{
				Box.min = new Vector3(Box.min.x, Box.min.y, (objectpos.z - radius));
				expanded = true;
			}
			if ((objectpos.z + radius) > Box.max.z)
			{
				Box.max = new Vector3(Box.max.x, Box.max.y, (objectpos.z + radius));
				expanded = true;
			}

			if (expanded && Parent != null)
			{
				Parent.ChildExpanded(nAda, this);
			}
		}

		private void AssignVolume(Vector3 objectpos, float radius)
		{		
			Box.min = new Vector3(objectpos.x - radius, objectpos.y - radius, objectpos.z - radius);
			Box.max = new Vector3(objectpos.x + radius, objectpos.y + radius, objectpos.z + radius);
		}

		internal void ComputeVolume(IBVHNodeAdapter<T> nAda)
		{
			AssignVolume(nAda.GetObjectPos(GObjects[0]), nAda.GetRadius(GObjects[0]));
			for (int i = 1; i < GObjects.Count; i++)
			{
				ExpandVolume(nAda, nAda.GetObjectPos(GObjects[i]), nAda.GetRadius(GObjects[i]));
			}
		}

		internal bool RefitVolume(IBVHNodeAdapter<T> nAda)
		{
			if (GObjects.Count == 0)
			{
				// TODO: fix this... we should never get called in this case...
				throw new NotImplementedException();
			}

			Bounds oldbox = Box;

			ComputeVolume(nAda);
			if (!Box.Equals(oldbox))
			{
				if (Parent != null) Parent.ChildRefit(nAda);
				return true;
			}
			else
			{
				return false;
			}
		}

		internal static float SA(Bounds box)
		{
			float x_size = box.max.x - box.min.x;
			float y_size = box.max.y - box.min.y;
			float z_size = box.max.z - box.min.z;

			return 2.0f * ((x_size * y_size) + (x_size * z_size) + (y_size * z_size));

		}
		internal static float SA(ref Bounds box)
		{
			float x_size = box.max.x - box.min.x;
			float y_size = box.max.y - box.min.y;
			float z_size = box.max.z - box.min.z;

			return 2.0f * ((x_size * y_size) + (x_size * z_size) + (y_size * z_size));

		}
		internal static float SA(BVHNode<T> node)
		{
			float x_size = node.Box.max.x - node.Box.min.x;
			float y_size = node.Box.max.y - node.Box.min.y;
			float z_size = node.Box.max.z - node.Box.min.z;

			return 2.0f * ((x_size * y_size) + (x_size * z_size) + (y_size * z_size));
		}
		internal static float SA(IBVHNodeAdapter<T> nAda, T obj)
		{
			float radius = nAda.GetRadius(obj);

			float size = radius * 2;
			return 6.0f * (size * size);
		}

		internal static Bounds AABBofPair(BVHNode<T> nodea, BVHNode<T> nodeb)
		{
			Bounds box = nodea.Box;
			box.Encapsulate(nodeb.Box);
			return box;
		}

		internal float SAofPair(BVHNode<T> nodea, BVHNode<T> nodeb)
		{
			Bounds box = nodea.Box;
			box.Encapsulate(nodeb.Box);
			return SA(ref box);
		}
		internal float SAofPair(Bounds boxa, Bounds boxb)
		{
			Bounds pairbox = boxa;
			pairbox.Encapsulate(boxb);
			return SA(ref pairbox);
		}
		internal static Bounds AABBofOBJ(IBVHNodeAdapter<T> nAda, T obj)
		{
			float radius = nAda.GetRadius(obj);
			Bounds box = new Bounds
			{
				min = new Vector3(-radius, -radius, -radius),
				max = new Vector3(radius, radius, radius)
			};
			return box;
		}

		internal float SAofList(IBVHNodeAdapter<T> nAda, List<T> list)
		{
			var box = AABBofOBJ(nAda, list[0]);

			list.ToList<T>().GetRange(1, list.Count - 1).ForEach(obj =>
			{
				var newbox = AABBofOBJ(nAda, obj);
				box.Encapsulate(newbox);
			});
			return SA(box);
		}

		// The list of all candidate rotations, from "Fast, Effective BVH Updates for Animated Scenes", Figure 1.
		internal enum Rot
		{
			NONE, L_RL, L_RR, R_LL, R_LR, LL_RR, LL_RL,
		}

		internal class RotOpt : IComparable<RotOpt>
		{  // rotation option
			public float SAH;
			public Rot rot;
			internal RotOpt(float SAH, Rot rot)
			{
				this.SAH = SAH;
				this.rot = rot;
			}
			public int CompareTo(RotOpt other)
			{
				return SAH.CompareTo(other.SAH);
			}
		}

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private static List<Rot> EachRot
		{
			get
			{
				return new List<Rot>((Rot[])Enum.GetValues(typeof(Rot)));
			}
		}

		/// <summary>
		/// tryRotate looks at all candidate rotations, and executes the rotation with the best resulting SAH (if any)
		/// </summary>
		/// <param name="bvh"></param>
		internal void TryRotate(BVH<T> bvh)
		{
			IBVHNodeAdapter<T> nAda = bvh.nAda;

			// if we are not a grandparent, then we can't rotate, so queue our parent and bail out
			if (Left.IsLeaf && Right.IsLeaf)
			{
				if (Parent != null)
				{
					bvh.refitNodes.Add(Parent);
					return;
				}
			}

			// for each rotation, check that there are grandchildren as necessary (aka not a leaf)
			// then compute total SAH cost of our branches after the rotation.

			float mySA = SA(Left) + SA(Right);

			RotOpt bestRot = EachRot.Min((rot) =>
			{
				switch (rot)
				{
					case Rot.NONE: return new RotOpt(mySA, Rot.NONE);
					// child to grandchild rotations
					case Rot.L_RL:
						if (Right.IsLeaf) return new RotOpt(float.MaxValue, Rot.NONE);
						else return new RotOpt(SA(Right.Left) + SA(AABBofPair(Left, Right.Right)), rot);
					case Rot.L_RR:
						if (Right.IsLeaf) return new RotOpt(float.MaxValue, Rot.NONE);
						else return new RotOpt(SA(Right.Right) + SA(AABBofPair(Left, Right.Left)), rot);
					case Rot.R_LL:
						if (Left.IsLeaf) return new RotOpt(float.MaxValue, Rot.NONE);
						else return new RotOpt(SA(AABBofPair(Right, Left.Right)) + SA(Left.Left), rot);
					case Rot.R_LR:
						if (Left.IsLeaf) return new RotOpt(float.MaxValue, Rot.NONE);
						else return new RotOpt(SA(AABBofPair(Right, Left.Left)) + SA(Left.Right), rot);
					// grandchild to grandchild rotations
					case Rot.LL_RR:
						if (Left.IsLeaf || Right.IsLeaf) return new RotOpt(float.MaxValue, Rot.NONE);
						else return new RotOpt(SA(AABBofPair(Right.Right, Left.Right)) + SA(AABBofPair(Right.Left, Left.Left)), rot);
					case Rot.LL_RL:
						if (Left.IsLeaf || Right.IsLeaf) return new RotOpt(float.MaxValue, Rot.NONE);
						else return new RotOpt(SA(AABBofPair(Right.Left, Left.Right)) + SA(AABBofPair(Left.Left, Right.Right)), rot);
					// unknown...
					default: throw new NotImplementedException("missing implementation for BVH Rotation SAH Computation .. " + rot.ToString());
				}
			});

			// perform the best rotation...            
			if (bestRot.rot != Rot.NONE)
			{
				// if the best rotation is no-rotation... we check our parents anyhow..                
				if (Parent != null)
				{
					// but only do it some random percentage of the time.
					if ((DateTime.Now.Ticks % 100) < 2)
					{
						bvh.refitNodes.Add(Parent);
					}
				}
			}
			else
			{

				if (Parent != null) { bvh.refitNodes.Add(Parent); }

				if (((mySA - bestRot.SAH) / mySA) < 0.3f)
				{
					return; // the benefit is not worth the cost
				}
				Console.WriteLine("BVH swap {0} from {1} to {2}", bestRot.rot.ToString(), mySA, bestRot.SAH);

				// in order to swap we need to:
				//  1. swap the node locations
				//  2. update the depth (if child-to-grandchild)
				//  3. update the parent pointers
				//  4. refit the boundary box
				BVHNode<T> swap = null;
				switch (bestRot.rot)
				{
					case Rot.NONE: break;
					// child to grandchild rotations
					case Rot.L_RL: swap = Left; Left = Right.Left; Left.Parent = this; Right.Left = swap; swap.Parent = Right; Right.ChildRefit(nAda, propagate: false); break;
					case Rot.L_RR: swap = Left; Left = Right.Right; Left.Parent = this; Right.Right = swap; swap.Parent = Right; Right.ChildRefit(nAda, propagate: false); break;
					case Rot.R_LL: swap = Right; Right = Left.Left; Right.Parent = this; Left.Left = swap; swap.Parent = Left; Left.ChildRefit(nAda, propagate: false); break;
					case Rot.R_LR: swap = Right; Right = Left.Right; Right.Parent = this; Left.Right = swap; swap.Parent = Left; Left.ChildRefit(nAda, propagate: false); break;

					// grandchild to grandchild rotations
					case Rot.LL_RR: swap = Left.Left; Left.Left = Right.Right; Right.Right = swap; Left.Left.Parent = Left; swap.Parent = Right; Left.ChildRefit(nAda, propagate: false); Right.ChildRefit(nAda, propagate: false); break;
					case Rot.LL_RL: swap = Left.Left; Left.Left = Right.Left; Right.Left = swap; Left.Left.Parent = Left; swap.Parent = Right; Left.ChildRefit(nAda, propagate: false); Right.ChildRefit(nAda, propagate: false); break;

					// unknown...
					default: throw new NotImplementedException("missing implementation for BVH Rotation .. " + bestRot.rot.ToString());
				}

				// fix the depths if necessary....
				switch (bestRot.rot)
				{
					case Rot.L_RL:
					case Rot.L_RR:
					case Rot.R_LL:
					case Rot.R_LR:
						this.SetDepth(nAda, this.Depth);
						break;
				}
			}
		}

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private static List<Axis> EachAxis
		{
			get
			{
				return new List<Axis>((Axis[])Enum.GetValues(typeof(Axis)));
			}
		}

		internal class SplitAxisOpt<GO> : IComparable<SplitAxisOpt<GO>>
		{  // split Axis option
			public float SAH;
			public Axis axis;
			public List<GO> left, right;
			internal SplitAxisOpt(float SAH, Axis axis, List<GO> left, List<GO> right)
			{
				this.SAH = SAH;
				this.axis = axis;
				this.left = left;
				this.right = right;
			}
			public int CompareTo(SplitAxisOpt<GO> other)
			{
				return SAH.CompareTo(other.SAH);
			}
		}

		internal void SplitNode(IBVHNodeAdapter<T> nAda)
		{
			// second, decide which axis to split on, and sort..
			List<T> splitlist = GObjects;
			splitlist.ForEach(o => nAda.UnmapObject(o));
			int center = (int)(splitlist.Count / 2); // find the center object

			SplitAxisOpt<T> bestSplit = EachAxis.Min((axis) =>
			{
				var orderedlist = new List<T>(splitlist);
				switch (axis)
				{
					case Axis.X:
						orderedlist.Sort(delegate (T go1, T go2) { return nAda.GetObjectPos(go1).x.CompareTo(nAda.GetObjectPos(go2).x); });
						break;
					case Axis.Y:
						orderedlist.Sort(delegate (T go1, T go2) { return nAda.GetObjectPos(go1).y.CompareTo(nAda.GetObjectPos(go2).y); });
						break;
					case Axis.Z:
						orderedlist.Sort(delegate (T go1, T go2) { return nAda.GetObjectPos(go1).z.CompareTo(nAda.GetObjectPos(go2).z); });
						break;
					default:
						throw new NotImplementedException("unknown split axis: " + axis.ToString());
				}

				var left_s = orderedlist.GetRange(0, center);
				var right_s = orderedlist.GetRange(center, splitlist.Count - center);

				float SAH = SAofList(nAda, left_s) * left_s.Count + SAofList(nAda, right_s) * right_s.Count;
				return new SplitAxisOpt<T>(SAH, axis, left_s, right_s);
			});

			// perform the split
			GObjects = null;
			this.Left = new BVHNode<T>(nAda.BVH, this, bestSplit.left, bestSplit.axis, this.Depth + 1); // Split the Hierarchy to the left
			this.Right = new BVHNode<T>(nAda.BVH, this, bestSplit.right, bestSplit.axis, this.Depth + 1); // Split the Hierarchy to the right                                
		}

		internal void SplitIfNecessary(IBVHNodeAdapter<T> nAda)
		{
			if (GObjects.Count > nAda.BVH.LEAF_OBJ_MAX)
			{
				SplitNode(nAda);
			}
		}

		internal void Add(IBVHNodeAdapter<T> nAda, T newOb, ref Bounds newObBox, float newObSAH)
		{
			Add(nAda, this, newOb, ref newObBox, newObSAH);
		}

		internal static void AddObjectPushdown(IBVHNodeAdapter<T> nAda, BVHNode<T> curNode, T newOb)
		{
			var left = curNode.Left;
			var right = curNode.Right;

			// merge and pushdown left and right as a new node..
			var mergedSubnode = new BVHNode<T>(nAda.BVH);
			mergedSubnode.Left = left;
			mergedSubnode.Right = right;
			mergedSubnode.Parent = curNode;
			mergedSubnode.GObjects = null; // we need to be an interior node... so null out our object list..
			left.Parent = mergedSubnode;
			right.Parent = mergedSubnode;
			mergedSubnode.ChildRefit(nAda, propagate: false);

			// make new subnode for obj
			var newSubnode = new BVHNode<T>(nAda.BVH);
			newSubnode.Parent = curNode;
			newSubnode.GObjects = new List<T> { newOb };
			nAda.MapObjectToBVHLeaf(newOb, newSubnode);
			newSubnode.ComputeVolume(nAda);

			// make assignments..
			curNode.Left = mergedSubnode;
			curNode.Right = newSubnode;
			curNode.SetDepth(nAda, curNode.Depth); // propagate new depths to our children.
			curNode.ChildRefit(nAda);
		}

		internal static void Add(IBVHNodeAdapter<T> nAda, BVHNode<T> curNode, T newOb, ref Bounds newObBox, float newObSAH)
		{
			// 1. first we traverse the node looking for the best leaf
			while (curNode.GObjects == null)
			{
				// find the best way to add this object.. 3 options..
				// 1. send to left node  (L+N,R)
				// 2. send to right node (L,R+N)
				// 3. merge and pushdown left-and-right node (L+R,N)

				var left = curNode.Left;
				var right = curNode.Right;

				float leftSAH = SA(left);
				float rightSAH = SA(right);

				//Create new bounds to avoid modifying originals when using encapsulate
				Bounds leftExpanded = new Bounds
				{
					min = left.Box.min,
					max = left.Box.max
				};

				Bounds rightExpanded = new Bounds
				{
					min = right.Box.min,
					max = right.Box.max
				};

				leftExpanded.Encapsulate(newObBox);
				rightExpanded.Encapsulate(newObBox);

				float sendLeftSAH = rightSAH + SA(leftExpanded);    // (L+N,R)
				float sendRightSAH = leftSAH + SA(rightExpanded);   // (L,R+N)
				float mergedLeftAndRightSAH = SA(AABBofPair(left, right)) + newObSAH; // (L+R,N)

				// Doing a merge-and-pushdown can be expensive, so we only do it if it's notably better
				const float MERGE_DISCOUNT = 0.3f;

				if (mergedLeftAndRightSAH < (Math.Min(sendLeftSAH, sendRightSAH)) * MERGE_DISCOUNT)
				{
					AddObjectPushdown(nAda, curNode, newOb);
					return;
				}
				else
				{
					if (sendLeftSAH < sendRightSAH)
					{
						curNode = left;
					}
					else
					{
						curNode = right;
					}
				}
			}

			// 2. then we add the object and map it to our leaf
			curNode.GObjects.Add(newOb);
			nAda.MapObjectToBVHLeaf(newOb, curNode);
			curNode.RefitVolume(nAda);
			// split if necessary...
			curNode.SplitIfNecessary(nAda);
		}

		internal int CountBVHNodes()
		{
			if (GObjects != null)
			{
				return 1;
			}
			else
			{
				return Left.CountBVHNodes() + Right.CountBVHNodes();
			}
		}

		internal void Remove(IBVHNodeAdapter<T> nAda, T newOb)
		{
			if (GObjects == null) { throw new Exception("removeObject() called on nonLeaf!"); }

			nAda.UnmapObject(newOb);
			GObjects.Remove(newOb);
			if (GObjects.Count > 0)
			{
				RefitVolume(nAda);
			}
			else
			{
				// our leaf is empty, so collapse it if we are not the root...
				if (Parent != null)
				{
					GObjects = null;
					Parent.RemoveLeaf(nAda, this);
					Parent = null;
				}
			}
		}

		void SetDepth(IBVHNodeAdapter<T> nAda, int newdepth)
		{
			this.Depth = newdepth;
			if (newdepth > nAda.BVH.maxDepth)
			{
				nAda.BVH.maxDepth = newdepth;
			}
			if (GObjects == null)
			{
				Left.SetDepth(nAda, newdepth + 1);
				Right.SetDepth(nAda, newdepth + 1);
			}
		}

		internal void RemoveLeaf(IBVHNodeAdapter<T> nAda, BVHNode<T> removeLeaf)
		{
			if (Left == null || Right == null) { throw new Exception("bad intermediate node"); }
			BVHNode<T> keepLeaf;

			if (removeLeaf == Left)
			{
				keepLeaf = Right;
			}
			else if (removeLeaf == Right)
			{
				keepLeaf = Left;
			}
			else
			{
				throw new Exception("removeLeaf doesn't match any leaf!");
			}

			// "become" the leaf we are keeping.
			Box = keepLeaf.Box;
			Left = keepLeaf.Left; Right = keepLeaf.Right; GObjects = keepLeaf.GObjects;
			// clear the leaf..
			// keepLeaf.left = null; keepLeaf.right = null; keepLeaf.gobjects = null; keepLeaf.parent = null; 

			if (GObjects == null)
			{
				Left.Parent = this; Right.Parent = this;  // reassign child parents..
				this.SetDepth(nAda, this.Depth); // this reassigns depth for our children
			}
			else
			{
				// map the objects we adopted to us...                                                
				GObjects.ForEach(o => { nAda.MapObjectToBVHLeaf(o, this); });
			}

			// propagate our new volume..
			if (Parent != null)
			{
				Parent.ChildRefit(nAda);
			}
		}

		internal BVHNode<T> RootNode()
		{
			BVHNode<T> cur = this;
			while (cur.Parent != null) { cur = cur.Parent; }
			return cur;
		}

		internal void FindOverlappingLeaves(IBVHNodeAdapter<T> nAda, Vector3 origin, float radius, List<BVHNode<T>> overlapList)
		{
			if (BoundsIntersectsSphere(ToBounds(), origin, radius))
			{
				if (GObjects != null)
				{
					overlapList.Add(this);
				}
				else
				{
					Left.FindOverlappingLeaves(nAda, origin, radius, overlapList);
					Right.FindOverlappingLeaves(nAda, origin, radius, overlapList);
				}
			}
		}

		//Modified from https://github.com/jeske/SimpleScene/blob/master/SimpleScene/Core/SSAABB.cs
		private bool BoundsIntersectsSphere(Bounds bounds, Vector3 origin, float radius)
		{
			if (
				(origin.x + radius < bounds.min.x) ||
				(origin.y + radius < bounds.min.y) ||
				(origin.z + radius < bounds.min.z) ||
				(origin.x - radius > bounds.max.x) ||
				(origin.y - radius > bounds.max.y) ||
				(origin.z - radius > bounds.max.z)
			   )
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		internal void FindOverlappingLeaves(IBVHNodeAdapter<T> nAda, Bounds aabb, List<BVHNode<T>> overlapList)
		{
			if (ToBounds().Intersects(aabb))
			{
				if (GObjects != null)
				{
					overlapList.Add(this);
				}
				else
				{
					Left.FindOverlappingLeaves(nAda, aabb, overlapList);
					Right.FindOverlappingLeaves(nAda, aabb, overlapList);
				}
			}
		}

		internal Bounds ToBounds()
		{
			Bounds bounds = new Bounds
			{
				min = new Vector3(Box.min.x, Box.min.y, Box.min.z),
				max = new Vector3(Box.max.x, Box.max.y, Box.max.z)
			};
			return bounds;
		}

		internal void ChildExpanded(IBVHNodeAdapter<T> nAda, BVHNode<T> child)
		{
			bool expanded = false;

			if (child.Box.min.x < Box.min.x)
			{
				Box.min = new Vector3(child.Box.min.x, Box.min.y, Box.min.z);
				expanded = true;
			}
			if (child.Box.max.x > Box.max.x)
			{
				Box.max = new Vector3(child.Box.max.x, Box.max.y, Box.max.z);
				expanded = true;
			}
			if (child.Box.min.y < Box.min.y)
			{
				Box.min = new Vector3( Box.min.x, child.Box.min.y, Box.min.z);
				expanded = true;
			}
			if (child.Box.max.y > Box.max.y)
			{
				Box.max = new Vector3( Box.max.x, child.Box.max.y, Box.max.z);
				expanded = true;
			}
			if (child.Box.min.z < Box.min.z)
			{
				Box.min = new Vector3( Box.min.x, Box.min.y, child.Box.min.z);
				expanded = true;
			}
			if (child.Box.max.z > Box.max.z)
			{
				Box.max = new Vector3( Box.max.x, Box.max.y, child.Box.max.z);
				expanded = true;
			}

			if (expanded && Parent != null)
			{
				Parent.ChildExpanded(nAda, this);
			}
		}

		internal void ChildRefit(IBVHNodeAdapter<T> nAda, bool propagate = true)
		{
			ChildRefit(nAda, this, propagate: propagate);
		}

		internal static void ChildRefit(IBVHNodeAdapter<T> nAda, BVHNode<T> curNode, bool propagate = true)
		{
			do
			{
				Bounds oldbox = curNode.Box;
				BVHNode<T> left = curNode.Left;
				BVHNode<T> right = curNode.Right;

				// start with the left box
				Bounds newBox = left.Box;

				// expand any dimension bigger in the right node
				if (right.Box.min.x < newBox.min.x)
				{
					newBox.min = new Vector3(right.Box.min.x, newBox.min.y, newBox.min.z);
				}
				if (right.Box.min.y < newBox.min.y)
				{
					newBox.min = new Vector3(newBox.min.x, right.Box.min.y, newBox.min.z);
				}
				if (right.Box.min.z < newBox.min.z)
				{
					newBox.min = new Vector3(newBox.min.x, newBox.min.y, right.Box.min.z);
				}

				if (right.Box.max.x > newBox.max.x)
				{
					newBox.max = new Vector3(right.Box.max.x, newBox.max.y, newBox.max.z);
				}
				if (right.Box.max.y > newBox.max.y)
				{
					newBox.max = new Vector3(newBox.max.x, right.Box.max.y, newBox.max.z);
				}
				if (right.Box.max.z > newBox.max.z)
				{
					newBox.max = new Vector3(newBox.max.x, newBox.max.y, right.Box.max.z);
				}

				// now set our box to the newly created box
				curNode.Box = newBox;

				// and walk up the tree
				curNode = curNode.Parent;
			} while (propagate && curNode != null);
		}

		internal BVHNode(BVH<T> bvh)
		{
			GObjects = new List<T>();
			Left = Right = null;
			Parent = null;
			this.NodeNumber = bvh.nodeCount++;
		}

		internal BVHNode(BVH<T> bvh, List<T> gobjectlist) : this(bvh, null, gobjectlist, Axis.X, 0)
		{

		}

		private BVHNode(BVH<T> bvh, BVHNode<T> lparent, List<T> gobjectlist, Axis lastSplitAxis, int curdepth)
		{
			IBVHNodeAdapter<T> nAda = bvh.nAda;
			this.NodeNumber = bvh.nodeCount++;

			this.Parent = lparent; // save off the parent BVHGObj Node
			this.Depth = curdepth;

			if (bvh.maxDepth < curdepth)
			{
				bvh.maxDepth = curdepth;
			}

			// Early out check due to bad data
			// If the list is empty then we have no BVHGObj, or invalid parameters are passed in
			if (gobjectlist == null || gobjectlist.Count < 1)
			{
				throw new Exception("ssBVHNode constructed with invalid paramaters");
			}

			// Check if we’re at our LEAF node, and if so, save the objects and stop recursing.  Also store the min/max for the leaf node and update the parent appropriately
			if (gobjectlist.Count <= bvh.LEAF_OBJ_MAX)
			{
				// once we reach the leaf node, we must set prev/next to null to signify the end
				Left = null;
				Right = null;
				// at the leaf node we store the remaining objects, so initialize a list
				GObjects = gobjectlist;
				GObjects.ForEach(o => nAda.MapObjectToBVHLeaf(o, this));
				ComputeVolume(nAda);
				SplitIfNecessary(nAda);
			}
			else
			{
				// --------------------------------------------------------------------------------------------
				// if we have more than (bvh.LEAF_OBJECT_COUNT) objects, then compute the volume and split
				GObjects = gobjectlist;
				ComputeVolume(nAda);
				SplitNode(nAda);
				ChildRefit(nAda, propagate: false);
			}
		}

	}
}
