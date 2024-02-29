// Advanced Resource Checker

using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
#endif
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

public class TextureDetails : IEquatable<TextureDetails>
{
	public bool isCubeMap;
    public bool hasAlphaWrongSetting;
    public int memSizeKB;
	public Texture texture;
	public TextureFormat format;
	public int mipMapCount;
	public List<Object> FoundInMaterials=new List<Object>();
	public List<Object> FoundInRenderers=new List<Object>();
	public List<Object> FoundInAnimators = new List<Object>();
	public List<Object> FoundInScripts = new List<Object>();
	public List<Object> FoundInGraphics = new List<Object>();
	public List<Object> FoundInButtons = new List<Object>();
	public bool isSky;
	public bool instance;
	public bool isgui;
    public bool hasAlpha;

	public TextureDetails()
	{

	}

    public bool Equals(TextureDetails other)
    {
        return texture != null && other.texture != null &&
			texture.GetNativeTexturePtr() == other.texture.GetNativeTexturePtr();
    }

    public override int GetHashCode()
    {
		return (int)texture.GetNativeTexturePtr();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TextureDetails);
    }
};

public class MaterialDetails
{

	public Material material;
    public String shaderName;
    public String shaderBrand;
	public List<Renderer> FoundInRenderers=new List<Renderer>();
	public List<Graphic> FoundInGraphics=new List<Graphic>();
	public bool instance;
	public bool isgui;
	public bool isSky;

	public MaterialDetails()
	{
		instance = false;
		isgui = false;
		isSky = false;
	}
};

public class MeshDetails
{

	public Mesh mesh;

	public List<MeshFilter> FoundInMeshFilters=new List<MeshFilter>();
	public List<SkinnedMeshRenderer> FoundInSkinnedMeshRenderer=new List<SkinnedMeshRenderer>();
	public bool instance;

	public MeshDetails()
	{
		instance = false;
	}
};

public class MissingGraphic{
	public Transform Object;
	public string type;
	public string name;
}

public class AdvancedResourceChecker : EditorWindow {


	string[] inspectToolbarStrings = {"Textures", "Materials","Shader","Meshes"};
	string[] inspectToolbarStrings2 = {"Textures", "Materials","Shader","Meshes", "Missing"};

	enum InspectType 
	{
		Textures,Materials,Shaders,Meshes,Missing
	};

	bool IncludeDisabledObjects=true;
	bool IncludeSpriteAnimations=true;
	bool IncludeScriptReferences=true;
	bool IncludeGuiElements=true;
	bool thingsMissing = false;

	InspectType ActiveInspectType=InspectType.Textures;

	float ThumbnailWidth=40;
	float ThumbnailHeight=40;

	List<TextureDetails> ActiveTextures=new List<TextureDetails>();
	List<MaterialDetails> ActiveMaterials=new List<MaterialDetails>();
	List<MeshDetails> ActiveMeshDetails=new List<MeshDetails>();
	List<MissingGraphic> MissingObjects = new List<MissingGraphic> ();
    private HashSet<string> printedMaterials;

	Vector2 textureListScrollPos=new Vector2(0,0);
	Vector2 materialListScrollPos=new Vector2(0,0);
    Vector2 shaderListScrollPos = new Vector2(0, 0);
    Vector2 meshListScrollPos=new Vector2(0,0);
	Vector2 missingListScrollPos = new Vector2 (0,0);

	int TotalTextureMemory=0;
	int TotalMeshVertices=0;
    int shaderCount = 0;
    bool shaderChecked;

    bool ctrlPressed=false;

	static int MinWidth=475;
	Color defColor;

	bool collectedInPlayingMode;

	[MenuItem ("Tools/Advanced Resource Checker")]
	static void Init ()
	{  
		AdvancedResourceChecker window = (AdvancedResourceChecker) EditorWindow.GetWindow (typeof (AdvancedResourceChecker));
		window.CheckResources();
		window.minSize=new Vector2(MinWidth,475);
	}

	void OnGUI ()
	{
		defColor = GUI.color;
		IncludeDisabledObjects = GUILayout.Toggle(IncludeDisabledObjects, "Include disabled objects", GUILayout.Width(300));
		IncludeSpriteAnimations = GUILayout.Toggle(IncludeSpriteAnimations, "Look in sprite animations", GUILayout.Width(300));
		GUI.color = new Color (0.8f, 0.8f, 1.0f, 1.0f);
		IncludeScriptReferences = GUILayout.Toggle(IncludeScriptReferences, "Look in behavior fields", GUILayout.Width(300));
		GUI.color = new Color (1.0f, 0.95f, 0.8f, 1.0f);
		IncludeGuiElements = GUILayout.Toggle(IncludeGuiElements, "Look in GUI elements", GUILayout.Width(300));
		GUI.color = defColor;
		GUILayout.BeginArea(new Rect(position.width-85,5,100,85));
		if (GUILayout.Button("Calculate",GUILayout.Width(80), GUILayout.Height(40)))
			CheckResources();
		if (GUILayout.Button("CleanUp",GUILayout.Width(80), GUILayout.Height(20)))
			Resources.UnloadUnusedAssets();
        if (GUILayout.Button("SelectAll", GUILayout.Width(80), GUILayout.Height(20)))
            selectAll();
        GUILayout.EndArea();
		RemoveDestroyedResources();

		GUILayout.Space(30);
		if (thingsMissing == true) {
			EditorGUI.HelpBox (new Rect(8,75,300,25),"Some GameObjects are missing graphical elements.", MessageType.Error);
		}
		GUILayout.BeginHorizontal();
		GUILayout.Label("Textures "+ActiveTextures.Count+" - "+FormatSizeString(TotalTextureMemory));
		GUILayout.Label("Materials "+ActiveMaterials.Count);
        GUILayout.Label("Shaders  " + shaderCount);
        GUILayout.Label("Meshes "+ActiveMeshDetails.Count+" - "+TotalMeshVertices+" verts");
		GUILayout.EndHorizontal();
		if (thingsMissing == true) {
			ActiveInspectType = (InspectType)GUILayout.Toolbar ((int)ActiveInspectType, inspectToolbarStrings2);
		} else {
			ActiveInspectType = (InspectType)GUILayout.Toolbar ((int)ActiveInspectType, inspectToolbarStrings);
		}

		ctrlPressed=Event.current.control || Event.current.command;

		switch (ActiveInspectType)
		{
		case InspectType.Textures:
			ListTextures();
			break;
		case InspectType.Materials:
			ListMaterials();
			break;
        case InspectType.Shaders:
            ListShader();
            break;
        case InspectType.Meshes:
			ListMeshes();
			break;
		case InspectType.Missing:
			ListMissing();
			break;
		}
	}

