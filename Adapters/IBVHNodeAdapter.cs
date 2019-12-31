using UnityEngine;

namespace DataStructures
{
	public interface IBVHNodeAdapter<T>
	{
		BVH<T> BVH { get; set; }
		Vector3 GetObjectPos(T obj);
		float GetRadius(T obj);
		void MapObjectToBVHLeaf(T obj, BVHNode<T> leaf);
        void OnPositionOrSizeChanged(T changed);
        void UnmapObject(T obj);
		void CheckMap(T obj);
		BVHNode<T> GetLeaf(T obj);
	}
}