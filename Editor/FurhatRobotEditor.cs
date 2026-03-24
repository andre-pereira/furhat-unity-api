using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Furhat.Runtime;

[CustomEditor(typeof(FurhatRobot))]
public class FurhatRobotEditor : Editor {
    private FurhatRobot _robot;
    private SerializedProperty _ipAddress;
    private SerializedProperty _authenticationKey;
    private SerializedProperty _connectOnStart;
    private SerializedProperty _enableLogging;
    private SerializedProperty _startWithVideoLogging;
    private SerializedProperty _startWithAudioLoggingMode;
    private SerializedProperty _startWithUserDataLogging;
    private SerializedProperty _logRootDirectory;
    private SerializedProperty _voiceProviderFilter;
    private SerializedProperty _voiceLanguageFilter;
    private SerializedProperty _voiceGenderFilter;
    private SerializedProperty _voiceId;
    private SerializedProperty _faceModel;
    private SerializedProperty _ledColorHex;
    private SerializedProperty _faceVisibility;
    private SerializedProperty _microexpressions;
    private SerializedProperty _blinking;
    private SerializedProperty _headSway;
    private SerializedProperty _listenLanguages;
    private SerializedProperty _listenPhrases;
    private SerializedProperty _attendTarget;
    private SerializedProperty _attendLocation;
    private SerializedProperty _attendUserId;
    private SerializedProperty _attendSpeed;
    private SerializedProperty _basicFaceParameters;
    private SerializedProperty _arkitFaceParameters;
    private SerializedProperty _charFaceParameters;
    private SerializedProperty _isSpeaking;
    private SerializedProperty _isListening;
    private SerializedProperty _isGesturing;
    private SerializedProperty _lastSpokenText;
    private SerializedProperty _lastHeardText;
    private SerializedProperty _lastGesturePlayed;
    private SerializedProperty _currentAttendUserId;
    private SerializedProperty _availableVoices;
    private SerializedProperty _availableFaceModels;

    private bool _showBasicParams;
    private bool _showArkitParams;
    private bool _showCharParams;
    private void OnEnable() {
        _robot = (FurhatRobot)target;
        _robot.LoadPersistedStatusCache();
        _robot.EnsureFaceParameterDefinitions();
        CacheProperties();
        _robot.OnInspectorStateChanged += HandleInspectorStateChanged;
    }

    private void OnDisable() {
        if (_robot != null) {
            _robot.OnInspectorStateChanged -= HandleInspectorStateChanged;
        }
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();

        DrawConnectionSection();
        GUILayout.Space(6f);
        DrawLiveStatusSection();
        GUILayout.Space(6f);
        DrawLoggingSection();
        GUILayout.Space(6f);
        DrawTextToSpeechSection();
        GUILayout.Space(6f);
        DrawFaceConfigurationSection();
        GUILayout.Space(6f);
        DrawLedSection();
        GUILayout.Space(6f);
        DrawListenSection();
        GUILayout.Space(6f);
        DrawAttentionSection();
        GUILayout.Space(6f);
        DrawFaceParametersSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawConnectionSection() {
        EditorGUILayout.LabelField("Connection Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_ipAddress);
        EditorGUILayout.PropertyField(_authenticationKey);
        EditorGUILayout.PropertyField(_connectOnStart);
    }

    private void DrawLoggingSection() {
        EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enableLogging, new GUIContent("Enable Logging"));
        using (new EditorGUI.DisabledScope(!_enableLogging.boolValue)) {
            EditorGUILayout.PropertyField(_startWithVideoLogging);
            EditorGUILayout.PropertyField(_startWithAudioLoggingMode);
            EditorGUILayout.PropertyField(_startWithUserDataLogging);
        }
        EditorGUILayout.PropertyField(_logRootDirectory);
        if (GUILayout.Button("Open Log Directory")) {
            EditorUtility.RevealInFinder(_robot.LogRootDirectory);
        }
    }

    private void DrawLiveStatusSection() {
        using (new EditorGUI.DisabledScope(true)) {
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Toggle(_isSpeaking.boolValue, "Speaking", EditorStyles.miniButton);
                GUILayout.Toggle(_isListening.boolValue, "Listening", EditorStyles.miniButton);
                GUILayout.Toggle(_isGesturing.boolValue, "Gesturing", EditorStyles.miniButton);
            }

            EditorGUILayout.TextField("Last Spoken", _lastSpokenText.stringValue);
            EditorGUILayout.TextField("Last Heard", _lastHeardText.stringValue);
            EditorGUILayout.TextField("Last Gesture", _lastGesturePlayed.stringValue);
        }
    }

