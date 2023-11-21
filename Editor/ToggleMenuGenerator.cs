using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

public class ToggleMenuGenerator : EditorWindow
{
    
    string animationSavePath = "Assets/KRHa's Assets/ToggleMenuGenerator/Animations";
    int numberOfObjects = 0;
    bool setupMA = true;

    AnimatorController animatorController;

    List<GameObject> selectedObjects = new List<GameObject>();
    List<bool> includeInCombinedAnimation = new List<bool>();
    List<bool> initialActiveState = new List<bool>();

    Dictionary<string, bool> initialStates = new Dictionary<string, bool>();
    Dictionary<string, bool> savedStates = new Dictionary<string, bool>();

    [MenuItem("くろ～は/ToggleMenuGenerator")]
    public static void ShowWindow()
    {
        GetWindow<ToggleMenuGenerator>("ToggleMenuGenerator");
    }

    void OnGUI()
    {
        GUILayout.Label("GameObjectを選択してください", EditorStyles.boldLabel);
        numberOfObjects = EditorGUILayout.IntField("GameObjectの数", numberOfObjects);

        AdjustLists(numberOfObjects);

        for (int i = 0; i < numberOfObjects; i++)
        {
            selectedObjects[i] = EditorGUILayout.ObjectField($"GameObject {i + 1}", selectedObjects[i], typeof(GameObject), true) as GameObject;
            includeInCombinedAnimation[i] = EditorGUILayout.ToggleLeft("アニメーションをまとめる", includeInCombinedAnimation[i]);
            GUILayout.Space(10);
        }

        GUILayout.Space(5);

        GUILayout.Label("保存先を変更(Assets/以下のフォルダにのみ保存できます)\n例: Assets/KRHa's Assets/ToggleMenuGenerator/Animations", EditorStyles.boldLabel);
        animationSavePath = EditorGUILayout.TextField(animationSavePath);

        if (GUILayout.Button("保存先のリセット"))
        {
            animationSavePath = "Assets/KRHa's Assets/ToggleMenuGenerator/Animations";
        }

        GUILayout.Space(10);
        setupMA = EditorGUILayout.ToggleLeft("ModularAvatarでセットアップ", setupMA);
        if (setupMA)
        {
            // 最上位の親オブジェクトが複数あるかチェック
            var rootObjects = selectedObjects
                .Where(obj => obj != null)
                .Select(obj => obj.transform.root.gameObject)
                .Distinct()
                .ToList();

            if (rootObjects.Count > 1) {
                // 複数の最上位の親オブジェクトがある場合は警告を表示
                EditorUtility.DisplayDialog("エラー", "複数のアバターは同時にセットアップできません", "OK");
                setupMA = false;
                return;
            }

            GUILayout.Space(10);

            GUILayout.Label("初期状態の設定\n(チェックを入れるとアクティブ、チェックを外すと非アクティブ)", EditorStyles.boldLabel);
            DisplayInitialStatesUI();

            GUILayout.Space(10);

            GUILayout.Label("Parametersの値を保存するか否かの設定\n(VRCExpressionParametersの'Saved'の部分)", EditorStyles.boldLabel);
            DisplaySavedStatesUI();
        }
            
        GUILayout.Space(5);

        if (GUILayout.Button("セットアップ！"))
        {
            // 保存先のチェック
            if (!Directory.Exists(animationSavePath))
            {
                EditorUtility.DisplayDialog("エラー", "指定された保存先が存在しません。", "OK");
                return;
            }

            CreateCombinedAnimation();

            foreach (var obj in selectedObjects)
            {
                if (obj != null && !includeInCombinedAnimation[selectedObjects.IndexOf(obj)])
                {
                    CreateAnimation(new GameObject[] { obj }, true, obj.name);
                    CreateAnimation(new GameObject[] { obj }, false, obj.name);
                }
            }
            if (setupMA)
            {
                CreateAnimatorController();

                string combinedObjectName = string.Join("_", selectedObjects.Where(obj => obj != null).Select(obj => obj.name));
                DuplicateBaseFolder(combinedObjectName);
                
                string newFolderPath = $"{animationSavePath}/TMG_Base_{combinedObjectName}";
                UpdateParamAndMenu(newFolderPath, animatorController);

                CreateMAComponents(combinedObjectName, newFolderPath);
            }
        }
    }

