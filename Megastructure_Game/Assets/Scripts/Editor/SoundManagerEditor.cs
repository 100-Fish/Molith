using UnityEngine;
using UnityEditor;
using System.Reflection;

[CustomEditor(typeof(SoundManager))]
public class SoundManagerEditor : Editor
{
    private bool[] soundFoldouts = new bool[7];

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Ambient Music section
        EditorGUILayout.LabelField("Ambient Music", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientMusic"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("musicPlayer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("musicVolume"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("volumeMultiplier"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound Mappings", EditorStyles.boldLabel);

        SerializedProperty soundMappings = serializedObject.FindProperty("soundMappings");

        for (int i = 0; i < soundMappings.arraySize; i++)
        {
            SerializedProperty mapping = soundMappings.GetArrayElementAtIndex(i);
            SerializedProperty soundID = mapping.FindPropertyRelative("soundID");
            SerializedProperty soundEffect = mapping.FindPropertyRelative("soundEffect");
            SerializedProperty clips = soundEffect.FindPropertyRelative("Clips");
            SerializedProperty volume = soundEffect.FindPropertyRelative("Volume");
            SerializedProperty vary = soundEffect.FindPropertyRelative("Vary");

            // Validation
            bool hasClips = clips.arraySize > 0;
            Color bgColor = hasClips ? new Color(0.8f, 1f, 0.8f) : new Color(1f, 0.8f, 0.8f);

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            soundFoldouts[i] = EditorGUILayout.Foldout(soundFoldouts[i], ((SoundID)soundID.intValue).ToString(), true);

            if (!hasClips)
            {
                GUILayout.Label("⚠", GUILayout.Width(20));
            }

            EditorGUILayout.EndHorizontal();

            if (soundFoldouts[i])
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(soundID);
                EditorGUILayout.PropertyField(clips, true);
                EditorGUILayout.PropertyField(volume);
                EditorGUILayout.PropertyField(vary);

                // Play preview button
                if (hasClips && GUILayout.Button("▶ Play Preview"))
                {
                    AudioClip previewClip = clips.GetArrayElementAtIndex(0).objectReferenceValue as AudioClip;
                    if (previewClip != null)
                        PlayClipPreview(previewClip);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void PlayClipPreview(AudioClip clip)
    {
        // Use reflection to access internal Unity audio preview
        Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
        System.Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
        MethodInfo method = audioUtilClass.GetMethod(
            "PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null
        );
        method?.Invoke(null, new object[] { clip, 0, false });
    }
}