	private void RemoveDestroyedResources()
	{
		if (collectedInPlayingMode != Application.isPlaying)
		{
			ActiveTextures.Clear();
			ActiveMaterials.Clear();
			ActiveMeshDetails.Clear();
			MissingObjects.Clear ();
			thingsMissing = false;
			collectedInPlayingMode = Application.isPlaying;
		}
		
		ActiveTextures.RemoveAll(x => !x.texture);
		ActiveTextures.ForEach(delegate(TextureDetails obj) {
			obj.FoundInAnimators.RemoveAll(x => !x);
			obj.FoundInMaterials.RemoveAll(x => !x);
			obj.FoundInRenderers.RemoveAll(x => !x);
			obj.FoundInScripts.RemoveAll(x => !x);
			obj.FoundInGraphics.RemoveAll(x => !x);
		});

		ActiveMaterials.RemoveAll(x => !x.material);
		ActiveMaterials.ForEach(delegate(MaterialDetails obj) {
			obj.FoundInRenderers.RemoveAll(x => !x);
			obj.FoundInGraphics.RemoveAll(x => !x);
		});

		ActiveMeshDetails.RemoveAll(x => !x.mesh);
		ActiveMeshDetails.ForEach(delegate(MeshDetails obj) {
			obj.FoundInMeshFilters.RemoveAll(x => !x);
			obj.FoundInSkinnedMeshRenderer.RemoveAll(x => !x);
		});

		TotalTextureMemory = 0;
		foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

		TotalMeshVertices = 0;
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;
	}

	int GetBitsPerPixel(TextureFormat format)
	{
		switch (format)
		{
		case TextureFormat.Alpha8: //	 Alpha-only texture format.
			return 8;
		case TextureFormat.ARGB4444: //	 A 16 bits/pixel texture format. Texture stores color with an alpha channel.
			return 16;
		case TextureFormat.RGBA4444: //	 A 16 bits/pixel texture format.
			return 16;
		case TextureFormat.RGB24:	// A color texture format.
			return 24;
		case TextureFormat.RGBA32:	//Color with an alpha channel texture format.
			return 32;
		case TextureFormat.ARGB32:	//Color with an alpha channel texture format.
			return 32;
		case TextureFormat.RGB565:	//	 A 16 bit color texture format.
			return 16;
		case TextureFormat.DXT1:	// Compressed color texture format.
			return 4;
		case TextureFormat.DXT5:	// Compressed color with alpha channel texture format.
			return 8;
			/*
			case TextureFormat.WiiI4:	// Wii texture format.
			case TextureFormat.WiiI8:	// Wii texture format. Intensity 8 bit.
			case TextureFormat.WiiIA4:	// Wii texture format. Intensity + Alpha 8 bit (4 + 4).
			case TextureFormat.WiiIA8:	// Wii texture format. Intensity + Alpha 16 bit (8 + 8).
			case TextureFormat.WiiRGB565:	// Wii texture format. RGB 16 bit (565).
			case TextureFormat.WiiRGB5A3:	// Wii texture format. RGBA 16 bit (4443).
			case TextureFormat.WiiRGBA8:	// Wii texture format. RGBA 32 bit (8888).
			case TextureFormat.WiiCMPR:	//	 Compressed Wii texture format. 4 bits/texel, ~RGB8A1 (Outline alpha is not currently supported).
				return 0;  //Not supported yet
			*/
		case TextureFormat.PVRTC_RGB2://	 PowerVR (iOS) 2 bits/pixel compressed color texture format.
			return 2;
		case TextureFormat.PVRTC_RGBA2://	 PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format
			return 2;
		case TextureFormat.PVRTC_RGB4://	 PowerVR (iOS) 4 bits/pixel compressed color texture format.
			return 4;
		case TextureFormat.PVRTC_RGBA4://	 PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format
			return 4;
		case TextureFormat.ETC_RGB4://	 ETC (GLES2.0) 4 bits/pixel compressed RGB texture format.
			return 4;								
		case TextureFormat.BGRA32://	 Format returned by iPhone camera
			return 32;
#if !UNITY_5 && !UNITY_5_3_OR_NEWER
			case TextureFormat.ATF_RGB_DXT1://	 Flash-specific RGB DXT1 compressed color texture format.
			case TextureFormat.ATF_RGBA_JPG://	 Flash-specific RGBA JPG-compressed color texture format.
			case TextureFormat.ATF_RGB_JPG://	 Flash-specific RGB JPG-compressed color texture format.
			return 0; //Not supported yet  
#endif
		}
		return 0;
	}

	int CalculateTextureSizeBytes(Texture tTexture)
	{

		int tWidth=tTexture.width;
		int tHeight=tTexture.height;
		if (tTexture is Texture2D)
		{
			Texture2D tTex2D=tTexture as Texture2D;
			int bitsPerPixel=GetBitsPerPixel(tTex2D.format);
			int mipMapCount=tTex2D.mipmapCount;
			int mipLevel=1;
			int tSize=0;
			while (mipLevel<=mipMapCount)
			{
				tSize+=tWidth*tHeight*bitsPerPixel/8;
				tWidth=tWidth/2;
				tHeight=tHeight/2;
				mipLevel++;
			}
			return tSize;
		}
		if (tTexture is Texture2DArray)
		{
			Texture2DArray tTex2D=tTexture as Texture2DArray;
			int bitsPerPixel=GetBitsPerPixel(tTex2D.format);
			int mipMapCount=10;
			int mipLevel=1;
			int tSize=0;
			while (mipLevel<=mipMapCount)
			{
				tSize+=tWidth*tHeight*bitsPerPixel/8;
				tWidth=tWidth/2;
				tHeight=tHeight/2;
				mipLevel++;
			}
			return tSize*((Texture2DArray)tTex2D).depth;
		}
		if (tTexture is Cubemap) {
			Cubemap tCubemap = tTexture as Cubemap;
			int bitsPerPixel = GetBitsPerPixel (tCubemap.format);
			return tWidth * tHeight * 6 * bitsPerPixel / 8;
		}
		return 0;
	}



    void SelectObject(Object selectedObject,bool append)
	{
		if (append)
		{
			List<Object> currentSelection=new List<Object>(Selection.objects);
			// Allow toggle selection
			if (currentSelection.Contains(selectedObject)) currentSelection.Remove(selectedObject);
			else currentSelection.Add(selectedObject);

			Selection.objects=currentSelection.ToArray();
		}
		else Selection.activeObject=selectedObject;
	}

	void SelectObjects(List<Object> selectedObjects,bool append)
	{
		if (append)
		{
			List<Object> currentSelection=new List<Object>(Selection.objects);
			currentSelection.AddRange(selectedObjects);
			Selection.objects=currentSelection.ToArray();
		}
		else Selection.objects=selectedObjects.ToArray();
	}

    void SelectMaterials(List<Material> selectedMaterials, bool append)
    {
        if (append)
        {
            List<Material> currentSelection = new List<Material>();
            currentSelection.AddRange(selectedMaterials);
            Selection.objects = currentSelection.ToArray();
        }
        else Selection.objects = selectedMaterials.ToArray();
    }

