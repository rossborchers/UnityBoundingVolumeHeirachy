using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using DataStructures;
using NUnit.Framework;

namespace DataStructures.Tests
{
    public class BVHTests
    {
        private const float TIME_PER_TEST = 10;

        List<GameObject> SetupScene(int objectCount = 25)
        {
            GameObject lightObj = new GameObject("Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = UnityEngine.LightType.Directional;
 
            GameObject cameraObj = new GameObject("Camera");
            cameraObj.transform.position = new Vector3(0, 0, -460);
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(77f/255f, 77f/255f, 77f/255f);


            List<GameObject> objects = new List<GameObject>();
            for (int i = 0; i < objectCount; i++)
            {
                GameObject obj = CreateGameObject();
                objects.Add(obj);
            }
            return objects;
        }

        private GameObject CreateGameObject()
        {
            GameObject obj = GameObject.CreatePrimitive((PrimitiveType)Random.Range(0, 6));
            obj.transform.position = Random.insideUnitSphere * 100f * Random.Range(1, 3);
            obj.transform.localScale = new Vector3(Mathf.Max(1f, 5f), Mathf.Max(1f, 5f), Mathf.Max(1f, 5f));
            obj.transform.rotation = Random.rotation;
            return obj;
        }

        private void DestroyScene(List<GameObject> objects)
        {
            Object.Destroy(GameObject.Find("Light"));
            Object.Destroy(GameObject.Find("Light"));

            foreach(GameObject obj in objects)
            {
                Object.Destroy(obj);
            }     
        }

        BVH<GameObject> CreateBVH(List<GameObject> objects)
        {
            return new BVH<GameObject>(new BVHGameObjectAdapter(), objects);
        }

        [UnityTest]
        public IEnumerator Test1CreateThroughConstruct()
        {
            List<GameObject> objects = SetupScene();

            BVH<GameObject> bvh = CreateBVH(objects);
            float t = 0;
            while(t < 20)
            {
                t += Time.deltaTime;

                //TODO: use opengl? to render the bvh
                //bvh.Render();

                bvh.RenderDebug();
                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test2Addition()
        {
            List<GameObject> objects = SetupScene(0);
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            int last = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;
                if (t * 5 > last)
                {
                    last++;
                    GameObject obj = CreateGameObject();
                    objects.Add(obj);
                    bvh.Add(obj);
                }

                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test3SingleItemPositionUpdate()
        {
            List<GameObject> objects = SetupScene();
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;

                objects[0].transform.Translate(objects[0].transform.forward * Time.deltaTime * 10);

                bvh.MarkForUpdate(objects[0]);

                bvh.Optimize();

                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test4MultipleItemPositionUpdate()
        {
            List<GameObject> objects = SetupScene();
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;

                foreach(GameObject obj in objects)
                {
                    obj.transform.Translate(obj.transform.forward * Time.deltaTime * 5);
                    bvh.MarkForUpdate(obj);
                }

                bvh.Optimize();
                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test5AdditionAndUpdate()
        {
            List<GameObject> objects = SetupScene(0);
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            int last = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;
                if (t * 5 > last)
                {
                    last++;
                    GameObject obj = CreateGameObject();
                    objects.Add(obj);
                    bvh.Add(obj);
                }

                foreach (GameObject obj in objects)
                {
                    obj.transform.Translate(obj.transform.forward * Time.deltaTime * 5);
                    bvh.MarkForUpdate(obj);
                }

                bvh.Optimize();
                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test6Destroy()
        {
            List<GameObject> objects = SetupScene();
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            int last = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;
                if (t * 5 > last)
                {
                    last++;
                    if(objects.Count > 0)
                    {
                        GameObject obj = objects[objects.Count - 1];
                        bvh.Remove(obj);
                        objects.RemoveAt(objects.Count - 1);
                        Object.Destroy(obj);
                    }
                }

                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test7DestroyAndUpdate()
        {
            List<GameObject> objects = SetupScene();
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            int last = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;
                if (t * 5 > last)
                {
                    last++;
                    if (objects.Count > 0)
                    {
                        GameObject obj = objects[objects.Count - 1];
                        bvh.Remove(obj);
                        objects.RemoveAt(objects.Count - 1);
                        Object.Destroy(obj);
                    }
                }

                foreach (GameObject obj in objects)
                {
                    obj.transform.Translate(obj.transform.forward * Time.deltaTime * 5);
                    bvh.MarkForUpdate(obj);
                }

                bvh.Optimize();
                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test8AddDestroyAndUpdate()
        {
            List<GameObject> objects = SetupScene();
            BVH<GameObject> bvh = CreateBVH(objects);

            float t = 0;
            int last = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;
                if (t * 5 > last)
                {
                    last++;

                    if (Random.Range(0, 2) == 1)
                    {
                        GameObject obj = CreateGameObject();
                        objects.Add(obj);
                        bvh.Add(obj);
                    }
                    else if (objects.Count > 0)
                    {
                        GameObject obj = objects[objects.Count - 1];
                        bvh.Remove(obj);
                        objects.RemoveAt(objects.Count - 1);
                        Object.Destroy(obj);
                    }
                }

                foreach (GameObject obj in objects)
                {
                    obj.transform.Translate(obj.transform.forward * Time.deltaTime * 5);
                    bvh.MarkForUpdate(obj);
                }

                bvh.Optimize();
                bvh.RenderDebug();

                yield return null;
            }

            DestroyScene(objects);
        }

        [UnityTest]
        public IEnumerator Test9RadialRetrival()
        {
            List<GameObject> objects = SetupScene(0);
            BVH<GameObject> bvh = CreateBVH(objects);

            GameObject insideObj1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            insideObj1.name = "In Radius 3";
            insideObj1.transform.localScale = Vector3.one * 0.01f;
            insideObj1.transform.position = new Vector3(0, 5, 0);

            GameObject insideObj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            insideObj2.name = "In Radius 1";
            insideObj2.transform.localScale = Vector3.one * 5;
            insideObj2.transform.position = new Vector3(0, -4.5f, 0);

            GameObject insideObj3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            insideObj3.name = "In Radius 2";
            insideObj3.transform.localScale = Vector3.one * 5;
            insideObj3.transform.position = new Vector3(-5, 0, 0);

            GameObject outsideObj1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outsideObj1.name = "Outside Radius 1";
            outsideObj1.transform.localScale = Vector3.one * 5;
            outsideObj1.transform.position = new Vector3(0, -40, 0);

            GameObject outsideObj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outsideObj2.name = "Outside Radius 2";
            outsideObj2.transform.localScale = Vector3.one * 5;
            outsideObj2.transform.position = new Vector3(-5.1f, 0, 0);


            objects.Add(insideObj1);
            bvh.Add(insideObj1);

            objects.Add(insideObj2);
            bvh.Add(insideObj2);

            objects.Add(insideObj3);
            bvh.Add(insideObj3);

            objects.Add(outsideObj1);
            bvh.Add(outsideObj1);

            objects.Add(outsideObj2);
            bvh.Add(outsideObj2);

            float t = 0;
            while (t < TIME_PER_TEST)
            {
                t += Time.deltaTime;

                bvh.RenderDebug();

                Vector3 center = new Vector3(0, 5, 0);
                float radius = 10f;
                List<BVHNode<GameObject>> hit = bvh.Traverse(BVHHelper.RadialNodeTraversalTest(center, radius));

                Debug.DrawLine(center, center + new Vector3(radius, 0, 0), Color.red);
                Debug.DrawLine(center, center + new Vector3(0, radius, 0), Color.green);
                Debug.DrawLine(center, center + new Vector3(0, 0, radius), Color.blue);

                Debug.Log("Traversed " + hit.Count + " nodes");

                int objectCount = 0;
                foreach (var g in hit)
                {
                    if(g.GObjects != null)
                    {
                        foreach (GameObject obj in g.GObjects)
                        {
                            Debug.Log("Found Object: " + obj.name);
                            Assert.AreNotSame(obj.name, "Outside Radius");
                            objectCount++;
                        }
                    }       
                }

                Debug.Log("Found " + objectCount + " leaf nodes");

                yield return null;
            }

            DestroyScene(objects);
        }
    }
}
