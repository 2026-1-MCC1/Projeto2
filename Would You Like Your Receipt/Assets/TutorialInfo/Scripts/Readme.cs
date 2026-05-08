using System;
using UnityEngine;

public class Readme : ScriptableObject
{
    // Imagem mostrada no topo do Readme dentro do Inspector da Unity.
    public Texture2D icon;
    // Titulo principal exibido para apresentar o projeto/tutorial.
    public string title;
    // Lista de secoes que formam o corpo do Readme.
    public Section[] sections;
    // Marca se o layout do template ja foi processado pelo editor.
    public bool loadedLayout;

    [Serializable]
    public class Section
    {
        // Titulo da secao.
        public string heading;
        // Texto principal da secao.
        public string text;
        // Texto clicavel que aparece como link.
        public string linkText;
        // URL aberta quando o link e clicado.
        public string url;
    }
}
