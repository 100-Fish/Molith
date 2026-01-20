using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AudioEventTrigger))]
public class AudioEventTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("soundID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isLooping"));

        EditorGUILayout.Space();

        if (Application.isPlaying && GUILayout.Button("Test Sound"))
        {
            AudioEventTrigger trigger = (AudioEventTrigger)target;
            trigger.TriggerSound();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Test Sound button only works in Play Mode", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
