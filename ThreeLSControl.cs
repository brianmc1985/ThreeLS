/*
 * ThreeLSControl
 *
 * Autor original: Gerard Llorach
 * Extensión: asimetría L/R y tiempos con jitter por Bruno + Copilot
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

public class ThreeLSControl : MonoBehaviour
{
    [Range(0.01f, 2.0f)]
    public float threshold = 0.5f;
    [Range(0.01f, 1.0f)]
    public float smoothness = 0.6f;
    [Range(0.01f, 1.0f)]
    public float vocalTractFactor = 1.0f;

    // ===== Lipsync (existente) =====
    public List<string> kissBlendShapeNames = new List<string>();
    public List<string> lipsClosedBlendShapeNames = new List<string>();
    public List<string> mouthOpenBlendShapeNames = new List<string>();
    [Range(0.01f, 3.0f)] public float kissFactor = 1.0f;
    [Range(0.01f, 3.0f)] public float lipsClosedFactor = 1.0f;
    [Range(0.01f, 3.0f)] public float mouthOpenFactor = 1.0f;
    public bool spectrumVisualization = false;

    // ===== NUEVO: Eye Close & Eyebrows por lado =====

    // --- Head Motion (habla reactiva) ---
    [Header("Head Motion")]
    public bool enableHeadMotion = true;
    public Transform headTarget; // arrastra aquí tu hueso/cntrl de cabeza
    public Transform headAnimationRoot; // NUEVO: el root del Animator para exportar

    [Range(0f, 20f)] public float headYawDeg = 6f;   // izquierda-derecha (Y)
    [Range(0f, 20f)] public float headPitchDeg = 4f; // arriba-abajo (X)
    [Range(0f, 20f)] public float headRollDeg = 2f;  // ladeo (Z)

    [Tooltip("Micro-ruido (°)")]
    [Range(0f, 5f)] public float noiseAmpDeg = 0.5f;
    [Range(0.05f, 5f)] public float noiseFreq = 1.2f;

    [Tooltip("Peso de movimiento acoplado a energía de voz")]
    [Range(0f, 1f)] public float reactiveAmount = 0.7f;
    [Tooltip("Hz base del cabeceo rítmico")]
    [Range(0.1f, 6f)] public float reactiveFreq = 2.0f;

    [Header("Head Impulses (sílabas)")]
    [Range(0f, 1f)] public float syllableImpulse = 0.5f;
    [Range(0.05f, 0.4f)] public float syllableCooldown = 0.12f;
    public AnimationCurve headImpulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Estado base de cabeza
    private Quaternion headRestLocalRot;
    private float headPhase;
    private float lastSyllableTime;
    private float impulseT = -1f; // <0: sin impulso activo
    private float prevSpeechLevel;

    // Para grabar curvas de cabeza
    private List<Keyframe> headYawKf, headPitchKf, headRollKf;


    [Header("---- Eyes & Eyebrows (L/R) ----")]
    public List<string> eyeCloseLBlendShapeNames = new List<string>();
    public List<string> eyeCloseRBlendShapeNames = new List<string>();
    public List<string> eyebrowLBlendShapeNames = new List<string>();
    public List<string> eyebrowRBlendShapeNames = new List<string>();
    [Range(0.01f, 3.0f)] public float eyeCloseFactor = 1.0f; // se aplica a L y R
    [Range(0.01f, 3.0f)] public float eyebrowFactor = 1.0f; // se aplica a L y R

    [Header("Auto Blink")]
    public bool enableAutoBlink = true;
    public Vector2 blinkIntervalRange = new Vector2(1f, 5f);
    public float blinkDuration = 0.12f;
    public AnimationCurve blinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Auto Eyebrow Pulse")]
    public bool enableAutoBrow = true;
    [Range(0f, 1f)] public float browChancePerBlink = 0.6f;
    public float browPulseDuration = 0.25f;
    [Range(0f, 1f)] public float browAmount = 0.5f;
    public AnimationCurve browCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Realismo (asimetría/variación)")]
    [Tooltip("± segundos de desfase entre ojos.")]
    public float perEyeStartJitter = 0.03f; // ~30 ms
    [Tooltip("Factor de variación de duración por lado (±%). 0.2=±20%")]
    [Range(0f, 0.5f)] public float perEyeDurationJitter = 0.2f;
    [Tooltip("Probabilidad de guiño (blink de un solo ojo).")]
    [Range(0f, 1f)] public float winkChance = 0.07f;
    [Tooltip("± segundos de desfase entre cejas.")]
    public float browPerSideStartJitter = 0.05f;
    [Tooltip("Factor de variación de duración por lado (±%) en cejas.")]
    [Range(0f, 0.5f)] public float browPerSideDurationJitter = 0.2f;

    // ====== Internos ======
    private AudioSource audioInput;
    private ThreeLS LS;

    [Header("Exportación")]
    public bool exportOnQuit = false;
    private bool isRecording = false;
    private List<Keyframe>[] lipsyncKeyframes = new List<Keyframe>[7];
    private float recordingStartTime;
    [SerializeField] private string manualExportFilePath;

    private float[] appliedGroupWeights = new float[7];


    // --- Inicio/Delay ---
    [Header("Inicio/Delay")]
    [Tooltip("Retraso inicial (segundos) antes de activar audio, lipsync, blendshapes y auto-blink/brow.")]
    [Min(0f)] public float startDelaySeconds = 1f;      // << configurable en el inspector
    private float playStartTime;
    private bool systemsActivated = false;

    // --- Arranque de grabación encolado desde el Editor ---
    [SerializeField] private bool startRecordingOnPlay = false;

    public bool IsRecording { get { return isRecording; } }

    private class BSReference
    {
        public List<SkinnedMeshRenderer> meshes;
        public List<int> BSIndices;
        public bool[] BSNameIsFound;
        public string[] BSNames;
        public float influence;

        public BSReference(string[] inBSNames)
        {
            BSNames = inBSNames;
            BSNameIsFound = new bool[inBSNames.Length];
            meshes = new List<SkinnedMeshRenderer>();
            BSIndices = new List<int>();
        }
    }

    // 0: Kiss, 1: LipsClosed, 2: MouthOpen, 3: EyeCloseL, 4: EyeCloseR, 5: EyebrowL, 6: EyebrowR
    private BSReference[] BSToFind;
    private bool fadingOut;
    private bool wasPlaying;
    private float timeToFadeOut;
    private Coroutine autoBlinkRoutine;

    // ----------------- Ciclo de vida -----------------
    void Start()
    {
        playStartTime = Time.time;               // arranca el cronómetro del delay
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        // Espera a tener AudioSource
        while (audioInput == null)
        {
            audioInput = GetComponent<AudioSource>();
            yield return new WaitForSeconds(0.1f);
        }

        // Crear analizador y detectar blendshapes
        LS = new ThreeLS(audioInput, threshold, smoothness, vocalTractFactor);
        FindBlendShapes();

        // Auto blink/brow (la rutina esperará al delay)
        if (autoBlinkRoutine != null) StopCoroutine(autoBlinkRoutine);
        autoBlinkRoutine = StartCoroutine(AutoBlinkAndBrow());

        // NOTA: el arranque real (lipsync, audio, etc.) se hará en Update() cuando pase el delay
        if (headTarget != null)
            headRestLocalRot = headTarget.localRotation;

    }

    void OnDisable()
    {
        if (autoBlinkRoutine != null)
        {
            StopCoroutine(autoBlinkRoutine);
            autoBlinkRoutine = null;
        }
    }

    void Update()
    {
        // --- Gate por delay inicial ---
        if (!systemsActivated)
        {
            if (Time.time - playStartTime < startDelaySeconds)
                return;

            systemsActivated = true;

#if UNITY_EDITOR
            // Si venimos con la grabación encolada desde el Editor, arrancar ahora
            if (startRecordingOnPlay && Application.isPlaying && !isRecording)
            {
                StartLipsyncExport(manualExportFilePath);
                startRecordingOnPlay = false;
            }
#endif
        }

        if (LS == null) return;

        // 1) Sincroniza parámetros de LS antes de analizar el audio
        LS.threshold = threshold;
        LS.smoothness = smoothness;
        LS.UpdateFrequencyBins(vocalTractFactor);

        // 2) Analiza audio (espectro/lipsync)
        LS.UpdateLs();

        // --- FIN HEAD MOTION (lógica movida a LateUpdate) ---

#if UNITY_EDITOR
        if (spectrumVisualization)
            AdjustmentVisualization();
#endif

        // 3) Si aún no tenemos los grupos de BS, no podemos aplicar ni grabar
        if (BSToFind == null || BSToFind.Length < 7)
            return;

        // 4) Actualiza influencias (factores) por grupo ANTES de aplicar blendshapes
        BSToFind[0].influence = kissFactor;
        BSToFind[1].influence = lipsClosedFactor;
        BSToFind[2].influence = mouthOpenFactor;
        BSToFind[3].influence = eyeCloseFactor;
        BSToFind[4].influence = eyeCloseFactor;
        BSToFind[5].influence = eyebrowFactor;
        BSToFind[6].influence = eyebrowFactor;

        // 5) Aplica blendshapes (esto debe rellenar appliedGroupWeights[0..2] para labios)
        UpdateBlendShapes();

        // 6) Grabación de 7 grupos (0..6)
        if (isRecording)
        {
            float t = Time.time - recordingStartTime;

            // Labios (0..2): usa el peso APLICADO; si faltara, fallback a LS*100*influence
            for (int i = 0; i < 3; i++)
            {
                float value = (appliedGroupWeights != null && appliedGroupWeights.Length > i)
                              ? appliedGroupWeights[i]
                              : LS.LSbsw[i] * 100f * BSToFind[i].influence;

                lipsyncKeyframes[i].Add(new Keyframe(t, value));
            }

            // Ojos/Cejas (3..6): siempre desde appliedGroupWeights (se actualiza en SetManualGroupWeight)
            for (int i = 3; i < 7; i++)
            {
                float value = (appliedGroupWeights != null && appliedGroupWeights.Length > i)
                              ? appliedGroupWeights[i]
                              : 0f;

                lipsyncKeyframes[i].Add(new Keyframe(t, value));
            }
        }
    }


    // El movimiento de cabeza se aplica en LateUpdate para ganar la "pelea" con el Animator
    void LateUpdate()
    {
        if (systemsActivated && enableHeadMotion && headTarget != null)
        {
            ApplyHeadMotion();
        }
    }

    private void ApplyHeadMotion()
    {
        // 2.1) Nivel de habla: mezcla bandas medias (o podrías usar LS.LSbsw[2] como "mouth open")
        float speechLevel = Mathf.Clamp01(0.6f * LS.energies[2] + 0.4f * LS.energies[1]);

        // 2.2) Detección de subida rápida -> impulso (sílabas)
        float d = (speechLevel - prevSpeechLevel) / Mathf.Max(Time.deltaTime, 1e-4f);
        if (Time.time - lastSyllableTime > syllableCooldown && d > 0.35f && speechLevel > 0.12f)
        {
            impulseT = 0f;
            lastSyllableTime = Time.time;
        }
        prevSpeechLevel = speechLevel;

        // 2.3) Base rítmica acoplada a energía
        headPhase += Time.deltaTime * reactiveFreq * (0.3f + speechLevel);
        float baseYaw = Mathf.Sin(headPhase) * headYawDeg * (reactiveAmount * speechLevel);
        float basePitch = Mathf.Sin(headPhase * 0.9f + 0.4f) * headPitchDeg * (reactiveAmount * speechLevel * 0.7f);
        float baseRoll = Mathf.Sin(headPhase * 1.1f - 0.2f) * headRollDeg * (reactiveAmount * speechLevel * 0.4f);

        // 2.4) Impulso breve (sílabas)
        float impPitch = 0f, impYaw = 0f;
        if (impulseT >= 0f)
        {
            float v = headImpulseCurve.Evaluate(impulseT); // 0..1
            impPitch = v * syllableImpulse * headPitchDeg;
            // alterna sutilmente la dirección del yaw entre sílabas
            impYaw = v * syllableImpulse * 0.5f * headYawDeg *
                       ((Mathf.FloorToInt(lastSyllableTime * 10f) % 2 == 0) ? 1f : -1f);
            impulseT += Time.deltaTime / 0.15f; // ~150 ms
            if (impulseT >= 1f) impulseT = -1f;
        }

        // 2.5) Micro-ruido Perlin
        float nt = Time.time * noiseFreq;
        float nYaw = (Mathf.PerlinNoise(nt, 0.13f) - 0.5f) * 2f * noiseAmpDeg;
        float nPitch = (Mathf.PerlinNoise(0.37f, nt) - 0.5f) * 2f * noiseAmpDeg;
        float nRoll = (Mathf.PerlinNoise(nt, nt) - 0.5f) * 2f * (noiseAmpDeg * 0.6f);

        // 2.6) Suma final y aplicar (Euler local X=pitch, Y=yaw, Z=roll)
        float yaw = baseYaw + impYaw + nYaw;
        float pitch = basePitch + impPitch + nPitch;
        float roll = baseRoll + nRoll;

        headTarget.localRotation = headRestLocalRot * Quaternion.Euler(pitch, yaw, roll);

        // 2.7) Si estamos grabando, muestrear ángulos para las curvas de cabeza
        if (isRecording)
        {
            float kt = Time.time - recordingStartTime;
            if (headYawKf != null) headYawKf.Add(new Keyframe(kt, yaw));
            if (headPitchKf != null) headPitchKf.Add(new Keyframe(kt, pitch));
            if (headRollKf != null) headRollKf.Add(new Keyframe(kt, roll));
        }
    }


    // ----------------- Exportación -----------------

    /// <summary>
    /// Lo llama el Editor: guarda la ruta y encola el inicio de la grabación al entrar en Play (tras el delay).
    /// </summary>
    public void PrepareExportPath(string filePath)
    {
        manualExportFilePath = filePath;
        startRecordingOnPlay = true;
        Debug.Log("Ruta de exportación preparada: " + manualExportFilePath);
    }

    /// <summary>
    /// Arranca la grabación ahora (si ya pasó el delay); si no, la deja encolada.
    /// </summary>
    public void StartLipsyncExport(string filePath)
    {
        if (isRecording)
        {
            Debug.LogWarning("Ya hay una grabación de lipsync en curso.");
            return;
        }

        if (audioInput == null) audioInput = GetComponent<AudioSource>();
        if (audioInput == null || audioInput.clip == null)
        {
            Debug.LogError("No hay AudioSource o no tiene clip. No se puede grabar.");
            return;
        }

#if UNITY_EDITOR
        // Si venimos de un clic de botón en el Editor, pedimos ruta si no hay
        if (string.IsNullOrEmpty(filePath) && string.IsNullOrEmpty(manualExportFilePath))
        {
            filePath = EditorUtility.SaveFilePanelInProject(
                "Save Lipsync Animation",
                "LipsyncExport.anim",
                "anim",
                "Choose a file name and location inside the project."
            );
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("No se seleccionó ninguna ruta de exportación.");
                return;
            }
        }
#endif

        if (!string.IsNullOrEmpty(filePath))
            manualExportFilePath = filePath;

        // Si aún no han pasado los segundos de delay, encola el arranque
        if (!systemsActivated)
        {
            startRecordingOnPlay = true;
            Debug.Log("Grabación encolada: empezará tras el delay inicial.");
            return;
        }

        // Arranca audio y buffers de grabación
        audioInput.time = 0f;
        audioInput.Play();

        isRecording = true;
        recordingStartTime = Time.time;

        // <-- AHORA inicializamos 7 listas (0..6)
        for (int i = 0; i < 7; i++)
            lipsyncKeyframes[i] = new List<Keyframe>();

        // Curvas de cabeza (yaw/pitch/roll)
        headYawKf = new List<Keyframe>();
        headPitchKf = new List<Keyframe>();
        headRollKf = new List<Keyframe>();

        // Opcional: resetea valores aplicados
        for (int i = 0; i < appliedGroupWeights.Length; i++)
            appliedGroupWeights[i] = 0f;

        Debug.Log("Lipsync recording started.");
    }


    public void StopAndExportLipsync()
    {
        if (!isRecording)
        {
            Debug.LogWarning("No hay grabación activa. Nada que exportar.");
            return;
        }

        isRecording = false;

        if (audioInput != null && audioInput.isPlaying)
            audioInput.Stop();

#if UNITY_EDITOR
        Debug.Log("Lipsync recording stopped. Generating animation file...");

        // Crea el clip (30 fps como en tu versión)
        AnimationClip clip = new AnimationClip { frameRate = 30 };

        // --- 1) Curvas de BLENDSHAPES (labios, ojos, cejas) ---
        if (BSToFind != null)
        {
            // Recorremos 0..6 (labios 0..2, ojos 3..4, cejas 5..6)
            for (int i = 0; i < 7; i++)
            {
                if (lipsyncKeyframes[i] == null || lipsyncKeyframes[i].Count == 0)
                    continue;

                // La curva ya está en unidades "peso aplicado" (0..100 * factor de grupo)
                AnimationCurve curve = new AnimationCurve(lipsyncKeyframes[i].ToArray());
                BSReference BSRef = BSToFind[i];

                for (int m = 0; m < BSRef.meshes.Count; m++)
                {
                    var smr = BSRef.meshes[m];
                    var mesh = smr != null ? smr.sharedMesh : null;
                    if (mesh == null) continue;

                    int bsIndex = BSRef.BSIndices[m];
                    if (bsIndex < 0 || bsIndex >= mesh.blendShapeCount) continue;

                    string bsName = mesh.GetBlendShapeName(bsIndex);
                    // Calcula la ruta relativa al root del script (donde está el SMR)
                    string path = AnimationUtility.CalculateTransformPath(smr.transform, transform);
                    string prop = "blendShape." + bsName;

                    clip.SetCurve(path, typeof(SkinnedMeshRenderer), prop, curve);
                }
            }
        }

        // --- 2) Curvas de CABEZA (Yaw/Pitch/Roll en grados locales) ---
        // Usa el headAnimationRoot para calcular la ruta correcta.
        if (headTarget != null && headAnimationRoot != null && headYawKf != null && headYawKf.Count > 0)
        {
            string hPath = AnimationUtility.CalculateTransformPath(headTarget, headAnimationRoot);

            var yawCurve = new AnimationCurve(headYawKf.ToArray());   // Y (izq-der)
            var pitchCurve = new AnimationCurve(headPitchKf.ToArray()); // X (arriba-abajo)
            var rollCurve = new AnimationCurve(headRollKf.ToArray());  // Z (ladeo)

            // Usamos localEulerAnglesRaw para evitar normalizaciones indeseadas
            clip.SetCurve(hPath, typeof(Transform), "localEulerAnglesRaw.y", yawCurve);   // Yaw
            clip.SetCurve(hPath, typeof(Transform), "localEulerAnglesRaw.x", pitchCurve); // Pitch
            clip.SetCurve(hPath, typeof(Transform), "localEulerAnglesRaw.z", rollCurve);  // Roll
        }
        else if (headTarget != null && headAnimationRoot == null)
        {
            Debug.LogWarning("No se asignó 'Head Animation Root'. La animación de cabeza no se exportará.");
        }

        // --- 3) Ruta de salida (pide si no existe) ---
        if (string.IsNullOrEmpty(manualExportFilePath))
        {
            string rel = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Save Lipsync Animation",
                "LipsyncExport.anim",
                "anim",
                "Choose a file name and location inside the project."
            );
            if (string.IsNullOrEmpty(rel))
            {
                Debug.LogError("Ruta de exportación vacía.");
                return;
            }
            manualExportFilePath = rel;
        }

        // Garantiza nombre único y guarda el asset
        manualExportFilePath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(manualExportFilePath);
        UnityEditor.AssetDatabase.CreateAsset(clip, manualExportFilePath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("Lipsync exportado a: " + manualExportFilePath);

        // Opcional: salir de Play al terminar (como en tu flujo actual)
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Debug.LogError("La exportación de animación solo es compatible en el Editor de Unity.");
#endif
    }
    // Dentro de ThreeLSControl.cs, en la clase ThreeLSControl
    public void CaptureHeadRestNow()
    {
        if (headTarget != null)
        {
            headRestLocalRot = headTarget.localRotation;
            Debug.Log("[ThreeLSControl] Head rest local rotation captured from current pose.");
        }
        else
        {
            Debug.LogWarning("[ThreeLSControl] Cannot capture head rest: headTarget is null.");
        }
    }

    // Este método se ejecutará cuando se detenga la reproducción en el Editor
    private void OnApplicationQuit()
    {
        if (exportOnQuit && isRecording)
        {
            // Backup por si se para bruscamente
            StopAndExportLipsync();
        }
    }

    // ----------------- Lógica existente de aplicación de blendshapes -----------------

    private void UpdateBlendShapes()
    {
        // Fade out si el audio se para (solo lipsync 0..2)
        if (!audioInput.isPlaying && wasPlaying)
        {
            wasPlaying = false;
            fadingOut = true;
            timeToFadeOut = Time.time + 1.0f;
        }
        if (!audioInput.isPlaying && fadingOut)
        {
            FadeOutLips();
            return;
        }
        else if (!audioInput.isPlaying)
            return;

        // Aplica los TRES grupos de lipsync 0..2
        for (int i = 0; i < 3; i++)
        {
            BSReference BSRef = BSToFind[i];
            float BSWeight = LS.LSbsw[i] * 100.0f * BSRef.influence;
            // Guarda el peso realmente aplicado para que la grabación coincida 1:1
            appliedGroupWeights[i] = BSWeight;

            for (int m = 0; m < BSRef.meshes.Count; m++)
                BSRef.meshes[m].SetBlendShapeWeight(BSRef.BSIndices[m], BSWeight);
        }


        // Grupos manuales 3..6 (ojos/cejas L/R) se animan en coroutines
        wasPlaying = true;
    }

    private void AdjustmentVisualization()
    {
        var spectrum = LS.smoothSpectrum;
        for (int i = 1; i < spectrum.Length - 1; i++)
        {
            float vv = threshold + (spectrum[i] + 20) / 140;
            float vv_p = threshold + (spectrum[i - 1] + 20) / 140;
            Debug.DrawLine(new Vector3((i - 1) / 200.0f + 1.0f, vv_p, 0), new Vector3(i / 200.0f + 1.0f, vv, 0), Color.cyan);
        }
        Debug.DrawLine(new Vector3(1.0f, 0, 0), new Vector3(spectrum.Length / 200.0f + 1.0f, 0, 0), Color.white);
        for (int bindInd = 0; bindInd < LS.freqBins.Length - 1; bindInd++)
        {
            int indxIn = Mathf.RoundToInt(LS.freqBins[bindInd] * (LS.fftSize / 2) / (LS.fs / 2));
            int indxEnd = Mathf.RoundToInt(LS.freqBins[bindInd + 1] * (LS.fftSize / 2) / (LS.fs / 2));
            Debug.DrawLine(new Vector3(indxIn / 200.0f + 1.0f, 0.5f, 0), new Vector3(indxIn / 200.0f + 1.0f, -0.5f, 0), Color.red);
            Debug.DrawLine(new Vector3(indxEnd / 200.0f + 1.0f, 0.5f, 0), new Vector3(indxEnd / 200.0f + 1.0f, -0.5f, 0), Color.red);
        }
        Debug.DrawLine(new Vector3(1.0f, 1.0f, 0.0f), new Vector3(1.0f, LS.energies[1] + 1.0f, 0.0f), Color.green);
        Debug.DrawLine(new Vector3(1.2f, 1.0f, 0.0f), new Vector3(1.2f, LS.energies[2] + 1.0f, 0.0f), Color.green);
        Debug.DrawLine(new Vector3(1.4f, 1.0f, 0.0f), new Vector3(1.4f, LS.energies[3] + 1.0f, 0.0f), Color.green);
        Debug.DrawLine(new Vector3(2.0f, 1.0f, 0.0f), new Vector3(2.0f, LS.LSbsw[0] + 1.0f, 0.0f), Color.blue);
        Debug.DrawLine(new Vector3(2.2f, 1.0f, 0.0f), new Vector3(2.2f, LS.LSbsw[1] + 1.0f, 0.0f), Color.blue);
        Debug.DrawLine(new Vector3(2.4f, 1.0f, 0.0f), new Vector3(2.4f, LS.LSbsw[2] + 1.0f, 0.0f), Color.blue);
    }

    private void FindBlendShapes()
    {
        // 0: Kiss, 1: LipsClosed, 2: MouthOpen, 3: EyeCloseL, 4: EyeCloseR, 5: EyebrowL, 6: EyebrowR
        BSToFind = new BSReference[7];
        BSToFind[0] = new BSReference(kissBlendShapeNames.ToArray());
        BSToFind[1] = new BSReference(lipsClosedBlendShapeNames.ToArray());
        BSToFind[2] = new BSReference(mouthOpenBlendShapeNames.ToArray());
        BSToFind[3] = new BSReference(eyeCloseLBlendShapeNames.ToArray());
        BSToFind[4] = new BSReference(eyeCloseRBlendShapeNames.ToArray());
        BSToFind[5] = new BSReference(eyebrowLBlendShapeNames.ToArray());
        BSToFind[6] = new BSReference(eyebrowRBlendShapeNames.ToArray());

        // Ahora usa GetComponentsInChildren para buscar en toda la jerarquía
        var skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();

        for (int m = 0; m < skinnedMeshes.Length; m++)
        {
            var smr = skinnedMeshes[m];
            if (smr && smr.sharedMesh.blendShapeCount != 0)
            {
                var mesh = smr.sharedMesh;
                for (var j = 0; j < mesh.blendShapeCount; j++)
                {
                    string bsName = mesh.GetBlendShapeName(j);
                    for (int k = 0; k < BSToFind.Length; k++)
                    {
                        for (int ss = 0; ss < BSToFind[k].BSNames.Length; ss++)
                        {
                            if (string.Equals(BSToFind[k].BSNames[ss], bsName))
                            {
                                BSToFind[k].meshes.Add(smr);
                                BSToFind[k].BSIndices.Add(j);
                                BSToFind[k].BSNameIsFound[ss] = true;
                            }
                        }
                    }
                }
            }
        }

        for (int k = 0; k < BSToFind.Length; k++)
        {
            for (int ss = 0; ss < BSToFind[k].BSNames.Length; ss++)
            {
                if (!BSToFind[k].BSNameIsFound[ss])
                    Debug.LogError("Blend Shape " + BSToFind[k].BSNames[ss] + " not found in children!");
            }
        }
    }

    private void FadeOutLips()
    {
        for (var i = 0; i < 3; i++)
        {
            BSReference BSRef = BSToFind[i];
            for (int m = 0; m < BSRef.meshes.Count; m++)
            {
                var indx = BSRef.BSIndices[m];
                var weight = BSRef.meshes[m].GetBlendShapeWeight(indx) * (timeToFadeOut - Time.time);
                BSRef.meshes[m].SetBlendShapeWeight(indx, weight);
            }
        }
        if (timeToFadeOut < Time.time)
        {
            fadingOut = false;
            for (var i = 0; i < 3; i++)
            {
                BSReference BSRef = BSToFind[i];
                for (int m = 0; m < BSRef.meshes.Count; m++)
                {
                    var indx = BSRef.BSIndices[m];
                    BSRef.meshes[m].SetBlendShapeWeight(indx, 0);
                }
            }
        }
    }


    private void SetManualGroupWeight(int bstIndex, float weight01)
    {
        weight01 = Mathf.Clamp01(weight01);
        BSReference BSRef = BSToFind[bstIndex];

        float factor = (bstIndex == 3 || bstIndex == 4) ? eyeCloseFactor : eyebrowFactor;
        float BSWeight = weight01 * 100f * factor;

        // <-- NUEVO: recuerda el peso APLICADO para exportar
        appliedGroupWeights[bstIndex] = BSWeight;

        for (int m = 0; m < BSRef.meshes.Count; m++)
            BSRef.meshes[m].SetBlendShapeWeight(BSRef.BSIndices[m], BSWeight);
    }


    private IEnumerator AutoBlinkAndBrow()
    {
        // Espera a que BSToFind esté listo
        while (BSToFind == null || BSToFind.Length < 7) yield return null;

        // Espera al delay inicial antes de parpadeos/cejas
        while (!systemsActivated) yield return null;

        if (blinkIntervalRange.x < 0.05f) blinkIntervalRange.x = 0.05f;
        if (blinkIntervalRange.y < blinkIntervalRange.x) blinkIntervalRange.y = blinkIntervalRange.x + 0.01f;

        while (true)
        {
            float wait = Random.Range(blinkIntervalRange.x, blinkIntervalRange.y);
            yield return new WaitForSeconds(wait);
            if (enableAutoBlink)
                yield return BlinkOnceSimultaneous();
            if (enableAutoBrow && Random.value <= browChancePerBlink)
                yield return BrowPulseOnceAsym();
        }
    }

    private IEnumerator BlinkOnceSimultaneous()
    {
        float duration = blinkDuration;
        float startDelay = 0f;
        Coroutine leftBlink = StartCoroutine(AnimateBlinkSide(3, duration, startDelay));
        Coroutine rightBlink = StartCoroutine(AnimateBlinkSide(4, duration, startDelay));
        yield return leftBlink;
        yield return rightBlink;
        SetManualGroupWeight(3, 0f);
        SetManualGroupWeight(4, 0f);
    }

    private IEnumerator AnimateBlinkSide(int bstIndex, float duration, float startDelay)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        float t = 0f;
        while (t < duration)
        {
            float v = blinkCurve.Evaluate(t / duration);
            SetManualGroupWeight(bstIndex, v);
            t += Time.deltaTime;
            yield return null;
        }
        SetManualGroupWeight(bstIndex, 1f);
        t = 0f;
        while (t < duration)
        {
            float v = blinkCurve.Evaluate(1f - (t / duration));
            SetManualGroupWeight(bstIndex, v);
            t += Time.deltaTime;
            yield return null;
        }
        SetManualGroupWeight(bstIndex, 0f);
    }

    private IEnumerator BrowPulseOnceAsym()
    {
        bool leftFirst = Random.value < 0.5f;
        float dL = browPulseDuration * (1f + Random.Range(-browPerSideDurationJitter, browPerSideDurationJitter));
        float dR = browPulseDuration * (1f + Random.Range(-browPerSideDurationJitter, browPerSideDurationJitter));
        float delayL = Mathf.Max(0f, Random.Range(-browPerSideStartJitter, browPerSideStartJitter));
        float delayR = Mathf.Max(0f, Random.Range(-browPerSideStartJitter, browPerSideStartJitter));

        int first = leftFirst ? 5 : 6;
        int second = leftFirst ? 6 : 5;

        var running = new List<Coroutine>();
        running.Add(StartCoroutine(AnimateBrowSide(first, leftFirst ? dL : dR, leftFirst ? delayL : delayR)));
        running.Add(StartCoroutine(AnimateBrowSide(second, leftFirst ? dR : dL, leftFirst ? delayR : delayL)));
        foreach (var c in running) yield return c;

        SetManualGroupWeight(5, 0f);
        SetManualGroupWeight(6, 0f);
    }

    private IEnumerator AnimateBrowSide(int bstIndex, float duration, float startDelay)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        float half = Mathf.Max(0.01f, duration * 0.5f);
        float t = 0f;
        while (t < half)
        {
            float v = browCurve.Evaluate(t / half) * browAmount;
            SetManualGroupWeight(bstIndex, v);
            t += Time.deltaTime;
            yield return null;
        }
        SetManualGroupWeight(bstIndex, browAmount);
        t = 0f;
        while (t < half)
        {
            float v = browCurve.Evaluate(1f - (t / half)) * browAmount;
            SetManualGroupWeight(bstIndex, v);
            t += Time.deltaTime;
            yield return null;
        }
        SetManualGroupWeight(bstIndex, 0f);
    }
}