    // 初期状態チェックボックスの処理
    void DisplayInitialStatesUI()
    {
        // 結合されるオブジェクトの初期状態チェックボックス
        var combinedNames = selectedObjects
            .Where((obj, index) => obj != null && includeInCombinedAnimation[index])
            .Select(obj => obj.name).ToList();

        if (combinedNames.Any())
        {
            string combinedName = string.Join("_", combinedNames);
            EnsureStateExistence(combinedName);
            initialStates[combinedName] = EditorGUILayout.ToggleLeft($"{combinedName}の初期状態", initialStates[combinedName]);
        }

        // 個別のオブジェクトの初期状態チェックボックス
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i] != null && !includeInCombinedAnimation[i])
            {
                string objName = selectedObjects[i].name;
                EnsureStateExistence(objName);
                initialStates[objName] = EditorGUILayout.ToggleLeft($"{objName}の初期状態", initialStates[objName]);
            }
        }
    }

    // 初期状態が存在しない場合はディクショナリに追加
    void EnsureStateExistence(string name)
    {
        if (!initialStates.ContainsKey(name))
        {
            initialStates.Add(name, true);// デフォルトは true
        }
    }

    void DisplaySavedStatesUI()
    {
        // 結合されるオブジェクトの Saved チェックボックス
        var combinedNames = selectedObjects
            .Where((obj, index) => obj != null && includeInCombinedAnimation[index])
            .Select(obj => obj.name).ToList();

        if (combinedNames.Any())
        {
            string combinedName = string.Join("_", combinedNames);
            EnsureSavedStateExistence(combinedName);
            savedStates[combinedName] = EditorGUILayout.ToggleLeft($"{combinedName}のSaved", savedStates[combinedName]);
        }

        // 個別のオブジェクトの Saved チェックボックス
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i] != null && !includeInCombinedAnimation[i])
            {
                string objName = selectedObjects[i].name;
                EnsureSavedStateExistence(objName);
                savedStates[objName] = EditorGUILayout.ToggleLeft($"{objName}のSaved", savedStates[objName]);
            }
        }
    }

    // Saved 状態が存在しない場合はディクショナリに追加
    void EnsureSavedStateExistence(string name)
    {
        if (!savedStates.ContainsKey(name))
        {
            savedStates.Add(name, true); // デフォルトは true
        }
    }

    // リストのサイズ調整
    void AdjustLists(int size)
    {
        while (selectedObjects.Count < size) selectedObjects.Add(null);
        while (includeInCombinedAnimation.Count < size) includeInCombinedAnimation.Add(false);
        while (selectedObjects.Count > size) selectedObjects.RemoveAt(selectedObjects.Count - 1);
        while (includeInCombinedAnimation.Count > size) includeInCombinedAnimation.RemoveAt(includeInCombinedAnimation.Count - 1);
        while (initialActiveState.Count < size) initialActiveState.Add(false);
        while (initialActiveState.Count > size) initialActiveState.RemoveAt(initialActiveState.Count - 1);
    }

    // 結合アニメーションの生成
    void CreateCombinedAnimation()
    {
        List<GameObject> combinedObjects = new List<GameObject>();
        foreach (var obj in selectedObjects)
        {
            if (obj != null && includeInCombinedAnimation[selectedObjects.IndexOf(obj)])
            {
                combinedObjects.Add(obj);
            }
        }

        // 結合されたオブジェクトがある場合のみアニメーション生成
        if (combinedObjects.Count > 0)
        {
            string combinedName = string.Join("_", combinedObjects.ConvertAll(obj => obj.name));
            CreateAnimation(combinedObjects.ToArray(), true, combinedName);
            CreateAnimation(combinedObjects.ToArray(), false, combinedName);
        }
    }

    // アニメーションの生成
    void CreateAnimation(GameObject[] objects, bool isActive, string name)
    {
        AnimationClip clip = new AnimationClip();
        foreach (var obj in objects)
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.00f, isActive ? 1.0f : 0.0f);
            curve.AddKey(0.01f, isActive ? 1.0f : 0.0f);

            string propertyName = "m_IsActive";

            // 最上位の親オブジェクトを取得
            GameObject topParent = obj.transform.root.gameObject;

            // パスの取得
            string path = GetHierarchyPath(obj.transform, topParent.transform);

            clip.SetCurve(path, typeof(GameObject), propertyName, curve);
        }

        string savePath = $"{animationSavePath}/{name}_{(isActive ? "ON" : "OFF")}.anim";
        AssetDatabase.CreateAsset(clip, savePath);
    }

    // GameObjectの階層パスを取得
    string GetHierarchyPath(Transform current, Transform topParent)
    {
        string path = current.name;
        while (current.parent != null && current.parent != topParent)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }

    // Animator Controllerの生成
    void CreateAnimatorController()
    {
        // Animator Controllerの名前を取得したGameObjectの名前から生成
        string combinedObjectName = string.Join("_", selectedObjects.Where(obj => obj != null).Select(obj => obj.name));
        string controllerName = $"{combinedObjectName}_ToggleLayer";

        string controllerPath = $"{animationSavePath}/{controllerName}_ToggleLayer.controller";
        animatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        var combinedNames = selectedObjects
            .Where((obj, index) => obj != null && includeInCombinedAnimation[index])
            .Select(obj => obj.name).ToList();

        if (combinedNames.Any())
        {
            string combinedName = string.Join("_", combinedNames);
            AddLayerAndParam(combinedName, initialStates[combinedName]);
        }

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i] != null && !includeInCombinedAnimation[i])
            {
                string objName = selectedObjects[i].name;
                AddLayerAndParam(objName, initialStates[objName]);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // AnimatorControllerにLayerとParamaterを追加
    void AddLayerAndParam(string name, bool isActiveInitially)
    {
        // レイヤーの作成
        string layerName = $"{name}_Toggle";
        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = layerName,
            defaultWeight = 1f,
            stateMachine = new AnimatorStateMachine()
        };

        // ステートの追加
        AnimatorStateMachine stateMachine = layer.stateMachine;
        AnimatorState onState = stateMachine.AddState($"{name}_ON");
        AnimatorState offState = stateMachine.AddState($"{name}_OFF");

        onState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animationSavePath}/{name}_ON.anim");
        offState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animationSavePath}/{name}_OFF.anim");

        // パラメータの追加（初期値も設定）
        string paramName = $"{name}_Toggle";
        var param = new AnimatorControllerParameter()
        {
            name = paramName,
            type = AnimatorControllerParameterType.Bool,
            defaultBool = isActiveInitially
        };
        animatorController.AddParameter(param);

        // 遷移の設定
        AnimatorStateTransition toOnTransition = offState.AddTransition(onState);
        toOnTransition.hasExitTime = false;
        toOnTransition.duration = 0f;
        toOnTransition.AddCondition(AnimatorConditionMode.If, 0, paramName);

        AnimatorStateTransition toOffTransition = onState.AddTransition(offState);
        toOffTransition.hasExitTime = false;
        toOffTransition.duration = 0f;
        toOffTransition.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);

        animatorController.AddLayer(layer);
    }

    // Baseフォルダを複製
    void DuplicateBaseFolder(string combinedObjectName)
    {
        string baseFolderPath = "Assets/KRHa's Assets/ToggleMenuGenerator/TMG_Base";
        string newFolderPath = $"{animationSavePath}/TMG_Base_{combinedObjectName}";
        AssetDatabase.CopyAsset(baseFolderPath, newFolderPath);
    }

    // VRCExpressionsMenuとVRCExpressionParametersに書き込み
    void UpdateParamAndMenu(string folderPath, AnimatorController animatorController)
    {
        // VRCExpressionsMenuとVRCExpressionParametersのアセットをロード
        string tagMenuPath = $"{folderPath}/TMG_Menu.asset";
        string tagMenuMainPath = $"{folderPath}/TMG_Menu_Main.asset";
        string tagParamPath = $"{folderPath}/TMG_Param.asset";

        VRCExpressionsMenu tagMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(tagMenuPath);
        VRCExpressionsMenu tagMenuMain = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(tagMenuMainPath);
        VRCExpressionParameters tagParam = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(tagParamPath);

        // TMG_MenuのサブメニューにTMG_Menu_Mainをセット
        VRCExpressionsMenu.Control mainControl = new VRCExpressionsMenu.Control
        {
            name = "ToggleMenuGenerator",
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = tagMenuMain
        };
        tagMenu.controls.Add(mainControl);

        // VRCExpressionsMenuへのコントロール追加
        foreach (var param in animatorController.parameters)
        {
            VRCExpressionsMenu.Control control = new VRCExpressionsMenu.Control
            {
                name = param.name,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = param.name },
                value = param.defaultBool ? 1 : 0
            };
            tagMenuMain.controls.Add(control);
        }

        // VRCExpressionParametersへのパラメータ追加
        List<VRCExpressionParameters.Parameter> parametersList = tagParam.parameters.ToList();
        foreach (var param in animatorController.parameters)
        {
            // saved の状態を適切に取得
            bool isSaved = savedStates.ContainsKey(param.name) && savedStates[param.name];

            VRCExpressionParameters.Parameter expressionParam = new VRCExpressionParameters.Parameter
            {
                name = param.name,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = param.defaultBool ? 1f : 0f,
                saved = isSaved
            };
            parametersList.Add(expressionParam);
        }
        tagParam.parameters = parametersList.ToArray();

        // 保存
        EditorUtility.SetDirty(tagMenu);
        EditorUtility.SetDirty(tagMenuMain);
        EditorUtility.SetDirty(tagParam);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // MA関連のコンポーネントを実装
    void CreateMAComponents(string combinedObjectName, string folderPath)
    {
        // 最上位の親オブジェクトを特定
        Transform topParent = selectedObjects[0].transform.root;

        // MA_ToggleAnim GameObjectの作成
        GameObject maToggleAnim = new GameObject("MA_ToggleAnim");
        maToggleAnim.transform.SetParent(topParent, false);

        // コンポーネントの追加
        var maAnimator = maToggleAnim.AddComponent<ModularAvatarMergeAnimator>();
        var maParams = maToggleAnim.AddComponent<ModularAvatarParameters>();
        var maMenu = maToggleAnim.AddComponent<ModularAvatarMenuInstaller>();

        // ModularAvatarParametersにパラメータを追加
        string tagParamPath = $"{folderPath}/TMG_Param.asset";
        VRCExpressionParameters tagParam = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(tagParamPath);
        foreach (var param in tagParam.parameters)
        {
            maParams.parameters.Add(new ParameterConfig()
            {
                nameOrPrefix = param.name,
                syncType = ParameterSyncType.Bool,
                defaultValue = param.defaultValue,
                saved = param.saved,
                remapTo = ""
            });
        }

        // ModularAvatarMenuInstallerの設定
        string tagMenuPath = $"{folderPath}/TMG_Menu.asset";
        VRCExpressionsMenu tagMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(tagMenuPath);
        maMenu.menuToAppend = tagMenu;

        // ModularAvatarMergeAnimatorの設定
        maAnimator.animator = animatorController;
        maAnimator.deleteAttachedAnimator = false;
        maAnimator.matchAvatarWriteDefaults = true;
        maAnimator.pathMode = MergeAnimatorPathMode.Absolute;
    }
}