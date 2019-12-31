# UnityBoundingVolumeHeirachy
Dynamic Unity 3d BVH - 3d Bounding Volume Hierarchy
Modified from David Jeske's [SimpleScene](https://github.com/jeske/SimpleScene/tree/master/SimpleScene/Util/ssBVH)

### About

This is a 3d Bounding Volume Hiearchy implementation for Unity in C#. It is used for sorting objects that occupy 
volume and answer geometric queries about them, such as ray, box, and sphere intersection. 

It includes an efficient algorithm for incrementally re-optimizing the BVH when contained objects move. 

For more information about what a BVH is, and about how to use this code, see the CodeProject article:

* [Dynamic Bounding Volume Hierarchy in C#](https://www.codeproject.com/Articles/832957/Dynamic-Bounding-Volume-Hiearchy-in-Csharp)

### Modifications

<table>
<tr>
<td>BVH.cs</td>
<td> The root interface to the BVH. Call RenderDebug() to render the debug bounds.</td></tr>
<tr>
<td>BVHNode.cs</td>
<td> The code for managing, traversing, and optimizing the BVH </td></tr>
<tr>
<td>BVHGameObjectAdaptor.cs</td>
<td> A example IBVHNodeAdaptor GameObject integration. Uses child renderers bounds to calculate bounds. Could be easily swapped out for any other bounds calculation. See BVHGameObjectAdaptor.GetBounds(GameObject obj)</td></tr>
<tr>
<td>BVHSphereAdaptor.cs</td>
<td> An example IBVHNodeAdaptor for placing spheres in the BVH.</td></tr>
<tr>
<td>IBVHNodeAdaptor.cs</td>
<td> Base interface for any BVHNodeAdaptor. Implement this to create a new adaptor.</td></tr>
</table>

- Supports GameObjects through a custom implementation of IBVHNodeAdapter (BVHGameObjectAdapter) 
- BVHHelper provides a radial node traversal test to be used with Traverse()
- Includes some tests that can be used as a starting reference.

### References

* ["Fast, Effective BVH Updates for Animated Scenes" (Kopta, Ize, Spjut, Brunvand, David, Kensler)](https://github.com/jeske/SimpleScene/blob/master/SimpleScene/Util/ssBVH/docs/BVH_fast_effective_updates_for_animated_scenes.pdf)
* [Space Partitioning: Octree vs. BVH](http://thomasdiewald.com/blog/?p=1488)

### Pictures
![BVH GIF](https://media.giphy.com/media/ZaomLtyboZSp9zl6WY/giphy.gif)