    private void DrawTextToSpeechSection() {
        EditorGUILayout.LabelField("Text-to-Speech Configuration", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        DrawVoiceFilterPopup(_voiceProviderFilter, "Voice Provider Filter", GetDistinctVoiceValues("provider"));
        DrawVoiceFilterPopup(_voiceLanguageFilter, "Voice Language Filter", GetDistinctVoiceValues("language"));
        DrawVoiceFilterPopup(_voiceGenderFilter, "Voice Gender Filter", GetDistinctVoiceValues("gender"));
        DrawVoiceIdControl();
        bool voiceChanged = EditorGUI.EndChangeCheck();

        if (GUILayout.Button("Get Voice List")) {
            _ = _robot.RefreshVoiceStatusAsync();
        }

        if (voiceChanged) {
            serializedObject.ApplyModifiedProperties();
            LiveApply(() => _robot.ApplyVoiceConfigAsync());
        }
    }

    private void DrawFaceConfigurationSection() {
        EditorGUILayout.LabelField("Face Configuration", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        DrawFaceModelControl();
        EditorGUILayout.PropertyField(_faceVisibility);
        EditorGUILayout.PropertyField(_microexpressions);
        EditorGUILayout.PropertyField(_blinking);
        EditorGUILayout.PropertyField(_headSway);
        bool faceChanged = EditorGUI.EndChangeCheck();

        if (GUILayout.Button("Get Face List")) {
            _ = _robot.RefreshFaceStatusAsync();
        }

        if (faceChanged) {
            serializedObject.ApplyModifiedProperties();
            LiveApply(() => _robot.ApplyFaceConfigAsync());
        }
    }

    private void DrawLedSection() {
        EditorGUILayout.LabelField("LED", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_ledColorHex, new GUIContent("Color Hex"));
        if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
            LiveApply(() => _robot.ApplyLedAsync());
        }
    }

    private void DrawListenSection() {
        EditorGUILayout.LabelField("Listen Configuration", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_listenLanguages, new GUIContent("Languages"));
        EditorGUILayout.PropertyField(_listenPhrases, new GUIContent("Boosted Phrases"));
        if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
            LiveApply(() => _robot.ApplyListenConfigAsync());
        }
    }