    void ListTextures()
	{
		textureListScrollPos = EditorGUILayout.BeginScrollView(textureListScrollPos);
        List<Object> MipMapTextures = new List<Object>();
        List<Object> SuperLargeTextures = new List<Object>(); //Store Over 2048 Textures
        List<Object> ExtraLargeTextures = new List<Object>(); //Store 2048 Textures
        List<Object> LargeTextures = new List<Object>(); //Store 1024 Textures
        List<Object> MediumTextures = new List<Object>(); //Store 512 Textures
        List<Object> SmallTextures = new List<Object>(); //Store Below 512 Textures

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sort By Name", GUILayout.Width(200)))
        {
            SortTextureName();
        }

        if (GUILayout.Button("Sort By Format", GUILayout.Width(200)))
        {
            SortTextureFormat();
        }

        if (GUILayout.Button("Sort By Size", GUILayout.Width(200)))
        {
            SortTextureSize();
        }

        if (GUILayout.Button("Sort By Alpha", GUILayout.Width(200)))
        {
            SortTextureAlpha();
        }

        GUILayout.EndHorizontal();

        if (ActiveTextures.Count > 0)
        {
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            //GUILayout.Box(" ",GUILayout.Width(ThumbnailWidth),GUILayout.Height(ThumbnailHeight));
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                List<Object> AllTextures = new List<Object>();
                foreach (TextureDetails tDetails in ActiveTextures) AllTextures.Add(tDetails.texture);
                SelectObjects(AllTextures, ctrlPressed);
            }

            if (GUILayout.Button("Select 2048+", GUILayout.Width(100)))
            {
                SelectObjects(SuperLargeTextures, ctrlPressed);
            }
            if (GUILayout.Button("Select 2048", GUILayout.Width(100)))
            {
                SelectObjects(ExtraLargeTextures, ctrlPressed);
            }
            if (GUILayout.Button("Select 1024", GUILayout.Width(100)))
            {
                SelectObjects(LargeTextures, ctrlPressed);
            }
            if (GUILayout.Button("Select 512", GUILayout.Width(100)))
            {
                SelectObjects(MediumTextures, ctrlPressed);
            }
            if (GUILayout.Button("Select 512-", GUILayout.Width(100)))
            {
                SelectObjects(SmallTextures, ctrlPressed);
            }
            EditorGUILayout.EndHorizontal();
        }
        foreach (TextureDetails tDetails in ActiveTextures)
		{			

			GUILayout.BeginHorizontal ();
            
			Texture tex =tDetails.texture;			
			if(tDetails.texture.GetType() == typeof(Texture2DArray) || tDetails.texture.GetType() == typeof(Cubemap)){
				tex = AssetPreview.GetMiniThumbnail(tDetails.texture);
			}
			GUILayout.Box(tex, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

			if (tDetails.instance == true)
				GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
			if (tDetails.isgui == true)
				GUI.color = new Color (defColor.r, 0.95f, 0.8f, 1.0f);
			if (tDetails.isSky)
				GUI.color = new Color (0.9f, defColor.g, defColor.b, 1.0f);

            GUILayout.BeginVertical();

            if (GUILayout.Button(tDetails.texture.name,GUILayout.Width(158)))
			{
				SelectObject(tDetails.texture,ctrlPressed);
			}


            GUILayout.BeginHorizontal();
            GUILayout.Label("Uses- ", GUILayout.Width(35));
            if (GUILayout.Button(tDetails.FoundInMaterials.Count + " Mats", GUILayout.Width(55)))
            {
                SelectObjects(tDetails.FoundInMaterials, ctrlPressed);
            }

            HashSet<Object> FoundObjects = new HashSet<Object>();
            foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
            foreach (Animator animator in tDetails.FoundInAnimators) FoundObjects.Add(animator.gameObject);
            foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
            foreach (Button button in tDetails.FoundInButtons) FoundObjects.Add(button.gameObject);
            foreach (MonoBehaviour script in tDetails.FoundInScripts) FoundObjects.Add(script.gameObject);
            if (GUILayout.Button(FoundObjects.Count + " GOs", GUILayout.Width(60)))
            {
                SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Texture Import Setting: ", GUILayout.Width(150));
            GUILayout.EndVertical();

            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tDetails.texture)) as TextureImporter;
            string mobileFormat = "";
            int maxSize = 0;
            string sizeLabel = "";

            if (importer != null)
            {
                TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
                TextureImporterPlatformSettings iOSSettings = importer.GetPlatformTextureSettings("iPhone");


                string androidfileformat = "Android Max Size: " + androidSettings.maxTextureSize + "  Format:  " + androidSettings.format;

                if (!androidSettings.overridden)
                {
                    androidfileformat = "Android: Same as Default ";
                }

                string iOSfileformat = "iPhone Max Size: " + iOSSettings.maxTextureSize + "  Format:  " + iOSSettings.format;

                if (!iOSSettings.overridden)
                {
                    iOSfileformat = "iPhone: Same as Default";
                }
                mobileFormat = androidfileformat + "\n" + iOSfileformat;
                maxSize = importer.maxTextureSize;
                sizeLabel = "Change Max Size (Default: " + maxSize + ")";
            }



            if (importer != null && importer.textureType == TextureImporterType.Default)
            {
                if (importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.DoesSourceTextureHaveAlpha())
                {
                    tDetails.hasAlpha = true;
                }
                else
                {
                    tDetails.hasAlpha = false;
                }

                if (importer.DoesSourceTextureHaveAlpha())
                {
                    if(importer.alphaSource != TextureImporterAlphaSource.FromInput)
                    {
                        tDetails.hasAlphaWrongSetting = true;
                    }
                }
            }
      
            string alphaCheckBoolean = tDetails.hasAlpha ? "+ Alpha" : "";


            GUI.color = defColor;
            string textureAssetPath = AssetDatabase.GetAssetPath(tex);
            string fileformat;
            string cubemapDetail;
            bool streamingSettingCheck = false;
            bool generateMipmap = false;
            bool readWriteBoolean = false;







            if (importer != null && importer.textureType == TextureImporterType.NormalMap)
            {
                fileformat = Path.GetExtension(textureAssetPath).ToUpper().TrimStart('.') + " - Normal Map";
                fileformat += "\n" + FormatSizeString(tDetails.memSizeKB) + " - " + tDetails.format;
            }
            else
            {
                fileformat = Path.GetExtension(textureAssetPath).ToUpper().TrimStart('.') + " " + alphaCheckBoolean;
                fileformat += "\n" + FormatSizeString(tDetails.memSizeKB) + " - " + tDetails.format;
            }



            TextureImporter textureImporterSetting = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;

            cubemapDetail = "Size: (" + tDetails.texture.width + " x" + tDetails.texture.height;
            cubemapDetail += ")\n Cubemap  " + "\n" + FormatSizeString(tDetails.memSizeKB) + " - " + tDetails.format;



            if (tDetails.isCubeMap)
            {
                GUILayout.Label(cubemapDetail, GUILayout.Width(150));
            }
            else
            {
                GUILayout.BeginVertical();
                GUILayout.Label(fileformat, GUILayout.Width(150));
                if (textureImporterSetting != null)
                {
                    readWriteBoolean = textureImporterSetting.isReadable;
                    string readWriteBoolString = readWriteBoolean ? "O" : "X";

                    if (GUILayout.Button("Read Write: " + readWriteBoolString, GUILayout.Width(130)))
                    {
                        textureImporterSetting.isReadable = !textureImporterSetting.isReadable;
                        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                    }

                }
                GUILayout.EndVertical();
            }



            if (importer != null)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("Texture Size: (" + tDetails.texture.width + " x" + tDetails.texture.height + ")");
                GUILayout.Label(sizeLabel, GUILayout.Width(200));
                GUILayout.BeginHorizontal();
                GUILayout.Label("-", GUILayout.Width(10));
                if (GUILayout.Button("2048", GUILayout.Width(45)))
                {
                    textureImporterSetting.maxTextureSize = 2048;
                    AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                }
                if (GUILayout.Button("1024", GUILayout.Width(40)))
                {
                    textureImporterSetting.maxTextureSize = 1024;
                    AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                }

                if (GUILayout.Button("512", GUILayout.Width(35)))
                {
                    textureImporterSetting.maxTextureSize = 512;
                    AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                }
                if (GUILayout.Button("256", GUILayout.Width(35)))
                {
                    textureImporterSetting.maxTextureSize = 256;
                    AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                }
                GUILayout.Label("-", GUILayout.Width(10));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();




                if (textureImporterSetting != null)
                {
                    GUILayout.BeginVertical();
                    GUILayout.Label(mobileFormat, GUILayout.Width(300));
                    GUILayout.BeginHorizontal();
                    streamingSettingCheck = textureImporterSetting.streamingMipmaps;
                    generateMipmap = textureImporterSetting.mipmapEnabled;
                    string generateMipMapBoolean = generateMipmap ? "O" : "X";
                    string streamingSettingCheckBoolean = streamingSettingCheck ? "O" : "X";
                    string streamingSetting = " " + streamingSettingCheckBoolean;
                    if (tDetails.hasAlphaWrongSetting)
                    {
                        if (GUILayout.Button("Fix Alpha Setting: " + " Input Texture Alpha", GUILayout.Width(260)))
                        {
                            // Set alpha source to input texture alpha
                            textureImporterSetting.alphaSource = TextureImporterAlphaSource.FromInput;
                            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                            CheckResources();
                        }
                    }
                    else
                    {


                        if (GUILayout.Button("Generate Mipmap: " + generateMipMapBoolean, GUILayout.Width(130)))
                        {
                            textureImporterSetting.mipmapEnabled = !textureImporterSetting.mipmapEnabled;
                            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                        }

                        if (GUILayout.Button("Stream Mipmap: " + streamingSetting, GUILayout.Width(130)))
                        {
                            textureImporterSetting.streamingMipmaps = !textureImporterSetting.streamingMipmaps;
                            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                        }
                    }


                    if (tDetails.texture.width > 2048)
                        SuperLargeTextures.Add(tDetails.texture);

                    if (tDetails.texture.width == 2048)
                        ExtraLargeTextures.Add(tDetails.texture);

                    if (tDetails.texture.width == 1024)
                        LargeTextures.Add(tDetails.texture);

                    if (tDetails.texture.width == 512)
                        MediumTextures.Add(tDetails.texture);

                    if (tDetails.texture.width < 512)
                        SmallTextures.Add(tDetails.texture);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                }

                else if (!tDetails.isCubeMap)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Import Setting N/A", GUILayout.Width(260));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Import Setting: N/A", GUILayout.Width(260));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("\n\n\nTexture Import Setting N/A", GUILayout.Width(300));
                GUILayout.EndHorizontal();
            }
            

            


       

            GUILayout.EndHorizontal();

        }


            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();

        if (GUILayout.Button("Turn on Mipmap Stream", GUILayout.Width(250)))
        {
            foreach (TextureDetails tDetails in ActiveTextures)
            {
                Texture tex = tDetails.texture;
                string textureAssetPath = AssetDatabase.GetAssetPath(tex);
                TextureImporter textureImporterSetting = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
                if (textureImporterSetting != null)
                {
                    if (!textureImporterSetting.streamingMipmaps)
                    {
                        textureImporterSetting.streamingMipmaps = true;
                        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }


        if (GUILayout.Button("Turn off Mipmap Stream", GUILayout.Width(250)))
        {
            foreach (TextureDetails tDetails in ActiveTextures)
            {
                Texture tex = tDetails.texture;
                string textureAssetPath = AssetDatabase.GetAssetPath(tex);
                TextureImporter textureImporterSetting = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
                if (textureImporterSetting != null)
                {
                    if (textureImporterSetting.streamingMipmaps)
                    {
                        textureImporterSetting.streamingMipmaps = false;
                        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }

        EditorGUILayout.EndHorizontal();
        

        EditorGUILayout.EndScrollView();
	}

    void selectAll()
    {
        switch (ActiveInspectType)
        {
            case InspectType.Textures:
                if (ActiveTextures.Count > 0)
                {
                        List<Object> AllTextures = new List<Object>();
                        foreach (TextureDetails tDetails in ActiveTextures) AllTextures.Add(tDetails.texture);
                        SelectObjects(AllTextures, ctrlPressed);
                    
                }
                break;
            case InspectType.Materials:
                ListMaterials();
                break;
            case InspectType.Meshes:
                ListMeshes();
                break;
            case InspectType.Missing:
                ListMissing();
                break;
        }
    }


    void ListMaterials()
    {
        materialListScrollPos = EditorGUILayout.BeginScrollView(materialListScrollPos);

    
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sort By Shader", GUILayout.Width(200)))
        {
            SortMaterialShaderBrand();
            SortMaterialShader();
        }



        if (GUILayout.Button("Sort By Material", GUILayout.Width(200)))
        {
            SortMaterialName();
        }
        EditorGUILayout.EndHorizontal();
        foreach (MaterialDetails tDetails in ActiveMaterials)
        {
            if (tDetails.material != null)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.material), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

                if (tDetails.instance == true)
                    GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                if (tDetails.isgui == true)
                    GUI.color = new Color(defColor.r, 0.95f, 0.8f, 1.0f);
                if (tDetails.isSky)
                    GUI.color = new Color(0.9f, defColor.g, defColor.b, 1.0f);
                if (GUILayout.Button(tDetails.material.name, GUILayout.Width(150)))
                {
                    SelectObject(tDetails.material, ctrlPressed);
                }
                GUI.color = defColor;

                string shaderLabel = tDetails.material.shader != null ? tDetails.material.shader.name : "no shader";

                string shaderShort = GetShaderName(shaderLabel);

                string shaderOrigin = GetShaderOrigin(shaderLabel, '/');


                tDetails.shaderName = shaderShort;
                tDetails.shaderBrand = shaderOrigin;

                GUILayout.Label(shaderOrigin, GUILayout.Width(70));
                GUILayout.Label(shaderShort, GUILayout.Width(170));
                string GPUInstancingBoolean = tDetails.material.enableInstancing ? "O" : "X";

                if (GUILayout.Button("GPU Instancing:   " + GPUInstancingBoolean, GUILayout.Width(150)))
                {
                    tDetails.material.enableInstancing = !tDetails.material.enableInstancing;
                }
                GUILayout.Label(" ", GUILayout.Width(20));
                if (GUILayout.Button((tDetails.FoundInRenderers.Count + tDetails.FoundInGraphics.Count) + " GO", GUILayout.Width(50)))
                {
                    List<Object> FoundObjects = new List<Object>();
                    foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
                    foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
                    SelectObjects(FoundObjects, ctrlPressed);
                }

                GUILayout.Label("Render Queue: " + tDetails.material.renderQueue.ToString());
                GUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
    }
    void ListShader()
    {
        shaderListScrollPos = EditorGUILayout.BeginScrollView(shaderListScrollPos);
        printedMaterials = new HashSet<string>();

        foreach (MaterialDetails tDetails in ActiveMaterials)
        {
            int count = 0;
            if (tDetails.material != null)
            {
                GUILayout.BeginHorizontal();

                tDetails.shaderName = tDetails.material.shader.name;
                List<Material> FoundMaterials = new List<Material>();


                foreach (MaterialDetails material in ActiveMaterials)
                {
                    if (material.material.shader == tDetails.material.shader)
                    {
                        FoundMaterials.Add(material.material);
                        count++;
                    }
                }


                if (!printedMaterials.Contains(tDetails.shaderName))
                {
                    printedMaterials.Add(tDetails.shaderName);
                    if (GUILayout.Button(count.ToString() + " Materials", GUILayout.Width(100)))
                    {
                        SelectMaterials(FoundMaterials, ctrlPressed);
                    }
                    GUILayout.Label( " uses  ", GUILayout.Width(50));
                    if (GUILayout.Button(GetShaderName(tDetails.shaderName), GUILayout.Width(250)))
                    {
                        SelectMaterials(FoundMaterials, ctrlPressed);
                        {
                            Shader shader = tDetails.material.shader;
                            EditorGUIUtility.PingObject(shader);
                            Selection.activeObject = shader;
                        }
                    }
                }



                GUILayout.EndHorizontal();
            }

        }

        EditorGUILayout.EndScrollView();
    }


    public static string GetShaderName(string fullShaderName)
    {
        int lastSlashIndex = fullShaderName.LastIndexOf('/');
        if (lastSlashIndex >= 0 && lastSlashIndex < fullShaderName.Length - 1)
        {
            // Extract the shader name after the last slash
            return fullShaderName.Substring(lastSlashIndex + 1);
        }
        else
        {
            // No slash found or it is the last character
            return fullShaderName;
        }
    }
    public static string GetShaderOrigin(string fullShaderName, char character)
    {

            int index = fullShaderName.IndexOf(character);
            if (index >= 0)
            {
                // Extract the substring before the character
                return fullShaderName.Substring(0, index);
            }
            else
            {
                // Character not found, return the original string
                return fullShaderName;
            }
        
    }

    void ListMeshes()
	{
		meshListScrollPos = EditorGUILayout.BeginScrollView(meshListScrollPos);

		foreach (MeshDetails tDetails in ActiveMeshDetails)
		{			
			if (tDetails.mesh!=null)
			{
				GUILayout.BeginHorizontal ();
				string name = tDetails.mesh.name;
				if (name == null || name.Count() < 1)
					name = tDetails.FoundInMeshFilters[0].gameObject.name;
				if (tDetails.instance == true)
					GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				if(GUILayout.Button(name,GUILayout.Width(150)))
				{
					SelectObject(tDetails.mesh,ctrlPressed);
				}
				GUI.color = defColor;
				string sizeLabel=""+tDetails.mesh.vertexCount+" vert";

				GUILayout.Label (sizeLabel,GUILayout.Width(100));

                string IsFBX = CheckIfFromFBX(tDetails.mesh) ? "FBX" : "Not FBX";
                GUILayout.Label("  " + IsFBX, GUILayout.Width(100));

                if (GUILayout.Button(tDetails.FoundInMeshFilters.Count + " GO",GUILayout.Width(50)))
				{
					List<Object> FoundObjects=new List<Object>();
					foreach (MeshFilter meshFilter in tDetails.FoundInMeshFilters) FoundObjects.Add(meshFilter.gameObject);
					SelectObjects(FoundObjects,ctrlPressed);
				}

                GUILayout.Label("Export as ", GUILayout.Width(60));

                if (GUILayout.Button("FBX", GUILayout.Width(35)))
                {
#if UNITY_EDITOR
                    Mesh mesh = tDetails.mesh;
                        if (mesh != null)
                        {
                            string meshPath = AssetDatabase.GetAssetPath(mesh);
                            string folderPath = System.IO.Path.GetDirectoryName(meshPath);
                            string meshName = mesh.name;
                            string exportPath = folderPath + "/" + meshName + ".fbx";
                            ModelExporter.ExportObject(exportPath, CreateTemporaryObjectWithMesh(mesh));
                        }
#else
                    Debug.LogError("FBX Exporter plugin is required to export as FBX.");
#endif
                }

                if (tDetails.FoundInSkinnedMeshRenderer.Count > 0) {
					if (GUILayout.Button (tDetails.FoundInSkinnedMeshRenderer.Count + " skinned mesh GO", GUILayout.Width (140))) {
						List<Object> FoundObjects = new List<Object> ();
						foreach (SkinnedMeshRenderer skinnedMeshRenderer in tDetails.FoundInSkinnedMeshRenderer)
							FoundObjects.Add (skinnedMeshRenderer.gameObject);
						SelectObjects (FoundObjects, ctrlPressed);
					}
				} else {
					GUI.color = new Color (defColor.r, defColor.g, defColor.b, 0.5f);
					GUILayout.Label("   0 skinned mesh");
					GUI.color = defColor;
				}





                GUILayout.EndHorizontal();	
			}
		}
		EditorGUILayout.EndScrollView();		
	}
    public static bool IsPartOfPrefab(GameObject gameObject)
    {
        PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(gameObject);
        return prefabType != PrefabAssetType.NotAPrefab;
    }


    public bool CheckIfFromFBX(Mesh mesh)
    {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mesh);
            string fileExtension = System.IO.Path.GetExtension(assetPath);

            if (fileExtension == ".fbx")
            {
                return true;
            }
            else
            {
                return false;
            }
    }

    private static GameObject CreateTemporaryObjectWithMesh(Mesh mesh)
    {
        GameObject tempObject = new GameObject("TempObject");
        MeshFilter meshFilter = tempObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = tempObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = mesh;

        return tempObject;
    }

    void ListMissing(){
		missingListScrollPos = EditorGUILayout.BeginScrollView(missingListScrollPos);
		foreach (MissingGraphic dMissing in MissingObjects) {
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button (dMissing.name, GUILayout.Width (150)))
				SelectObject (dMissing.Object, ctrlPressed);
			GUILayout.Label ("missing ", GUILayout.Width(48));
			switch (dMissing.type) {
			case "lod":
				GUI.color = new Color(defColor.r, defColor.b, 0.8f, 1.0f);
				break;
			case "mesh":
				GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				break;
			case "sprite":
				GUI.color = new Color (defColor.r, 0.8f, 0.8f, 1.0f);
				break;
			case "material":
				GUI.color = new Color (0.8f, defColor.g, 0.8f, 1.0f);
				break;
			}
			GUILayout.Label (dMissing.type);
			GUI.color = defColor;
			GUILayout.EndHorizontal ();
		}
		EditorGUILayout.EndScrollView();
	}

	string FormatSizeString(int memSizeKB)
	{
		if (memSizeKB<1024) return ""+memSizeKB+"k";
		else
		{
			float memSizeMB=((float)memSizeKB)/1024.0f;
			return memSizeMB.ToString("0.00")+"Mb";
		}
	}


	TextureDetails FindTextureDetails(Texture tTexture)
	{
		foreach (TextureDetails tTextureDetails in ActiveTextures)
		{
			if (tTextureDetails.texture==tTexture) return tTextureDetails;
		}
		return null;

	}

	MaterialDetails FindMaterialDetails(Material tMaterial)
	{
		foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
		{
			if (tMaterialDetails.material==tMaterial) return tMaterialDetails;
		}
		return null;

	}

	MeshDetails FindMeshDetails(Mesh tMesh)
	{
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails)
		{
			if (tMeshDetails.mesh==tMesh) return tMeshDetails;
		}
		return null;

	}


	void CheckResources()
	{
		ActiveTextures.Clear();
		ActiveMaterials.Clear();
		ActiveMeshDetails.Clear();
		MissingObjects.Clear ();
  
		thingsMissing = false;

		Renderer[] renderers = FindObjects<Renderer>();

		MaterialDetails skyMat = new MaterialDetails ();

        HashSet<Shader> uniqueShaders = new HashSet<Shader>();

        skyMat.material = RenderSettings.skybox;
		skyMat.isSky = true;
		ActiveMaterials.Add (skyMat);

		//Debug.Log("Total renderers "+renderers.Length);
		foreach (Renderer renderer in renderers)
		{
			//Debug.Log("Renderer is "+renderer.name);
			foreach (Material material in renderer.sharedMaterials)
			{

				MaterialDetails tMaterialDetails = FindMaterialDetails(material);
				if (tMaterialDetails == null)
				{
					tMaterialDetails = new MaterialDetails();
					tMaterialDetails.material = material;
					ActiveMaterials.Add(tMaterialDetails);
                    

				}

                if (material != null)
                {
                    uniqueShaders.Add(material.shader);
                }
				tMaterialDetails.FoundInRenderers.Add(renderer);
			}

            shaderCount = uniqueShaders.Count;

			if (renderer is SpriteRenderer)
			{
				SpriteRenderer tSpriteRenderer = (SpriteRenderer)renderer;

				if (tSpriteRenderer.sprite != null) {
					var tSpriteTextureDetail = GetTextureDetail (tSpriteRenderer.sprite.texture, renderer);
					if (!ActiveTextures.Contains (tSpriteTextureDetail)) {
						ActiveTextures.Add (tSpriteTextureDetail);
					}
				} else if (tSpriteRenderer.sprite == null) {
					MissingGraphic tMissing = new MissingGraphic ();
					tMissing.Object = tSpriteRenderer.transform;
					tMissing.type = "sprite";
					tMissing.name = tSpriteRenderer.transform.name;
					MissingObjects.Add (tMissing);
					thingsMissing = true;
				}
			}
		}

		if (IncludeGuiElements)
		{
			Graphic[] graphics = FindObjects<Graphic>();

			foreach(Graphic graphic in graphics)
			{
				if (graphic.mainTexture)
				{
					var tSpriteTextureDetail = GetTextureDetail(graphic.mainTexture, graphic);
					if (!ActiveTextures.Contains(tSpriteTextureDetail))
					{
						ActiveTextures.Add(tSpriteTextureDetail);
					}
				}

				if (graphic.materialForRendering)
				{
					MaterialDetails tMaterialDetails = FindMaterialDetails(graphic.materialForRendering);
					if (tMaterialDetails == null)
					{
						tMaterialDetails = new MaterialDetails();
						tMaterialDetails.material = graphic.materialForRendering;
						tMaterialDetails.isgui = true;
						ActiveMaterials.Add(tMaterialDetails);
					}
					tMaterialDetails.FoundInGraphics.Add(graphic);
				}
			}

			Button[] buttons = FindObjects<Button>();
			foreach (Button button in buttons)
			{
				CheckButtonSpriteState(button, button.spriteState.disabledSprite);
				CheckButtonSpriteState(button, button.spriteState.highlightedSprite);
				CheckButtonSpriteState(button, button.spriteState.pressedSprite);
			}
		}

		foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
		{
			Material tMaterial = tMaterialDetails.material;
			if (tMaterial != null)
			{
				var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
				foreach (Object obj in dependencies)
				{
					if (obj is Texture)
					{
						Texture tTexture = obj as Texture;
						var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMaterialDetails);
						tTextureDetail.isSky = tMaterialDetails.isSky;
						tTextureDetail.instance = tMaterialDetails.instance;
						tTextureDetail.isgui = tMaterialDetails.isgui;
						ActiveTextures.Add(tTextureDetail);
					}
				}

				//if the texture was downloaded, it won't be included in the editor dependencies
				if (tMaterial.HasProperty ("_MainTex")) {
					if (tMaterial.mainTexture != null && !dependencies.Contains (tMaterial.mainTexture)) {
						var tTextureDetail = GetTextureDetail (tMaterial.mainTexture, tMaterial, tMaterialDetails);
						ActiveTextures.Add (tTextureDetail);
					}
				}
			}
		}


		MeshFilter[] meshFilters = FindObjects<MeshFilter>();

		foreach (MeshFilter tMeshFilter in meshFilters)
		{
			Mesh tMesh = tMeshFilter.sharedMesh;
			if (tMesh != null)
			{
				MeshDetails tMeshDetails = FindMeshDetails(tMesh);
				if (tMeshDetails == null)
				{
					tMeshDetails = new MeshDetails();
					tMeshDetails.mesh = tMesh;
					ActiveMeshDetails.Add(tMeshDetails);
				}
				tMeshDetails.FoundInMeshFilters.Add(tMeshFilter);
			} else if (tMesh == null && tMeshFilter.transform.GetComponent("TextContainer")== null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tMeshFilter.transform;
				tMissing.type = "mesh";
				tMissing.name = tMeshFilter.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}

			var meshRenderrer = tMeshFilter.transform.GetComponent<MeshRenderer>();
				
			if (meshRenderrer == null || meshRenderrer.sharedMaterial == null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tMeshFilter.transform;
				tMissing.type = "material";
				tMissing.name = tMeshFilter.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}
		}

		SkinnedMeshRenderer[] skinnedMeshRenderers = FindObjects<SkinnedMeshRenderer>();

		foreach (SkinnedMeshRenderer tSkinnedMeshRenderer in skinnedMeshRenderers)
		{
			Mesh tMesh = tSkinnedMeshRenderer.sharedMesh;
			if (tMesh != null)
			{
				MeshDetails tMeshDetails = FindMeshDetails(tMesh);
				if (tMeshDetails == null)
				{
					tMeshDetails = new MeshDetails();
					tMeshDetails.mesh = tMesh;
					ActiveMeshDetails.Add(tMeshDetails);
				}
				tMeshDetails.FoundInSkinnedMeshRenderer.Add(tSkinnedMeshRenderer);
			} else if (tMesh == null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tSkinnedMeshRenderer.transform;
				tMissing.type = "mesh";
				tMissing.name = tSkinnedMeshRenderer.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}
			if (tSkinnedMeshRenderer.sharedMaterial == null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tSkinnedMeshRenderer.transform;
				tMissing.type = "material";
				tMissing.name = tSkinnedMeshRenderer.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}
		}

		LODGroup[] lodGroups = FindObjects<LODGroup>();

		// Check if any LOD groups have no renderers
		foreach (var group in lodGroups)
		{
			var lods = group.GetLODs();
			for (int i = 0, l = lods.Length; i < l; i++)
			{
				if (lods[i].renderers.Length == 0)
				{
					MissingGraphic tMissing = new MissingGraphic();
					tMissing.Object = group.transform;
					tMissing.type = "lod";
					tMissing.name = group.transform.name;
					MissingObjects.Add(tMissing);
					thingsMissing = true;
				}
			}
		}


		if (IncludeSpriteAnimations)
		{
			Animator[] animators = FindObjects<Animator>();
			foreach (Animator anim in animators)
			{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
				UnityEditorInternal.AnimatorController ac = anim.runtimeAnimatorController as UnityEditorInternal.AnimatorController;
#elif UNITY_5 || UNITY_5_3_OR_NEWER
				UnityEditor.Animations.AnimatorController ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
#endif

				//Skip animators without layers, this can happen if they don't have an animator controller.
				if (!ac || ac.layers == null || ac.layers.Length == 0)
					continue;

				for (int x = 0; x < anim.layerCount; x++)
				{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
					UnityEditorInternal.StateMachine sm = ac.GetLayer(x).stateMachine;
					int cnt = sm.stateCount;
#elif UNITY_5 || UNITY_5_3_OR_NEWER
					UnityEditor.Animations.AnimatorStateMachine sm = ac.layers[x].stateMachine;
					int cnt = sm.states.Length;
#endif

					for (int i = 0; i < cnt; i++)
					{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
						UnityEditorInternal.State state = sm.GetState(i);
						Motion m = state.GetMotion();
#elif UNITY_5 || UNITY_5_3_OR_NEWER
						UnityEditor.Animations.AnimatorState state = sm.states[i].state;
						Motion m = state.motion;
#endif
                        if (m != null)
						{
							AnimationClip clip = m as AnimationClip;

						    if (clip != null)
						    {
						        EditorCurveBinding[] ecbs = AnimationUtility.GetObjectReferenceCurveBindings(clip);

						        foreach (EditorCurveBinding ecb in ecbs)
						        {
						            if (ecb.propertyName == "m_Sprite")
						            {
						                foreach (ObjectReferenceKeyframe keyframe in AnimationUtility.GetObjectReferenceCurve(clip, ecb))
						                {
						                    Sprite tSprite = keyframe.value as Sprite;

						                    if (tSprite != null)
						                    {
						                        var tTextureDetail = GetTextureDetail(tSprite.texture, anim);
						                        if (!ActiveTextures.Contains(tTextureDetail))
						                        {
						                            ActiveTextures.Add(tTextureDetail);
						                        }
						                    }
						                }
						            }
						        }
						    }
						}
					}
				}

			}
		}

		if (IncludeScriptReferences)
		{
			MonoBehaviour[] scripts = FindObjects<MonoBehaviour>();
			foreach (MonoBehaviour script in scripts)
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.Instance; // only public non-static fields are bound to by Unity.
				FieldInfo[] fields = script.GetType().GetFields(flags);

				foreach (FieldInfo field in fields)
				{
					System.Type fieldType = field.FieldType;
					if (fieldType == typeof(Sprite))
					{
						Sprite tSprite = field.GetValue(script) as Sprite;
						if (tSprite != null)
						{
							var tSpriteTextureDetail = GetTextureDetail(tSprite.texture, script);
							if (!ActiveTextures.Contains(tSpriteTextureDetail))
							{
								ActiveTextures.Add(tSpriteTextureDetail);
							}
						}
					}if (fieldType == typeof(Mesh))
					{
						Mesh tMesh = field.GetValue(script) as Mesh;
						if (tMesh != null)
						{
							MeshDetails tMeshDetails = FindMeshDetails(tMesh);
							if (tMeshDetails == null)
							{
								tMeshDetails = new MeshDetails();
								tMeshDetails.mesh = tMesh;
								tMeshDetails.instance = true;
								ActiveMeshDetails.Add(tMeshDetails);
							}
						}
					}if (fieldType == typeof(Material))
					{
						Material tMaterial = field.GetValue(script) as Material;
						if (tMaterial != null)
						{
							MaterialDetails tMatDetails = FindMaterialDetails(tMaterial);
							if (tMatDetails == null)
							{
								tMatDetails = new MaterialDetails();
								tMatDetails.instance = true;
								tMatDetails.material = tMaterial;
								if(!ActiveMaterials.Contains(tMatDetails))
									ActiveMaterials.Add(tMatDetails);
							}
							if (tMaterial.mainTexture)
							{
								var tSpriteTextureDetail = GetTextureDetail(tMaterial.mainTexture);
								if (!ActiveTextures.Contains(tSpriteTextureDetail))
								{
									ActiveTextures.Add(tSpriteTextureDetail);
								}
							}
							var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
							foreach (Object obj in dependencies)
							{
								if (obj is Texture)
								{
									Texture tTexture = obj as Texture;
									var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMatDetails);
									if(!ActiveTextures.Contains(tTextureDetail))
										ActiveTextures.Add(tTextureDetail);
								}
							}
						}
					}
				}
			}
		}

		TotalTextureMemory = 0;
		foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

		TotalMeshVertices = 0;
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;

		// Sort by size, descending

		ActiveMeshDetails.Sort(delegate(MeshDetails details1, MeshDetails details2) { return details2.mesh.vertexCount - details1.mesh.vertexCount; });

        // Sort shader by name descending



        collectedInPlayingMode = Application.isPlaying;
	}

    public class ShaderNameComparer : IComparer<MaterialDetails>
    {
        public int Compare(MaterialDetails x, MaterialDetails y)
        {
            // Compare the names of the objects
            return string.Compare(x.shaderName, y.shaderName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ShaderBrandNameComparer : IComparer<MaterialDetails>
    {
        public int Compare(MaterialDetails x, MaterialDetails y)
        {
            // Compare the names of the objects
            return string.Compare(x.shaderBrand, y.shaderBrand, StringComparison.OrdinalIgnoreCase);
        }
    }
    public class ObjectNameComparer : IComparer<MaterialDetails>
    {
        public int Compare(MaterialDetails x, MaterialDetails y)
        {
            // Compare the names of the objects
            return string.Compare(x.material.name, y.material.name, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class TextureNameComparer : IComparer<TextureDetails>
    {
        public int Compare(TextureDetails x, TextureDetails y)
        {
            // Compare the names of the objects
            return string.Compare(x.texture.name, y.texture.name, StringComparison.OrdinalIgnoreCase);
        }
    }
    public class TextureFormatNameComparer : IComparer<TextureDetails>
    {
        public int Compare(TextureDetails x, TextureDetails y)
        {
            string textureAssetPathX = AssetDatabase.GetAssetPath(x.texture);
            string textureAssetPathY = AssetDatabase.GetAssetPath(y.texture);
            string fileformatX = Path.GetExtension(textureAssetPathX);
            string fileformatY = Path.GetExtension(textureAssetPathY);
            // Compare the names of the objects
            return string.Compare(fileformatX, fileformatY, StringComparison.OrdinalIgnoreCase);
        }
    }


    void SortTextureName()
    {
        ActiveTextures.Sort(new TextureNameComparer());
        ActiveTextures = ActiveTextures.Distinct().ToList();
    }

    void SortTextureSize()
    {
        ActiveTextures.Sort(delegate (TextureDetails details1, TextureDetails details2) { return details2.memSizeKB - details1.memSizeKB; });
        ActiveTextures = ActiveTextures.Distinct().ToList();
    }

    void SortTextureFormat()
    {
        ActiveTextures.Sort(new TextureFormatNameComparer());
        ActiveTextures = ActiveTextures.Distinct().ToList();
    }

    void SortTextureAlpha()
    {
        ActiveTextures.Sort((a, b) => b.hasAlpha.CompareTo(a.hasAlpha));
        ActiveTextures = ActiveTextures.Distinct().ToList();
    }
    void SortMaterialShader()
    {
        ActiveMaterials.Sort(new ShaderNameComparer());
        ActiveMaterials = ActiveMaterials.Distinct().ToList();
    }
    void SortMaterialShaderBrand()
    {
        ActiveMaterials.Sort(new ShaderBrandNameComparer());
        ActiveMaterials = ActiveMaterials.Distinct().ToList();
    }

    void SortMaterialName()
    {
        ActiveMaterials.Sort(new ObjectNameComparer());
        ActiveMaterials = ActiveMaterials.Distinct().ToList();
    }

    private void CheckButtonSpriteState(Button button, Sprite sprite) 
	{
		if (sprite == null) return;
		
		var texture = sprite.texture;
		var tButtonTextureDetail = GetTextureDetail(texture, button);
		if (!ActiveTextures.Contains(tButtonTextureDetail))
		{
			ActiveTextures.Add(tButtonTextureDetail);
		}
	}
	
    private static GameObject[] GetAllRootGameObjects()
    {
#if !UNITY_5 && !UNITY_5_3_OR_NEWER
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToArray();
#else
        List<GameObject> allGo = new List<GameObject>();
        for (int sceneIdx = 0; sceneIdx < UnityEngine.SceneManagement.SceneManager.sceneCount; ++sceneIdx){
            allGo.AddRange( UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIdx).GetRootGameObjects().ToArray() );
        }
        return allGo.ToArray();
#endif
    }

	private T[] FindObjects<T>() where T : Object
	{
		if (IncludeDisabledObjects) {
			List<T> meshfilters = new List<T> ();
			GameObject[] allGo = GetAllRootGameObjects();
			foreach (GameObject go in allGo) {
				Transform[] tgo = go.GetComponentsInChildren<Transform> (true).ToArray ();
				foreach (Transform tr in tgo) {
					if (tr.GetComponent<T> ())
						meshfilters.Add (tr.GetComponent<T> ());
				}
			}
			return (T[])meshfilters.ToArray ();
		}
		else
			return (T[])FindObjectsOfType(typeof(T));
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Material tMaterial, MaterialDetails tMaterialDetails)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInMaterials.Add(tMaterial);
		foreach (Renderer renderer in tMaterialDetails.FoundInRenderers)
		{
			if (!tTextureDetails.FoundInRenderers.Contains(renderer)) tTextureDetails.FoundInRenderers.Add(renderer);
		}
		return tTextureDetails;
	}





    private TextureDetails GetTextureDetail(Texture tTexture, Renderer renderer)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInRenderers.Add(renderer);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Animator animator)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInAnimators.Add(animator);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Graphic graphic)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInGraphics.Add(graphic);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, MonoBehaviour script)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInScripts.Add(script);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Button button) 
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		if (!tTextureDetails.FoundInButtons.Contains(button))
		{
			tTextureDetails.FoundInButtons.Add(button);
		}
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture)
	{
		TextureDetails tTextureDetails = FindTextureDetails(tTexture);
		if (tTextureDetails == null)
		{
			tTextureDetails = new TextureDetails();
			tTextureDetails.texture = tTexture;
			tTextureDetails.isCubeMap = tTexture is Cubemap;

			int memSize = CalculateTextureSizeBytes(tTexture);

			TextureFormat tFormat = TextureFormat.RGBA32;
			int tMipMapCount = 1;
			if (tTexture is Texture2D)
			{
				tFormat = (tTexture as Texture2D).format;
				tMipMapCount = (tTexture as Texture2D).mipmapCount;
			}
			if (tTexture is Cubemap)
			{
				tFormat = (tTexture as Cubemap).format;
				memSize = 8 * tTexture.height * tTexture.width;
			}
			if(tTexture is Texture2DArray){
				tFormat = (tTexture as Texture2DArray).format;
				tMipMapCount = 10;
			}

			tTextureDetails.memSizeKB = memSize / 1024;
			tTextureDetails.format = tFormat;
			tTextureDetails.mipMapCount = tMipMapCount;

		}

		return tTextureDetails;
	}

}
