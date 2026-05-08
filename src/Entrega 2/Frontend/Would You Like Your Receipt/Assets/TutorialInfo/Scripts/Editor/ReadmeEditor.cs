using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;

[CustomEditor(typeof(Readme))]
[InitializeOnLoad]
public class ReadmeEditor : Editor
{
    // Chave usada para lembrar se o Readme ja foi exibido nesta sessao do editor.
    static string s_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";

    // Pasta que contem os arquivos do tutorial/importados junto com o template.
    static string s_ReadmeSourceDirectory = "Assets/TutorialInfo";

    // Espacamento padrao usado entre blocos visuais do Inspector.
    const float k_Space = 16f;

    static ReadmeEditor()
    {
        // Agenda a selecao automatica do Readme assim que a Unity terminar de carregar.
        EditorApplication.delayCall += SelectReadmeAutomatically;
    }

    static void RemoveTutorial()
    {
        // Pergunta ao usuario se ele realmente quer apagar os arquivos de tutorial.
        if (EditorUtility.DisplayDialog(
            "Remove Readme Assets",
            $"All contents under {s_ReadmeSourceDirectory} will be removed, are you sure you want to proceed?",
            "Proceed",
            "Cancel"))
        {
            // Remove a pasta principal do tutorial e seu .meta.
            if (Directory.Exists(s_ReadmeSourceDirectory))
            {
                FileUtil.DeleteFileOrDirectory(s_ReadmeSourceDirectory);
                FileUtil.DeleteFileOrDirectory(s_ReadmeSourceDirectory + ".meta");
            }
            else
            {
                Debug.Log($"Could not find the Readme folder at {s_ReadmeSourceDirectory}");
            }

            // Tambem tenta remover o asset do Readme em si.
            var readmeAsset = SelectReadme();
            if (readmeAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(readmeAsset);
                FileUtil.DeleteFileOrDirectory(path + ".meta");
                FileUtil.DeleteFileOrDirectory(path);
            }

            // Atualiza o AssetDatabase para refletir a exclusao dos arquivos.
            AssetDatabase.Refresh();
        }
    }

    static void SelectReadmeAutomatically()
    {
        // So tenta selecionar automaticamente uma vez por sessao do editor.
        if (!SessionState.GetBool(s_ShowedReadmeSessionStateName, false))
        {
            var readme = SelectReadme();
            SessionState.SetBool(s_ShowedReadmeSessionStateName, true);

            if (readme && !readme.loadedLayout)
            {
                // Marca o layout como carregado, mas sem forcar a troca de layout da Scene View.
                readme.loadedLayout = true;
            }
        }
    }

    static void LoadLayout()
    {
        // Acessa por reflexao a API interna da Unity que sabe carregar layouts salvos.
        var assembly = typeof(EditorApplication).Assembly;
        var windowLayoutType = assembly.GetType("UnityEditor.WindowLayout", true);
        var method = windowLayoutType.GetMethod("LoadWindowLayout", BindingFlags.Public | BindingFlags.Static);
        method.Invoke(null, new object[] { Path.Combine(Application.dataPath, "TutorialInfo/Layout.wlt"), false });
    }

    static Readme SelectReadme()
    {
        // Procura assets do tipo Readme no projeto.
        var ids = AssetDatabase.FindAssets("Readme t:Readme");
        if (ids.Length == 1)
        {
            var readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));

            // Seleciona o asset no editor para que ele apareca no Inspector.
            Selection.objects = new UnityEngine.Object[] { readmeObject };

