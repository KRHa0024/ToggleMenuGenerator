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
    AnimatorController animatorController;

    // オブジェクトリストの管理に使用されるクラス
    class ObjectData
    {
        public GameObject gameObject;
        public bool initialState = true;
        public bool isSaved = true;
        public List<GameObject> combinedObjects = new List<GameObject>();
        public bool includeBlendShapes = false;
        public GameObject blendShapeMesh;
        public Dictionary<string, bool> blendShapeEnabled = new Dictionary<string, bool>();
        public Dictionary<string, Vector2> blendShapeValues = new Dictionary<string, Vector2>();
        public bool blendShapeFoldout = false;
    }

    List<ObjectData> objectDatas = new List<ObjectData>();

    [MenuItem("くろ～は/ToggleMenuGenerator")]
    public static void ShowWindow()
    {
        ToggleMenuGenerator window = GetWindow<ToggleMenuGenerator>("ToggleMenuGenerator");
        window.minSize = new Vector2(500, 400);
    }

    void OnGUI()
    {
        var originalColor = GUI.backgroundColor;

        GUILayout.Label("トグルするアイテムを選択してください", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("アイテムを追加", GUILayout.Height(25)))
        {
            objectDatas.Add(new ObjectData());
        }

        if (GUILayout.Button("アイテムを削除", GUILayout.Height(25)) && objectDatas.Count > 0)
        {
            objectDatas.RemoveAt(objectDatas.Count - 1);
        }

        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < objectDatas.Count; i++)
        {
            var data = objectDatas[i];

            EditorGUILayout.BeginHorizontal();
            data.gameObject = EditorGUILayout.ObjectField($"アイテム {i + 1}", data.gameObject, typeof(GameObject), true) as GameObject;
            if (GUILayout.Button("まとめるアイテムを追加", GUILayout.Width(150)))
            {
                data.combinedObjects.Add(null);
            }
            EditorGUILayout.EndHorizontal();

            for (int j = 0; j < data.combinedObjects.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                data.combinedObjects[j] = EditorGUILayout.ObjectField($"まとめるアイテム {j + 1}", data.combinedObjects[j], typeof(GameObject), true) as GameObject;
                if (GUILayout.Button("×", GUILayout.Width(50)) && data.combinedObjects.Count > 0)
                {
                    data.combinedObjects.RemoveAt(j);
                    j--;
                }
                EditorGUILayout.EndHorizontal();
            }

            data.initialState = EditorGUILayout.ToggleLeft("初期状態", data.initialState);
            data.isSaved = EditorGUILayout.ToggleLeft("Saved", data.isSaved);

            // ブレンドシェイプ関連のUIを追加
            data.includeBlendShapes = EditorGUILayout.ToggleLeft("Blendshapeも変化させる", data.includeBlendShapes);

            if (data.includeBlendShapes)
            {
                // ブレンドシェイプメッシュオブジェクトのフィールドを表示
                data.blendShapeMesh = EditorGUILayout.ObjectField("BlendShape Mesh", data.blendShapeMesh, typeof(GameObject), true) as GameObject;
                data.blendShapeFoldout = EditorGUILayout.Foldout(data.blendShapeFoldout, "BlendShapes");
                if (data.blendShapeFoldout)
                {
                    SkinnedMeshRenderer skinnedMeshRenderer = data.blendShapeMesh.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderer != null)
                    {
                        Mesh mesh = skinnedMeshRenderer.sharedMesh;
                        for (int j = 0; j < mesh.blendShapeCount; j++)
                        {
                            string blendShapeName = mesh.GetBlendShapeName(j);
                            if (!data.blendShapeEnabled.ContainsKey(blendShapeName))
                            {
                                data.blendShapeEnabled[blendShapeName] = false;
                                data.blendShapeValues[blendShapeName] = new Vector2(0, 0); // 初期値
                            }

                            // ブレンドシェイプの選択
                            data.blendShapeEnabled[blendShapeName] = EditorGUILayout.ToggleLeft(blendShapeName, data.blendShapeEnabled[blendShapeName]);

                            // 値の設定
                            if (data.blendShapeEnabled[blendShapeName])
                            {
                                data.blendShapeValues[blendShapeName] = EditorGUILayout.Vector2Field("Values", data.blendShapeValues[blendShapeName]);
                            }
                        }
                    }
                }
            }

            GUI.color = new Color(0.16f, 0.16f, 0.16f);;
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
            GUI.color = originalColor;
        }

        GUILayout.Space(10);

        GUILayout.Label("保存先を変更(Assets/以下のフォルダにのみ保存できます)\n例: Assets/KRHa's Assets/ToggleMenuGenerator/Animations", EditorStyles.boldLabel);
        animationSavePath = EditorGUILayout.TextField(animationSavePath);
    
        if (GUILayout.Button("保存先のリセット"))
        {
            animationSavePath = "Assets/KRHa's Assets/ToggleMenuGenerator/Animations";
        }
            
        GUILayout.Space(5);

        if (GUILayout.Button("セットアップ！", GUILayout.Height(50)))
        {
            // アイテムが追加されていない場合のチェック
            if (objectDatas.Count == 0 || objectDatas.All(data => data.gameObject == null))
            {
                EditorUtility.DisplayDialog("警告", "アイテムが追加されていません。", "OK");
                return;
            }

            // 保存先のチェック
            if (!Directory.Exists(animationSavePath))
            {
                EditorUtility.DisplayDialog("エラー", "指定された保存先が存在しません。", "OK");
                return;
            }

            CreateCombinedAnimation();

            string combinedObjectName = GetCombinedObjectName();

            CreateAnimatorController();

            DuplicateBaseFolder(combinedObjectName);
            
            string newFolderPath = $"{animationSavePath}/TMG_Base_{combinedObjectName}";

            Setup(newFolderPath, animatorController);
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
                CreateAnimation(data, true, combinedName);
                CreateAnimation(data, false, combinedName);
            }
        }
    }

    // アニメーションの生成
    void CreateAnimation(ObjectData data, bool isActive, string name)
    {
        AnimationClip clip = new AnimationClip();
        if (data.gameObject != null)
        {
            // 通常のアクティブ/非アクティブのアニメーションキーを設定
            AnimationCurve activeCurve = new AnimationCurve();
            activeCurve.AddKey(0.00f, isActive ? 1.0f : 0.0f);
            string activePropertyName = "m_IsActive";
            GameObject topParent = data.gameObject.transform.root.gameObject;
            string path = GetHierarchyPath(data.gameObject.transform, topParent.transform);
            clip.SetCurve(path, typeof(GameObject), activePropertyName, activeCurve);

            // ブレンドシェイプのアニメーションキーを設定（もし該当する場合）
            if (data.includeBlendShapes && data.blendShapeMesh != null)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = data.blendShapeMesh.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    Mesh mesh = skinnedMeshRenderer.sharedMesh;
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string blendShapeName = mesh.GetBlendShapeName(i);
                        if (data.blendShapeEnabled.ContainsKey(blendShapeName) && data.blendShapeEnabled[blendShapeName])
                        {
                            AnimationCurve blendShapeCurve = new AnimationCurve();
                            Vector2 values = data.blendShapeValues[blendShapeName];
                            float startValue = isActive ? values.x : values.y;
                            blendShapeCurve.AddKey(0.00f, startValue);
                            string blendShapePropertyName = $"blendShape.{blendShapeName}";
                            clip.SetCurve(path, typeof(SkinnedMeshRenderer), blendShapePropertyName, blendShapeCurve);
                        }
                    }
                }
            }
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

    // AnimatorControllerにLayerとParameterを追加
    void AddLayerAndParam(string name, bool initialState)
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

        // パラメータの追加（初期状態に基づいた設定）
        string paramName = $"{name}_Toggle";
        var param = new AnimatorControllerParameter()
        {
            name = paramName,
            type = AnimatorControllerParameterType.Bool,
            defaultBool = initialState
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

    // MA関連のコンポーネントを実装
    void Setup(string folderPath, AnimatorController animatorController)
    {
        Transform topParent = objectDatas.Select(d => d.gameObject).FirstOrDefault(obj => obj != null)?.transform.root;

        GameObject maToggleAnim = new GameObject("MA_ToggleAnim");
        maToggleAnim.transform.SetParent(topParent, false);

        var maAnimator = maToggleAnim.AddComponent<ModularAvatarMergeAnimator>();
        var maParams = maToggleAnim.AddComponent<ModularAvatarParameters>();
        var maMenu = maToggleAnim.AddComponent<ModularAvatarMenuInstaller>();

        // VRCExpressionsMenuのアセットをロード
        string tagMenuPath = $"{folderPath}/TMG_Menu.asset";
        string tagMenuMainPath = $"{folderPath}/TMG_Menu_Main.asset";
        VRCExpressionsMenu tagMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(tagMenuPath);
        VRCExpressionsMenu tagMenuMain = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(tagMenuMainPath);

        // TMG_MenuのサブメニューにTMG_Menu_Mainをセット
        VRCExpressionsMenu.Control mainControl = new VRCExpressionsMenu.Control
        {
            name = "ToggleMenuGenerator",
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = tagMenuMain
        };
        tagMenu.controls.Add(mainControl);

        // ModularAvatarParametersにパラメータを追加
        foreach (var param in animatorController.parameters)
        {
            var initialState = objectDatas.FirstOrDefault(d => GetCombinedName(d) + "_Toggle" == param.name)?.initialState ?? false;

            // VRCExpressionsMenuへのコントロール追加
            var control = new VRCExpressionsMenu.Control
            {
                name = param.name,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = param.name },
                value = 1//initialState ? 1 : 0(初期値をFalseにすると何故かうまく動かないため。)
            };
            tagMenuMain.controls.Add(control);

            // ModularAvatarParametersにパラメータを追加
            maParams.parameters.Add(new ParameterConfig()
            {
                nameOrPrefix = param.name,
                syncType = ParameterSyncType.Bool,
                defaultValue = initialState ? 1f : 0f,
                saved = objectDatas.FirstOrDefault(d => GetCombinedName(d) + "_Toggle" == param.name)?.isSaved ?? false,
            });
        }

        // 保存
        EditorUtility.SetDirty(tagMenu);
        EditorUtility.SetDirty(tagMenuMain);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ModularAvatarMergeAnimatorの設定
        maAnimator.animator = animatorController;
        maAnimator.deleteAttachedAnimator = false;
        maAnimator.matchAvatarWriteDefaults = true;
        maAnimator.pathMode = MergeAnimatorPathMode.Absolute;

        // ModularAvatarMenuInstallerの設定
        maMenu.menuToAppend = tagMenu;
    }
    // 各オブジェクトの結合された名前を取得
    string GetCombinedName(ObjectData data)
    {
        // まとめるオブジェクトがない場合は単一のオブジェクト名を返す
        if (data.combinedObjects.Count == 0)
        {
            return data.gameObject.name;
        }
        else
        {
            // まとめるオブジェクトがある場合は結合された名前を生成
            var combinedNames = data.combinedObjects.Select(obj => obj.name).ToList();
            combinedNames.Insert(0, data.gameObject.name); // 最初にメインのオブジェクトを追加
            return string.Join("_", combinedNames);
        }
    }

    // 全オブジェクトの結合名を取得
    string GetCombinedObjectName()
    {
        return string.Join("_", objectDatas.Select(data => GetCombinedName(data)));
    }
    
}