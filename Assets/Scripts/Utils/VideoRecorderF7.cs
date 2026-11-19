
using System;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

public class VideoRecorderF7 : MonoBehaviour
{
    private static VideoRecorderF7 instance;

#if UNITY_EDITOR
    private RecorderController controller;
    private bool isRecording = false;
#endif

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f7Key.wasPressedThisFrame)
        {
#if UNITY_EDITOR
            if (isRecording)
                StopRecording();
            else
                StartRecording();
#else
            Debug.LogWarning(
                "La registrazione funziona solo nell'Editor."
            );
#endif
        }
    }

#if UNITY_EDITOR
    void StartRecording()
    {
        string fileName = "Video_" +
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        var settings = ScriptableObject.CreateInstance
            <RecorderControllerSettings>();

        settings.SetRecordModeToManual();
        settings.FrameRate = 30;

        var movie = ScriptableObject.CreateInstance
            <MovieRecorderSettings>();

        movie.name = "Gameplay Recorder";
        movie.Enabled = true;

        movie.OutputFormat =
            MovieRecorderSettings.VideoRecorderOutputFormat.MP4;

        movie.OutputFile = "Recordings/" + fileName;

        movie.ImageInputSettings =
            new GameViewInputSettings();

        settings.AddRecorderSettings(movie);

        controller = new RecorderController(settings);

        controller.PrepareRecording();

        if (controller.StartRecording())
        {
            isRecording = true;
            Debug.Log("🔴 REGISTRAZIONE INIZIATA");
        }
        else
        {
            Debug.LogError(
                "Impossibile avviare la registrazione."
            );
        }
    }

    void StopRecording()
    {
        if (controller != null && isRecording)
        {
            controller.StopRecording();
            isRecording = false;

            Debug.Log("⏹ REGISTRAZIONE TERMINATA");
        }
    }

    void OnDisable()
    {
        if (isRecording)
            StopRecording();
    }
#endif
}