            return (Readme)readmeObject;
        }
        else
        {
            Debug.Log("Couldn't find a readme");
            return null;
        }
    }

    protected override void OnHeaderGUI()
    {
        // Desenha a cabecalho com icone e titulo.
        var readme = (Readme)target;
        Init();

        var iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

        GUILayout.BeginHorizontal("In BigTitle");
        {
            if (readme.icon != null)
            {
                GUILayout.Space(k_Space);
                GUILayout.Label(readme.icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
            }

            GUILayout.Space(k_Space);
            GUILayout.BeginVertical();
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(readme.title, TitleStyle);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndHorizontal();
    }

    public override void OnInspectorGUI()
    {
        // Desenha cada secao de conteudo configurada no asset Readme.
        var readme = (Readme)target;
        Init();

        foreach (var section in readme.sections)
        {
            if (!string.IsNullOrEmpty(section.heading))
            {
                GUILayout.Label(section.heading, HeadingStyle);
            }

            if (!string.IsNullOrEmpty(section.text))
            {
                GUILayout.Label(section.text, BodyStyle);
            }

            if (!string.IsNullOrEmpty(section.linkText))
            {
                // Cria um texto clicavel que abre a URL configurada.
                if (LinkLabel(new GUIContent(section.linkText)))
                {
                    Application.OpenURL(section.url);
                }
            }

            GUILayout.Space(k_Space);
        }

        if (GUILayout.Button("Remove Readme Assets", ButtonStyle))
        {
            // Remove o material do tutorial quando o usuario confirmar.
            RemoveTutorial();
        }
    }

    // Flag interna para nao recriar os estilos toda vez que o Inspector redesenhar.
    bool m_Initialized;

    GUIStyle LinkStyle
    {
        get { return m_LinkStyle; }
    }

    [SerializeField]
    GUIStyle m_LinkStyle;

    GUIStyle TitleStyle
    {
        get { return m_TitleStyle; }
    }

    [SerializeField]
    GUIStyle m_TitleStyle;

    GUIStyle HeadingStyle
    {
        get { return m_HeadingStyle; }
    }

    [SerializeField]
    GUIStyle m_HeadingStyle;

    GUIStyle BodyStyle
    {
        get { return m_BodyStyle; }
    }

    [SerializeField]
    GUIStyle m_BodyStyle;

    GUIStyle ButtonStyle
    {
        get { return m_ButtonStyle; }
    }

    [SerializeField]
    GUIStyle m_ButtonStyle;

    void Init()
    {
        // Sai imediatamente se os estilos ja tiverem sido preparados antes.
        if (m_Initialized)
            return;

        // Estilo base usado no corpo dos textos.
        m_BodyStyle = new GUIStyle(EditorStyles.label);
        m_BodyStyle.wordWrap = true;
        m_BodyStyle.fontSize = 14;
        m_BodyStyle.richText = true;

        // Estilo maior para o titulo principal.
        m_TitleStyle = new GUIStyle(m_BodyStyle);
        m_TitleStyle.fontSize = 26;

        // Estilo em negrito para os subtitulos das secoes.
        m_HeadingStyle = new GUIStyle(m_BodyStyle);
        m_HeadingStyle.fontStyle = FontStyle.Bold;
        m_HeadingStyle.fontSize = 18;

        // Estilo visual dos links clicaveis.
        m_LinkStyle = new GUIStyle(m_BodyStyle);
        m_LinkStyle.wordWrap = false;
        m_LinkStyle.normal.textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);
        m_LinkStyle.stretchWidth = false;

        // Estilo do botao de remover assets.
        m_ButtonStyle = new GUIStyle(EditorStyles.miniButton);
        m_ButtonStyle.fontStyle = FontStyle.Bold;

        m_Initialized = true;
    }

    bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
    {
        // Reserva a area do link no layout atual do Inspector.
        var position = GUILayoutUtility.GetRect(label, LinkStyle, options);

        // Desenha uma linha embaixo do texto para reforcar que ele e clicavel.
        Handles.BeginGUI();
        Handles.color = LinkStyle.normal.textColor;
        Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
        Handles.color = Color.white;
        Handles.EndGUI();

        // Troca o cursor quando ele passa por cima do link.
        EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

        // Retorna true quando o usuario clica no texto.
        return GUI.Button(position, label, LinkStyle);
    }
}
