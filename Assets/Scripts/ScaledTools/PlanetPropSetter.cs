﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteInEditMode]
public class PlanetPropSetter : MonoBehaviour
{
    Material mat;
    public Mesh exportableMeshTemplate;
    
    // Mesh radius
    float _MeshRadius = 1.0f;

    // Altitudes from planet radius and planet radius
    // Real units, real size
    public float _MinAltitude;
    public float _MaxAltitude;
    public float _LowMidBlendStart;
    public float _LowMidBlendEnd;
    public float _MidHighBlendStart;
    public float _MidHighBlendEnd;
    public float _PlanetRadius = 0.5f;
    public float _PlanetRadius2 = 0.5f;

    public float _SkyboxRotation = 0;

    void Start()
    {
        mat = GetComponent<MeshRenderer>().sharedMaterial;
        //_MeshRadius = gameObject.GetComponent<MeshRenderer>().bounds.size.x * 0.5f; //gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * 0.5f;

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        if (mesh.isReadable)
        {
            Vector3[] verts = GetComponent<MeshFilter>().sharedMesh.vertices;
            float avgRad = 0;
            foreach (Vector3 v in verts)
            {
                avgRad += Vector3.Distance(gameObject.transform.position, transform.TransformPoint(v));
            }
            avgRad /= verts.Length;
            _MeshRadius = avgRad;
            //Debug.Log("Mesh rad: " + _MeshRadius + " go name: " + gameObject.name);
        }
        else
        {
            _MeshRadius = 0.5f;//gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * 0.5f * transform.localScale.x;
        }
    }

    // Update is called once per frame
    void Update()
    {

        // Assume normalised heightmap (0 min altitude, 1 max altitude)

        // Multiply by the scaling factor to maintain proportions
        float scalingFactor = _MeshRadius / _PlanetRadius;

        mat.SetVector("_PlanetOrigin", transform.position);
        mat.SetFloat("_MinRadialAltitude", (_MinAltitude) * scalingFactor);
        mat.SetFloat("_MaxRadialAltitude", (_MaxAltitude) * scalingFactor);

        mat.SetFloat("_LowMidBlendStart", (_PlanetRadius + _LowMidBlendStart) * scalingFactor);
        mat.SetFloat("_LowMidBlendEnd", (_PlanetRadius + _LowMidBlendEnd) * scalingFactor);
        mat.SetFloat("_MidHighBlendStart", (_PlanetRadius + _MidHighBlendStart) * scalingFactor);
        mat.SetFloat("_MidHighBlendEnd", (_PlanetRadius + _MidHighBlendEnd) * scalingFactor);

        mat.SetFloat("_WorldPlanetRadius", _MeshRadius);
        mat.SetMatrix("_WorldRotation", Matrix4x4.Inverse(Matrix4x4.Rotate(gameObject.transform.rotation)));

        mat.SetFloat("_ScaleFactor", scalingFactor);

        Quaternion rot = Quaternion.Euler(0, _SkyboxRotation, 0);
        Matrix4x4 rotMat = Matrix4x4.Rotate(rot);

        mat.SetMatrix("_SkyboxRotation", rotMat);
    }
    public float EstimateMaxRayLength()
    {
        // At each pixel in the height map, calculate distance to sphere tangent
        Texture2D heightMap = (Texture2D)gameObject.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_HeightMap");
        if (heightMap == null)
        {
            return 0;
        }
        float maxAltitude = 0;

        float scalingFactor = _MeshRadius / _PlanetRadius;

        int width = heightMap.width;
        int height = heightMap.height;
        float altitude = 0;
        Vector3 worldPos = Vector3.zero;
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                altitude = Mathf.Lerp(_MinAltitude, _MaxAltitude, heightMap.GetPixel(i, j).r);
                if (altitude > maxAltitude)
                {
                    maxAltitude = altitude;
                }
            }
        }

        // Now compute distance to sphere tangent
        float worldDistance = maxAltitude * scalingFactor + _MeshRadius;
        float theta = Mathf.Asin(_MeshRadius / worldDistance);
        float tangentDist = _MeshRadius / Mathf.Tan(theta);

        Debug.Log("Tangent distance: " + tangentDist);
        Debug.Log("Tangent distance as function of planet radius: " + tangentDist / _MeshRadius);

        return tangentDist;
    }
    public Mesh GenerateMesh()
    {
        Mesh mesh = exportableMeshTemplate;
        if (!mesh) { return null; }

        return mesh;
    }
}

[CustomEditor(typeof(PlanetPropSetter))]
class PlanetPropSetterButton : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Generate Mesh"))
        {
            PlanetPropSetter script = (PlanetPropSetter)target;
            Mesh mesh = script.GenerateMesh();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            go.transform.position = Vector3.zero;
        }
        if (GUILayout.Button("Calculate ray step size"))
        {
            PlanetPropSetter script = (PlanetPropSetter)target;
            script.EstimateMaxRayLength();
        }
    }
}