    private void DrawAttentionSection() {
        EditorGUILayout.LabelField("Attention", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_attendTarget);

        var attendTargetValue = (FurhatAttendTarget)_attendTarget.enumValueIndex;
        if (attendTargetValue == FurhatAttendTarget.Location) {
            EditorGUILayout.PropertyField(_attendLocation, new GUIContent("Location"));
        }

        if (attendTargetValue == FurhatAttendTarget.UserId) {
            EditorGUILayout.PropertyField(_attendUserId, new GUIContent("User Id"));
        }

        EditorGUILayout.PropertyField(_attendSpeed, new GUIContent("Speed"));

        using (new EditorGUI.DisabledScope(true)) {
            if (!string.IsNullOrWhiteSpace(_currentAttendUserId.stringValue)) {
                EditorGUILayout.TextField("Current User Id", _currentAttendUserId.stringValue);
            }
        }

        if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
            LiveApply(() => _robot.ApplyAttendTargetAsync());
        }
    }

    private void DrawFaceParametersSection() {
        EditorGUILayout.LabelField("Face Parameters", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _showBasicParams = EditorGUILayout.Foldout(_showBasicParams, "furhatos.gestures.BasicParams", true);
        if (_showBasicParams) {
            DrawFaceParameterList(_basicFaceParameters);
        }

        _showArkitParams = EditorGUILayout.Foldout(_showArkitParams, "furhatos.gestures.ARKitParams", true);
        if (_showArkitParams) {
            DrawFaceParameterList(_arkitFaceParameters);
        }

        _showCharParams = EditorGUILayout.Foldout(_showCharParams, "furhatos.gestures.CharParams", true);
        if (_showCharParams) {
            DrawFaceParameterList(_charFaceParameters);
        }

        if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
            LiveApply(() => _robot.ApplyFaceParametersAsync());
        }

        if (GUILayout.Button("Reset Parameters")) {
            _ = _robot.ResetFaceParametersAsync();
            serializedObject.Update();
        }
    }

    private void DrawVoiceIdControl() {
        var filteredVoices = _robot.GetFilteredVoices().ToList();
        if (filteredVoices.Count == 0) {
            EditorGUILayout.PropertyField(_voiceId);
            return;
        }

        var ids = filteredVoices.Select(v => v.voiceId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!string.IsNullOrWhiteSpace(_voiceId.stringValue) && !ids.Contains(_voiceId.stringValue)) {
            ids.Insert(0, _voiceId.stringValue);
        }

        int selectedIndex = Mathf.Max(0, ids.FindIndex(id => string.Equals(id, _voiceId.stringValue, StringComparison.Ordinal)));
        int newIndex = EditorGUILayout.Popup("Voice Id", selectedIndex, ids.ToArray());
        _voiceId.stringValue = ids[newIndex];

    }

    private void DrawFaceModelControl() {
        var models = Enumerable.Range(0, _availableFaceModels.arraySize)
            .Select(i => _availableFaceModels.GetArrayElementAtIndex(i).stringValue)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (models.Count == 0) {
            EditorGUILayout.PropertyField(_faceModel);
            return;
        }

        if (!models.Contains(_faceModel.stringValue)) {
            models.Insert(0, _faceModel.stringValue);
        }

        int selectedIndex = Mathf.Max(0, models.FindIndex(v => string.Equals(v, _faceModel.stringValue, StringComparison.Ordinal)));
        int newIndex = EditorGUILayout.Popup("Face Model", selectedIndex, models.ToArray());
        _faceModel.stringValue = models[newIndex];
    }

    private static void DrawFaceParameterList(SerializedProperty property) {
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++) {
            var item = property.GetArrayElementAtIndex(i);
            var name = item.FindPropertyRelative("name");
            var value = item.FindPropertyRelative("value");
            value.floatValue = EditorGUILayout.FloatField(name.stringValue, value.floatValue);
        }
        EditorGUI.indentLevel--;
    }

    private void LiveApply(Func<System.Threading.Tasks.Task> apply) {
        if (!Application.isPlaying || !_robot.IsConnected) return;
        _ = apply();
    }

    private void DrawVoiceFilterPopup(SerializedProperty property, string label, List<string> values) {
        var options = new List<string> { "Any" };
        options.AddRange(values);

        string currentValue = string.IsNullOrWhiteSpace(property.stringValue) ? "Any" : property.stringValue;
        if (!options.Contains(currentValue)) {
            options.Add(currentValue);
        }

        int index = Mathf.Max(0, options.FindIndex(v => string.Equals(v, currentValue, StringComparison.Ordinal)));
        int newIndex = EditorGUILayout.Popup(label, index, options.ToArray());
        property.stringValue = newIndex <= 0 ? "" : options[newIndex];
    }

    private List<string> GetDistinctVoiceValues(string fieldName) {
        var values = new List<string>();
        for (int i = 0; i < _availableVoices.arraySize; i++) {
            var item = _availableVoices.GetArrayElementAtIndex(i);
            string value = item.FindPropertyRelative(fieldName).stringValue;
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value)) {
                values.Add(value);
            }
        }

        values.Sort(StringComparer.OrdinalIgnoreCase);
        return values;
    }

    private void HandleInspectorStateChanged() {
        if (this == null) return;
        Repaint();
    }

    private void CacheProperties() {
        _ipAddress = serializedObject.FindProperty("ipAddress");
        _authenticationKey = serializedObject.FindProperty("authenticationKey");
        _connectOnStart = serializedObject.FindProperty("connectOnStart");
        _enableLogging = serializedObject.FindProperty("enableLogging");
        _startWithVideoLogging = serializedObject.FindProperty("startWithVideoLogging");
        _startWithAudioLoggingMode = serializedObject.FindProperty("startWithAudioLoggingMode");
        _startWithUserDataLogging = serializedObject.FindProperty("startWithUserDataLogging");
        _logRootDirectory = serializedObject.FindProperty("logRootDirectory");
        _voiceProviderFilter = serializedObject.FindProperty("voiceProviderFilter");
        _voiceLanguageFilter = serializedObject.FindProperty("voiceLanguageFilter");
        _voiceGenderFilter = serializedObject.FindProperty("voiceGenderFilter");
        _voiceId = serializedObject.FindProperty("voiceId");
        _faceModel = serializedObject.FindProperty("faceModel");
        _ledColorHex = serializedObject.FindProperty("ledColorHex");
        _faceVisibility = serializedObject.FindProperty("faceVisibility");
        _microexpressions = serializedObject.FindProperty("microexpressions");
        _blinking = serializedObject.FindProperty("blinking");
        _headSway = serializedObject.FindProperty("headSway");
        _listenLanguages = serializedObject.FindProperty("listenLanguages");
        _listenPhrases = serializedObject.FindProperty("boostedListenPhrases");
        _attendTarget = serializedObject.FindProperty("attendTarget");
        _attendLocation = serializedObject.FindProperty("attendLocation");
        _attendUserId = serializedObject.FindProperty("attendUserId");
        _attendSpeed = serializedObject.FindProperty("attendSpeed");
        _basicFaceParameters = serializedObject.FindProperty("basicFaceParameters");
        _arkitFaceParameters = serializedObject.FindProperty("arkitFaceParameters");
        _charFaceParameters = serializedObject.FindProperty("charFaceParameters");
        _isSpeaking = serializedObject.FindProperty("isSpeaking");
        _isListening = serializedObject.FindProperty("isListening");
        _isGesturing = serializedObject.FindProperty("isGesturing");
        _lastSpokenText = serializedObject.FindProperty("lastSpokenText");
        _lastHeardText = serializedObject.FindProperty("lastHeardText");
        _lastGesturePlayed = serializedObject.FindProperty("lastGesturePlayed");
        _currentAttendUserId = serializedObject.FindProperty("currentAttendUserId");
        _availableVoices = serializedObject.FindProperty("availableVoices");
        _availableFaceModels = serializedObject.FindProperty("availableFaceModels");
    }
}
