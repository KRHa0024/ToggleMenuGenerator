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
    bool setupMA = true;
    AnimatorController animatorController;

    // オブジェクトリストの管理に使用されるクラス
    class ObjectData
    {
        public GameObject gameObject;
        public bool initialState;
        public bool isSaved;
        public List<GameObject> combinedObjects = new List<GameObject>();
    }

    List<ObjectData> objectDatas = new List<ObjectData>();

    [MenuItem("くろ～は/ToggleMenuGenerator")]
    public static void ShowWindow()
    {
        GetWindow<ToggleMenuGenerator>("ToggleMenuGenerator");
    }

    void OnGUI()
    {
        GUILayout.Label("GameObjectを選択してください", EditorStyles.boldLabel);

        // "オブジェクトを追加"ボタン
        if (GUILayout.Button("オブジェクトを追加"))
        {
            objectDatas.Add(new ObjectData());
        }

        // "オブジェクトを削除"ボタン（一番最後のオブジェクトフィールドから削除）
        if (GUILayout.Button("オブジェクトを削除") && objectDatas.Count > 0)
        {
            objectDatas.RemoveAt(objectDatas.Count - 1);
        }


        for (int i = 0; i < objectDatas.Count; i++)
        {
            var data = objectDatas[i];

            EditorGUILayout.BeginHorizontal();
            data.gameObject = EditorGUILayout.ObjectField($"GameObject {i + 1}", data.gameObject, typeof(GameObject), true) as GameObject;
            if (GUILayout.Button("+"))
            {
                data.combinedObjects.Add(null);
            }
            EditorGUILayout.EndHorizontal();

            for (int j = 0; j < data.combinedObjects.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                data.combinedObjects[j] = EditorGUILayout.ObjectField($"Combined Object {j + 1}", data.combinedObjects[j], typeof(GameObject), true) as GameObject;
                if (GUILayout.Button("-") && data.combinedObjects.Count > 0)
                {
                    data.combinedObjects.RemoveAt(j);
                    j--;
                }
                EditorGUILayout.EndHorizontal();
            }

            data.initialState = EditorGUILayout.ToggleLeft("初期状態", data.initialState);
            data.isSaved = EditorGUILayout.ToggleLeft("Saved", data.isSaved);
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
        setupMA = EditorGUILayout.Toggle("ModularAvatarでセットアップ", setupMA);
            
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

            if (setupMA)
            {
                string combinedObjectName = GetCombinedObjectName();

                CreateAnimatorController();

                DuplicateBaseFolder(combinedObjectName);
                
                string newFolderPath = $"{animationSavePath}/TMG_Base_{combinedObjectName}";
                UpdateParamAndMenu(newFolderPath, animatorController);

                CreateMAComponents(newFolderPath);
            }
        }
    }

    // 結合アニメーションの生成
    void CreateCombinedAnimation()
    {
        foreach (var data in objectDatas)
        {
            if (data.gameObject != null)
            {
                // まとめるオブジェクトのリストを作成
                List<GameObject> objectsToCombine = new List<GameObject>();
                objectsToCombine.Add(data.gameObject);
                objectsToCombine.AddRange(data.combinedObjects);

                // アニメーションを生成
                string combinedName = GetCombinedName(data);
                CreateAnimation(objectsToCombine.ToArray(), true, combinedName);
                CreateAnimation(objectsToCombine.ToArray(), false, combinedName);
            }
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
        string combinedObjectName = GetCombinedObjectName();
        string controllerName = $"{combinedObjectName}_ToggleLayer";
        string controllerPath = $"{animationSavePath}/{controllerName}.controller";
        animatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        foreach (var data in objectDatas)
        {
            if (data.gameObject != null)
            {
                string layerName = GetCombinedName(data);
                bool initialState = data.initialState;
                AddLayerAndParam(layerName, initialState);
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
    void UpdateParamAndMenu(string newFolderPath, AnimatorController animatorController)
    {
        // VRCExpressionsMenuとVRCExpressionParametersのアセットをロード
        string tagMenuPath = $"{newFolderPath}/TMG_Menu.asset";
        string tagMenuMainPath = $"{newFolderPath}/TMG_Menu_Main.asset";
        string tagParamPath = $"{newFolderPath}/TMG_Param.asset";

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

            VRCExpressionParameters.Parameter expressionParam = new VRCExpressionParameters.Parameter
            {
                name = param.name,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = param.defaultBool ? 1f : 0f,
                //saved = param.isSaved,
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
    void CreateMAComponents(string folderPath)
    {
        Transform topParent = objectDatas.Select(d => d.gameObject).FirstOrDefault(obj => obj != null)?.transform.root;

        GameObject maToggleAnim = new GameObject("MA_ToggleAnim");
        maToggleAnim.transform.SetParent(topParent, false);

        var maAnimator = maToggleAnim.AddComponent<ModularAvatarMergeAnimator>();
        var maParams = maToggleAnim.AddComponent<ModularAvatarParameters>();
        var maMenu = maToggleAnim.AddComponent<ModularAvatarMenuInstaller>();

        // ModularAvatarParametersにパラメータを追加
        foreach (var data in objectDatas)
        {
            if (data.gameObject != null)
            {
                string paramName = GetCombinedName(data) + "_Toggle";
                maParams.parameters.Add(new ParameterConfig()
                {
                    nameOrPrefix = paramName,
                    syncType = ParameterSyncType.Bool,
                    defaultValue = data.initialState ? 1f : 0f,
                    saved = data.isSaved,
                    remapTo = ""
                });
            }
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
    // 各オブジェクトの結合された名前を取得
    string GetCombinedName(ObjectData data)
    {
        return data.gameObject.name + "_" + string.Join("_", data.combinedObjects.Select(obj => obj.name));
    }

    // 全オブジェクトの結合名を取得
    string GetCombinedObjectName()
    {
        return string.Join("_", objectDatas.Select(data => GetCombinedName(data)));
    }
    
}