using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(ThreeLSControl))]
public class ThreeLSControlEditor : Editor
{
    private bool showLipSyncNames = true;
    private bool showEyeBrowNames = true;
    private string[] allBlendShapes;
    private string defaultExportFileName = "LipsyncExport.anim";
    // en la clase ThreeLSControlEditor (fuera del método)
    private bool showHeadMotion = true;


    public override void OnInspectorGUI()
    {
        ThreeLSControl control = (ThreeLSControl)target;

        serializedObject.Update();
        // fuerza el repintado para que los botones Start/Stop reflejen el estado
        Repaint();

        // --- Cargar BlendShapes ---
        if (GUILayout.Button("Load Available Blend Shapes"))
        {
            allBlendShapes = GetAllBlendShapeNames(control);
        }
        if (allBlendShapes == null || allBlendShapes.Length == 0)
        {
            allBlendShapes = GetAllBlendShapeNames(control);
        }

        // --- Configuración de Lipsync ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lipsync Control", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("threshold"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothness"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("vocalTractFactor"));

        // Visualización y Delay de inicio
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spectrumVisualization"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startDelaySeconds"));

        // --- Nombres de Blend Shapes ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blend Shape Names", EditorStyles.boldLabel);

        showLipSyncNames = EditorGUILayout.Foldout(showLipSyncNames, "Lipsync Blend Shapes");
        if (showLipSyncNames)
        {
            DrawBlendShapeDropdownList("Kiss", control.kissBlendShapeNames);
            DrawBlendShapeDropdownList("Lips Closed", control.lipsClosedBlendShapeNames);
            DrawBlendShapeDropdownList("Mouth Open", control.mouthOpenBlendShapeNames);
        }

        showEyeBrowNames = EditorGUILayout.Foldout(showEyeBrowNames, "Eyes & Eyebrows Blend Shapes");
        if (showEyeBrowNames)
        {
            DrawBlendShapeDropdownList("Eye Close Left", control.eyeCloseLBlendShapeNames);
            DrawBlendShapeDropdownList("Eye Close Right", control.eyeCloseRBlendShapeNames);
            DrawBlendShapeDropdownList("Eyebrow Left", control.eyebrowLBlendShapeNames);
            DrawBlendShapeDropdownList("Eyebrow Right", control.eyebrowRBlendShapeNames);
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("kissFactor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lipsClosedFactor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mouthOpenFactor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeCloseFactor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eyebrowFactor"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableAutoBlink"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blinkIntervalRange"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blinkDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blinkCurve"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableAutoBrow"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("browChancePerBlink"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("browPulseDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("browAmount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("browCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("perEyeStartJitter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("perEyeDurationJitter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("winkChance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("browPerSideStartJitter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("browPerSideDurationJitter"));

        // --- Head Motion ---
        EditorGUILayout.Space();
        showHeadMotion = EditorGUILayout.Foldout(showHeadMotion, "Head Motion");
        if (showHeadMotion)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableHeadMotion"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headTarget"), new GUIContent("Head Bone/Control"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headAnimationRoot"), new GUIContent("Head Animation Root (Export)"));


            // Aviso si falta hueso de cabeza asignado
            if (serializedObject.FindProperty("enableHeadMotion").boolValue &&
                serializedObject.FindProperty("headTarget").objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Asigna el 'Head Bone/Control' para poder mover la cabeza.", MessageType.Warning);
            }
            if (serializedObject.FindProperty("enableHeadMotion").boolValue &&
                serializedObject.FindProperty("headAnimationRoot").objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Asigna el 'Head Animation Root' (normalmente el root del esqueleto) para exportar correctamente la animación.", MessageType.Warning);
            }

            // Amplitudes principales
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headYawDeg"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headPitchDeg"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headRollDeg"));

            // Ruido sutil
            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseAmpDeg"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseFreq"));

            // Acoplamiento a voz
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reactiveAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reactiveFreq"));

            // Impulsos por sílaba
            EditorGUILayout.PropertyField(serializedObject.FindProperty("syllableImpulse"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("syllableCooldown"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headImpulseCurve"));

            EditorGUILayout.EndVertical();
        }

        // --- Exportación de Animación ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Export", EditorStyles.boldLabel);
        control.exportOnQuit = EditorGUILayout.Toggle("Export on Quit", control.exportOnQuit);
        defaultExportFileName = EditorGUILayout.TextField("Default File Name", defaultExportFileName);

        // Start/Stop según estado de grabación
        EditorGUI.BeginDisabledGroup(control.IsRecording);
        if (GUILayout.Button("Start Recording"))
        {
            StartExporting(control);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!control.IsRecording);
        if (GUILayout.Button("Stop Recording"))
        {
            StopExporting(control);
        }
        EditorGUI.EndDisabledGroup();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBlendShapeDropdownList(string label, List<string> list)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(label, EditorStyles.boldLabel);

        if (allBlendShapes == null || allBlendShapes.Length == 0)
        {
            EditorGUILayout.HelpBox("No blend shapes found on children SkinnedMeshRenderers.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        if (GUILayout.Button("Add new"))
        {
            list.Add(allBlendShapes[0]);
        }

        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            int currentIndex = System.Array.IndexOf(allBlendShapes, list[i]);
            int newIndex = EditorGUILayout.Popup(currentIndex, allBlendShapes);
            list[i] = allBlendShapes[newIndex];

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                list.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }


    private void StartExporting(ThreeLSControl control)
    {
        var audioSource = control.GetComponent<AudioSource>();
        if (audioSource == null || audioSource.clip == null)
        {
            EditorUtility.DisplayDialog("Export Error", "Please add an AudioSource component with an audio clip to this GameObject.", "OK");
            return;
        }

        // NEW: usar SaveFilePanelInProject para asegurar ruta relativa dentro de Assets
        string relativePath = EditorUtility.SaveFilePanelInProject(
            "Save Lipsync Animation",
            defaultExportFileName,
            "anim",
            "Choose a file name and location inside the project."
        );

        if (!string.IsNullOrEmpty(relativePath))
        {
            // NEW: encolamos en el componente y luego entramos a Play
            control.PrepareExportPath(relativePath);
            EditorApplication.isPlaying = true;
        }
    }

    private void StopExporting(ThreeLSControl control)
    {
        control.StopAndExportLipsync();
    }

    private string[] GetAllBlendShapeNames(ThreeLSControl control)
    {
        HashSet<string> names = new HashSet<string>();
        foreach (var smr in control.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh != null)
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    names.Add(mesh.GetBlendShapeName(i));
            }
        }
        return names.ToArray();
    }
}