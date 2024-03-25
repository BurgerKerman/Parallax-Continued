﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    public struct Triangle
    {
        public Vector3 center;
        public Vector3 v1, v2, v3;
        public Vector3 n1, n2, n3;
        public Color c1, c2, c3;
        public int index1, index2, index3;
        public Triangle(Vector3 center, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3, Color c1, Color c2, Color c3, int index1, int index2, int index3)
        {
            this.center = center;
            this.v1 = v1; this.v2 = v2; this.v3 = v3;
            this.n1 = n1; this.n2 = n2; this.n3 = n3;
            this.c1 = c1; this.c2 = c2; this.c3 = c3;
            this.index1 = index1;
            this.index2 = index2;
            this.index3 = index3;
        }
    }
    public class ASQuad
    {
        public Dictionary<Vector3, int> newVertexIndices = new Dictionary<Vector3, int>();
        public HashSet<Vector3> newHashVerts = new HashSet<Vector3>();
        public List<int> newTris = new List<int>();
        public List<Vector3> newVerts = new List<Vector3>();
        public List<Vector3> newNormals = new List<Vector3>();
        public List<Color> newColors = new List<Color>();
        public void AppendTriangle(Triangle tri)
        {
            int index1;
            int index2;
            int index3;

            if (newHashVerts.Add(tri.v1)) { index1 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v1, index1); newVerts.Add(tri.v1); newNormals.Add(tri.n1); newColors.Add(tri.c1); } else { index1 = newVertexIndices[tri.v1]; }
            if (newHashVerts.Add(tri.v2)) { index2 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v2, index2); newVerts.Add(tri.v2); newNormals.Add(tri.n2); newColors.Add(tri.c2); } else { index2 = newVertexIndices[tri.v2]; }
            if (newHashVerts.Add(tri.v3)) { index3 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v3, index3); newVerts.Add(tri.v3); newNormals.Add(tri.n3); newColors.Add(tri.c3); } else { index3 = newVertexIndices[tri.v3]; }

            newTris.Add(index1);
            newTris.Add(index2);
            newTris.Add(index3);
        }
        public void Clear()
        {
            newVertexIndices.Clear();
            newHashVerts.Clear();
            newTris.Clear();
            newVerts.Clear();
            newNormals.Clear();
            newColors.Clear();
        }
    }
    public class AdvancedSubdivision
    {
        private Vector3[] quadVerts;
        private Vector3[] quadNormals;
        private Color[] quadColors;
        private int[] quadIndices;
        private Triangle[] quadTris;

        public GameObject fakeQuad;                                                     //The instantiated mesh from the quad
        public GameObject cutoutQuad;                                                   //The actual, new cutout part from the quad
        private Mesh mesh;
        private Mesh quadMesh;
        private Mesh storedMesh;
        public float searchRadius;

        public ASQuad newQuad = new ASQuad();
        public ASQuad oldQuad = new ASQuad();

        public PQ quad;
        public Material quadMaterial;

        float timeSinceLastUpdate = 0;
        int subdivisionLevel;
        private bool cleaned = false;

        public AdvancedSubdivision(PQ quad, ref GameObject fakeQuad, ref Mesh quadMesh, float searchRadius, ref Material quadMaterial, int subdivisionLevel)
        {
            this.quad = quad;
            this.fakeQuad = fakeQuad;
            this.quadMesh = quadMesh;
            this.quadVerts = quadMesh.vertices;
            this.quadIndices = quadMesh.triangles;
            this.quadNormals = quadMesh.normals;
            this.quadColors = quadMesh.colors;
            this.quadMaterial = quadMaterial;
            this.subdivisionLevel = subdivisionLevel;
            this.searchRadius = Mathf.Max((Mathf.Sqrt(searchRadius) / 15f) * 2.5f, 50);    //5x5 square of vertices, or a range of 50 meters (on small planets)
            this.searchRadius *= this.searchRadius;
            this.storedMesh = GameObject.Instantiate(quad.mesh);
            GameEvents.onVesselChange.Add(OnVesselSwitch);
            Initialize();
        }

        void Initialize()
        {
            CreateGameObject();
            CreateTris();
            mesh = new Mesh();
            cutoutQuad.GetComponent<MeshRenderer>().sharedMaterial = quadMaterial;
            cutoutQuad.SetActive(true);
        }
        void CreateGameObject()
        {
            cutoutQuad = new GameObject();
            cutoutQuad.AddComponent<MeshFilter>();
            cutoutQuad.AddComponent<MeshRenderer>();
            cutoutQuad.transform.position = quad.gameObject.transform.position;
            cutoutQuad.transform.localScale = quad.gameObject.transform.localScale;
            cutoutQuad.gameObject.transform.rotation = quad.gameObject.transform.rotation;
            cutoutQuad.transform.parent = quad.gameObject.transform;
            cutoutQuad.layer = quad.gameObject.layer;
            cutoutQuad.tag = quad.gameObject.tag;

            Debug.Log("Adv quad layer, tag: " + cutoutQuad.layer + ", " + cutoutQuad.tag);
        }
        void CreateTris()
        {
            quadTris = new Triangle[quadIndices.Length / 3];
            for (int i = 0; i < quadIndices.Length; i += 3)                             //Create array of each triangle in the quad
            {
                int index1 = quadIndices[i + 0];
                int index2 = quadIndices[i + 1];
                int index3 = quadIndices[i + 2];

                Vector3 v1 = quadVerts[index1];
                Vector3 v2 = quadVerts[index2];
                Vector3 v3 = quadVerts[index3];

                Vector3 n1 = quadNormals[index1];
                Vector3 n2 = quadNormals[index2];
                Vector3 n3 = quadNormals[index3];

                Color c1 = quadColors[index1];
                Color c2 = quadColors[index2];
                Color c3 = quadColors[index3];

                Vector3 center = (v1 + v2 + v3) / 3;

                Triangle tri = new Triangle(center, v1, v2, v3, n1, n2, n3, c1, c2, c3, quadIndices[i], quadIndices[i + 1], quadIndices[i + 2]);
                quadTris[i / 3] = tri;
            }
        }
        bool completedOneCheck = false;
        public void RangeCheck(ref Vector3 originPoint, bool force)
        {
            if (!force)
            {
                if (!FlightGlobals.ready || cleaned) { return; }
                if (completedOneCheck && (Time.realtimeSinceStartup - timeSinceLastUpdate < 0.5f || (FlightGlobals.ActiveVessel != null && (FlightGlobals.ActiveVessel.speed > 100)))) { return; }
                timeSinceLastUpdate = Time.realtimeSinceStartup;
                completedOneCheck = true;
            }

            newQuad.Clear();
            oldQuad.Clear();

            Triangle tri;
            for (int i = 0; i < quadTris.Length; i++)
            {
                tri = quadTris[i];
                if ((tri.center - originPoint).sqrMagnitude < searchRadius)
                {
                    newQuad.AppendTriangle(tri);                                        //Construct new quad mesh
                }
                else
                {
                    oldQuad.AppendTriangle(tri);                                        //Construct old quad mesh with the new quad mesh missing. Funny quirky goofy ahh jigsaw
                }
            }

            mesh.Clear();
            if (subdivisionLevel > 36) { mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
            mesh.vertices = newQuad.newVerts.ToArray();
            mesh.triangles = newQuad.newTris.ToArray();
            mesh.normals = newQuad.newNormals.ToArray();
            mesh.colors = newQuad.newColors.ToArray();
            MeshHelper.Subdivide(mesh, subdivisionLevel);
            cutoutQuad.GetComponent<MeshFilter>().sharedMesh = mesh;


            quadMesh.Clear();
            quadMesh.vertices = oldQuad.newVerts.ToArray();
            quadMesh.triangles = oldQuad.newTris.ToArray();
            quadMesh.normals = oldQuad.newNormals.ToArray();
            quadMesh.colors = oldQuad.newColors.ToArray();
            fakeQuad.GetComponent<MeshFilter>().sharedMesh = quadMesh;
        }
        public void OnVesselSwitch(Vessel v)
        {
            RebuildMesh();
            Vector3 craftPos = v.transform.position;
            RangeCheck(ref craftPos, true);
        }
        public void RebuildMesh()
        {
            oldQuad.Clear();
            for (int i = 0; i < quadTris.Length; i++)
            {
                oldQuad.AppendTriangle(quadTris[i]);                                        //Construct old quad mesh with the new quad mesh missing. Funny quirky goofy ahh jigsaw
            }
            quadMesh.Clear();
            quadMesh.vertices = oldQuad.newVerts.ToArray();
            quadMesh.triangles = oldQuad.newTris.ToArray();
            quadMesh.normals = oldQuad.newNormals.ToArray();
            quadMesh.colors = oldQuad.newColors.ToArray();
            fakeQuad.GetComponent<MeshFilter>().sharedMesh = quadMesh;
        }
        public void Cleanup()
        {
            GameEvents.onVesselChange.Remove(OnVesselSwitch);
            RebuildMesh();
            newQuad.Clear();
            oldQuad.Clear();
            UnityEngine.GameObject.Destroy(cutoutQuad);
            quadVerts = null;
            quadIndices = null;
            quadTris = null;
            cleaned = true;
        }
    }
}
