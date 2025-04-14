// Advanced Resource Checker

using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR && ENABLE_FBX_EXPORTER
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


	string[] inspectToolbarStrings = { "Textures", "Materials", "Shaders", "Meshes", "Particles", "Audio" };

	enum InspectType 
	{
		Textures,Materials,Shaders,Meshes,Particles,Audio 
	};

	enum MaxTextureSize
	{
		_4096 = 4096,
		_2048 = 2048,
		_1024 = 1024,
		_512 = 512,
		_256 = 256,
		_128 = 128
	}

	public enum TextureSortOption
	{
		Name,               // Texture name A-Z
		MemoryUsage,        // Total memory size (KB)
		Resolution,         // Width x Height
		Format,             // Compression format like ASTC, ETC2, etc.
		AlphaChannel,       // Has alpha or not
		TextureType         // Texture2D, NormalMap, etc.
	}

	private TextureSortOption sortOption = TextureSortOption.Name;
	private string[] sortLabels = new string[]
	{
	"Name (A–Z)",
	"Memory Usage",
	"Resolution",
	"Format",
	"Has Alpha",
	"Texture Type"
	};

	private bool isSortAscending = false;

	private Dictionary<Texture, MaxTextureSize> sizeOverrides = new Dictionary<Texture, MaxTextureSize>();

	public enum ShaderRiskLevel
	{
		None,
		Low,
		Moderate,
		High
	}


	bool IncludeDisabledObjects =true;
	bool IncludeSpriteAnimations=true;
	bool IncludeScriptReferences=true;
	bool IncludeGuiElements=true;
	bool thingsMissing = false;

	InspectType ActiveInspectType=InspectType.Textures;
	MaxTextureSize currentMaxSize = MaxTextureSize._2048;

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


	int TotalTextureMemory=0;
	int TotalMeshVertices=0;
    int shaderCount = 0;
    bool shaderChecked;

    bool ctrlPressed=false;

	static int MinWidth=475;
	Color defColor;

	bool collectedInPlayingMode;

	[MenuItem ("Tools/AdvancedToolkit/Resource Checker")]
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
		GUILayout.Label("Textures " + ActiveTextures.Count + " - " + FormatSizeString(TotalTextureMemory));
		GUILayout.Label("Materials " + ActiveMaterials.Count);
		GUILayout.Label("Shaders " + shaderCount);
		GUILayout.Label("Meshes " + ActiveMeshDetails.Count + " - " + TotalMeshVertices + " verts");

		// Add particle and audio system counts
		int particleCount = FindObjects<ParticleSystem>().Length;
		int audioCount = FindObjects<AudioSource>().Length;
		GUILayout.Label("Particles " + particleCount);
		GUILayout.Label("Audio Sources " + audioCount);

		GUILayout.EndHorizontal();


		ctrlPressed = Event.current.control || Event.current.command;

		GUILayout.Space(10);
		int selectedTab = (int)ActiveInspectType;
		selectedTab = GUILayout.Toolbar(selectedTab, inspectToolbarStrings);
		ActiveInspectType = (InspectType)selectedTab;
		GUILayout.Space(10);

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
		case InspectType.Particles:
			ListParticles();
			break;
		case InspectType.Audio:
			ListAudios();
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

		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("Sort By", GUILayout.Width(50));

		EditorGUI.BeginChangeCheck();
		selectedSortOption = (TextureSortOption)EditorGUILayout.EnumPopup(selectedSortOption, GUILayout.Width(180));
		bool ascending = GUILayout.Toggle(isSortAscending, isSortAscending ? "▲" : "▼", "Button", GUILayout.Width(30));
		if (EditorGUI.EndChangeCheck() || ascending != isSortAscending)
		{
			isSortAscending = ascending;
			ApplyTextureSort(); // Re-apply sort whenever change occurs
		}
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();


		float viewWidth = EditorGUIUtility.currentViewWidth;
		float padding = 20f;
		int totalColumns = 5;
		float columnWidth = (viewWidth - ThumbnailWidth - padding) / (totalColumns - 1);

		foreach (TextureDetails tDetails in ActiveTextures)
		{
			GUILayout.BeginHorizontal();

			// COLUMN 1: Thumbnail
			Texture previewTex = tDetails.texture;
			if (previewTex is Texture2DArray || previewTex is Cubemap)
				previewTex = AssetPreview.GetMiniThumbnail(previewTex);
			GUILayout.Box(previewTex, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

			// COLUMN 2: Name + Usage
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));

			if (GUILayout.Button(tDetails.texture.name, GUILayout.Width(columnWidth)))
			{
				SelectObject(tDetails.texture, ctrlPressed);
			}

			GUILayout.Label($"{tDetails.FoundInMaterials.Count} Mats, {tDetails.FoundInRenderers.Count} Renderers");

			GUILayout.EndVertical();

			// COLUMN 3: Format Info
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));

			TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tDetails.texture)) as TextureImporter;
			if (importer != null)
			{
				// COLUMN 3A: Format Info
				string ext = Path.GetExtension(AssetDatabase.GetAssetPath(tDetails.texture)).ToUpper().TrimStart('.');
				string formatLabel = $"Format: {ext}\nMemory: {FormatSizeString(tDetails.memSizeKB)}\nUnity Format: {tDetails.format}";
				EditorGUILayout.HelpBox(formatLabel, MessageType.None);

			}
			else
			{
				GUILayout.Label("Import settings not available");
			}

			GUILayout.EndVertical();

			// COLUMN 4: Max Texture Size
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));

			if (importer != null)
			{
				TextureImporter textureImporterSetting = importer;

				MaxTextureSize currentSize = (MaxTextureSize)textureImporterSetting.maxTextureSize;
				MaxTextureSize selectedSize;

				// Check override first
				if (!sizeOverrides.TryGetValue(tDetails.texture, out selectedSize))
					selectedSize = currentSize;

				EditorGUILayout.HelpBox($"Current Resolution: {tDetails.texture.width} x {tDetails.texture.height}", MessageType.None);

				GUILayout.BeginHorizontal();
				selectedSize = (MaxTextureSize)EditorGUILayout.EnumPopup(selectedSize);
				sizeOverrides[tDetails.texture] = selectedSize;

					if (GUILayout.Button("Change Size"))
					{
						textureImporterSetting.maxTextureSize = (int)selectedSize;
						AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(tDetails.texture), ImportAssetOptions.ForceUpdate);
					}
				
				GUILayout.EndHorizontal();
			}



			GUILayout.EndVertical();



			GUILayout.BeginVertical(GUILayout.Width(columnWidth));

			// Material references
			if (GUILayout.Button($"Materials ({tDetails.FoundInMaterials.Count})", GUILayout.Width(columnWidth)))
			{
				SelectObjects(tDetails.FoundInMaterials, ctrlPressed);
			}
			HashSet<Object> FoundObjects = new HashSet<Object>();
			foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
			foreach (Animator animator in tDetails.FoundInAnimators) FoundObjects.Add(animator.gameObject);
			foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
			foreach (Button button in tDetails.FoundInButtons) FoundObjects.Add(button.gameObject);
			foreach (MonoBehaviour script in tDetails.FoundInScripts) FoundObjects.Add(script.gameObject);
			if (GUILayout.Button($"GameObjects ({FoundObjects.Count})", GUILayout.Width(columnWidth)))
			{
				SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
			}

			GUILayout.EndVertical();

		GUILayout.EndHorizontal();
		}

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
		// Columns
		float viewWidth = EditorGUIUtility.currentViewWidth;
		float padding = 20f;
		int totalColumns = 5;
		float columnWidth = (viewWidth - ThumbnailWidth - padding) / (totalColumns - 1);

		foreach (MaterialDetails mat in ActiveMaterials)
		{
			if (mat.material == null) continue;

			GUILayout.BeginHorizontal();

			// Column 1: Thumbnail
			GUILayout.Box(AssetPreview.GetAssetPreview(mat.material), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

			// Column 2: Material Name Only
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));

			// Material name button
			if (GUILayout.Button(mat.material.name, GUILayout.Width(columnWidth)))
			{
				SelectObject(mat.material, ctrlPressed);
			}

			GUILayout.EndVertical();


			// Column 3: Shader Info (Origin + Short Name)
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));
			string fullShaderName = mat.material.shader?.name ?? "None";
			string origin = GetShaderOrigin(fullShaderName, '/');
			string shortName = GetShaderName(fullShaderName);

			GUILayout.Label($"Origin: {origin}", EditorStyles.miniLabel);
			GUILayout.Label($"Shader: {shortName}", EditorStyles.miniLabel);
			GUILayout.EndVertical();


			// Column 4: Material Rendering Properties
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));

			Material matRef = mat.material;

			// Surface type based on render queue
			string surface = "Surface: ";
			if (matRef.renderQueue <= 2450) surface += "Opaque";
			else if (matRef.renderQueue <= 2500) surface += "Alpha Cutoff";
			else surface += "Transparent";

			// Render face using _Cull
			string face = "Render Face: ";
			if (matRef.HasProperty("_Cull"))
			{
				int cull = (int)matRef.GetFloat("_Cull");
				face += cull switch
				{
					0 => "Both Sides",
					1 => "Front Only",
					2 => "Back Only",
					_ => "Unknown"
				};
			}
			else
			{
				face += "Default";
			}

			// Combine into helpbox
			string info = $"{surface}\n{face}\nQueue: {matRef.renderQueue}";
			EditorGUILayout.HelpBox(info.Trim(), MessageType.None);

			GUILayout.EndVertical();



			// Column 5: GameObject References
			GUILayout.BeginVertical(GUILayout.Width(columnWidth));
			int totalUsage = mat.FoundInRenderers.Count + mat.FoundInGraphics.Count;
			if (GUILayout.Button($"Used In ({totalUsage}) Game Objects", GUILayout.Width(columnWidth)))
			{
				List<Object> found = new List<Object>();
				mat.FoundInRenderers.ForEach(r => found.Add(r.gameObject));
				mat.FoundInGraphics.ForEach(g => found.Add(g.gameObject));
				SelectObjects(found, ctrlPressed);
			}
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();
	}
	// Add this at the top of your class
	Dictionary<string, bool> shaderOriginFoldouts = new Dictionary<string, bool>();

	void ListShader()
	{
		shaderListScrollPos = EditorGUILayout.BeginScrollView(shaderListScrollPos);
		printedMaterials = new HashSet<string>();

		var groupedByShader = ActiveMaterials
			.Where(m => m.material != null && m.material.shader != null)
			.GroupBy(m => m.material.shader);

		// Group by shader origin (e.g., Universal, Custom)
		var shadersByOrigin = groupedByShader
			.GroupBy(g => GetShaderOrigin(g.Key.name, '/'))
			.OrderBy(g => g.Key);

		foreach (var originGroup in shadersByOrigin)
		{
			string origin = originGroup.Key;

			// Initialize foldout state
			if (!shaderOriginFoldouts.ContainsKey(origin))
				shaderOriginFoldouts[origin] = true;

			GUILayout.BeginVertical("box");
			GUILayout.Space(4);

			// Origin Header with Fold Toggle
			GUILayout.BeginHorizontal();
			shaderOriginFoldouts[origin] = GUILayout.Toggle(shaderOriginFoldouts[origin], shaderOriginFoldouts[origin] ? "▼" : "▶", "Label", GUILayout.Width(20));
			GUILayout.Label($"Shader Origin: {origin}", EditorStyles.boldLabel);
			GUILayout.EndHorizontal();

			GUILayout.Space(4);

			// If expanded, show shaders in this group
			if (shaderOriginFoldouts[origin])
			{
				foreach (var shaderGroup in originGroup.OrderBy(g => GetShaderName(g.Key.name)))
				{
					Shader shader = shaderGroup.Key;
					List<Material> materials = shaderGroup.Select(m => m.material).ToList();

					GUILayout.BeginVertical("box");
					GUILayout.Space(2);

					// Shader Info Header
					GUILayout.BeginHorizontal();

					GUILayout.BeginVertical();
					GUILayout.Label($"Shader: {GetShaderName(shader.name)}", EditorStyles.boldLabel);
					GUILayout.EndVertical();

					GUILayout.FlexibleSpace();

					GUILayout.BeginVertical(GUILayout.Width(180));
					GUILayout.Label($"Materials: {materials.Count}", EditorStyles.label);

					GUILayout.BeginHorizontal();
					if (GUILayout.Button("Select Shader"))
					{
						Selection.activeObject = shader;
						EditorGUIUtility.PingObject(shader);
					}
					if (GUILayout.Button("Select Materials"))
					{
						SelectMaterials(materials, ctrlPressed);
					}
					GUILayout.EndHorizontal();
					GUILayout.EndVertical();

					GUILayout.EndHorizontal();
					GUILayout.Space(4);

					// Static analysis
					EditorGUILayout.HelpBox(AnalyzeShaderStaticInfo(shader), MessageType.None);

					GUILayout.Space(6);
					GUILayout.EndVertical(); // Shader box
				}
			}

			GUILayout.Space(4);
			GUILayout.EndVertical(); // Origin box
			GUILayout.Space(10);
		}

		EditorGUILayout.EndScrollView();
	}

	private string AnalyzeShaderStaticInfo(Shader shader)
	{
		int passCount = shader.passCount;

		// Texture2D Properties
		int textureCount = 0;
		List<string> textureNames = new List<string>();

		int propertyCount = ShaderUtil.GetPropertyCount(shader);
		for (int i = 0; i < propertyCount; i++)
		{
			if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
			{
				string propName = ShaderUtil.GetPropertyName(shader, i);

				// Filter out internal/unwanted texture properties
				if (propName.Contains("unity") || propName.ToLower().Contains("lightmap") || propName.Length > 30)
					continue;

				textureCount++;
				textureNames.Add(propName);
			}
		}


		// Keywords
		string[] keywords = shader.keywordSpace.keywords.Select(k => k.name).ToArray();
		int multiCompileCount = keywords.Count(k => k.Contains("shader_feature") || k.Contains("multi_compile"));

		// Risk Indicators
		string riskText = "";

		riskText += $"Pass Count: {passCount} ";
		riskText += passCount > 2 ? "⚠️ Moderate\n" : "✅ Low\n";

		riskText += $"Texture2D Properties: {textureCount} ";
		riskText += textureCount > 3 ? "⚠️ Moderate\n" : "✅ Low\n";

		riskText += $"Keywords: {keywords.Length} ";
		riskText += keywords.Length > 10 ? "⚠️ High (many defines)\n" : "✅ Low\n";

		// Texture list output
		string texList = textureNames.Count > 0
			? $"Used Texture Props ({textureNames.Count}): " + string.Join(", ", textureNames)
			: "No visible texture properties";

		return texList + "\n\n" + riskText;
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
		GUILayout.Space(8);

		// ────── STATIC MESHES ──────
		GUILayout.Label("STATIC MESHES", EditorStyles.boldLabel);
		foreach (MeshDetails tDetails in ActiveMeshDetails)
		{
			if (tDetails.mesh == null || tDetails.FoundInMeshFilters.Count == 0) continue;

			GUILayout.BeginVertical("box");
			DrawMeshRow(tDetails, isSkinned: false);
			GUILayout.EndVertical();
			GUILayout.Space(4);
		}

		GUILayout.Space(12);

		// ────── SKINNED MESHES ──────
		GUILayout.Label("SKINNED MESHES", EditorStyles.boldLabel);
		foreach (MeshDetails tDetails in ActiveMeshDetails)
		{
			if (tDetails.mesh == null || tDetails.FoundInSkinnedMeshRenderer.Count == 0) continue;

			GUILayout.BeginVertical("box");
			DrawMeshRow(tDetails, isSkinned: true);
			GUILayout.EndVertical();
			GUILayout.Space(4);
		}

		EditorGUILayout.EndScrollView();
	}

	void DrawMeshRow(MeshDetails tDetails, bool isSkinned)
	{
		GUILayout.BeginHorizontal();

		// ── Mesh Name ──
		GUILayout.BeginVertical();
		GUILayout.Label("Mesh Name", EditorStyles.miniLabel);
		string meshName = string.IsNullOrEmpty(tDetails.mesh.name)
			? (tDetails.FoundInMeshFilters.Count > 0 ? tDetails.FoundInMeshFilters[0].gameObject.name : "Unnamed")
			: tDetails.mesh.name;

		if (GUILayout.Button(meshName, GUILayout.Width(200)))
		{
			SelectObject(tDetails.mesh, ctrlPressed);
		}
		GUILayout.EndVertical();

		// ── Vertex Count ──
		GUILayout.BeginVertical();
		GUILayout.Label("Vertex Count", EditorStyles.miniLabel);
		GUILayout.Label($"{tDetails.mesh.vertexCount} verts", GUILayout.Width(100));
		GUILayout.EndVertical();

		// ── Model Type ──
		GUILayout.BeginVertical();
		GUILayout.Label("Model Type", EditorStyles.miniLabel);
		string modelType = CheckIfFromFBX(tDetails.mesh) ? "FBX" : "Not FBX";
		GUILayout.Label(modelType, GUILayout.Width(80));
		GUILayout.EndVertical();

		// ── Scene Object Button ──
		GUILayout.BeginVertical();
		GUILayout.Label("Scene Objects", EditorStyles.miniLabel);
		int count = isSkinned ? tDetails.FoundInSkinnedMeshRenderer.Count : tDetails.FoundInMeshFilters.Count;
		if (GUILayout.Button($"{count} Objects", GUILayout.Width(110)))
		{
			var objects = isSkinned
				? tDetails.FoundInSkinnedMeshRenderer.Select(r => (Object)r.gameObject).ToList()
				: tDetails.FoundInMeshFilters.Select(f => (Object)f.gameObject).ToList();

			SelectObjects(objects, ctrlPressed);
		}
		GUILayout.EndVertical();

		// ── Export Button ──
		GUILayout.BeginVertical();
		GUILayout.Label("Export", EditorStyles.miniLabel);
		if (GUILayout.Button("Export FBX", GUILayout.Width(90)))
		{
#if UNITY_EDITOR && ENABLE_FBX_EXPORTER
		GameObject source = isSkinned && tDetails.FoundInSkinnedMeshRenderer.Count > 0
			? tDetails.FoundInSkinnedMeshRenderer[0].gameObject
			: (tDetails.FoundInMeshFilters.Count > 0 ? tDetails.FoundInMeshFilters[0].gameObject : null);

		if (source != null)
		{
			string meshPath = AssetDatabase.GetAssetPath(tDetails.mesh);
			string folder = Path.GetDirectoryName(meshPath);
			string exportPath = $"{folder}/{source.name}_exported.fbx";
			ModelExporter.ExportObject(exportPath, CreateTemporaryObjectWithMesh(tDetails.mesh));
		}
#endif
		}
		GUILayout.EndVertical();

		// ── Prefab Reference ──
		GameObject firstObj = isSkinned
			? (tDetails.FoundInSkinnedMeshRenderer.Count > 0 ? tDetails.FoundInSkinnedMeshRenderer[0].gameObject : null)
			: (tDetails.FoundInMeshFilters.Count > 0 ? tDetails.FoundInMeshFilters[0].gameObject : null);

		if (firstObj != null && PrefabUtility.GetPrefabAssetType(firstObj) != PrefabAssetType.NotAPrefab)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Prefab", EditorStyles.miniLabel);
			if (GUILayout.Button("Select Prefab", GUILayout.Width(100)))
			{
				GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(firstObj);
				if (prefab != null)
				{
					Selection.activeObject = prefab;
					EditorGUIUtility.PingObject(prefab);
				}
			}
			GUILayout.EndVertical();
		}

		GUILayout.EndHorizontal();
	}

	void ListParticles()
	{
		Vector2 scrollPos = Vector2.zero;
		scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

		ParticleSystem[] allParticles = FindObjects<ParticleSystem>();
		GUILayout.Label($"Particle Systems Found: {allParticles.Length}", EditorStyles.boldLabel);
		GUILayout.Space(8);

		foreach (var ps in allParticles)
		{
			if (ps == null) continue;
			var main = ps.main;
			var emission = ps.emission;
			var shape = ps.shape;

			GUILayout.BeginVertical("box");

			GUILayout.BeginHorizontal();

			// Column 1: GameObject name
			GUILayout.BeginVertical(GUILayout.Width(200));
			GUILayout.Label("GameObject", EditorStyles.miniBoldLabel);
			GUILayout.Label(ps.gameObject.name, GUILayout.Width(180));
			GUILayout.EndVertical();

			// Column 2: Core Settings
			GUILayout.BeginVertical(GUILayout.Width(250));
			GUILayout.Label("Settings", EditorStyles.miniBoldLabel);
			GUILayout.Label($"Duration: {main.duration}s | Looping: {main.loop}");
			GUILayout.Label($"Max Particles: {main.maxParticles}");
			GUILayout.Label($"Start Lifetime: {main.startLifetime.constant}");
			GUILayout.EndVertical();

			// Column 3: Emission / Shape Info
			GUILayout.BeginVertical(GUILayout.Width(220));
			GUILayout.Label("Emission / Shape", EditorStyles.miniBoldLabel);
			GUILayout.Label($"Rate: {(emission.enabled ? emission.rateOverTime.constant : 0)} / sec");
			GUILayout.Label($"Shape: {(shape.enabled ? shape.shapeType.ToString() : "Disabled")}");
			GUILayout.EndVertical();

			// Column 4: Select Button
			GUILayout.BeginVertical(GUILayout.Width(120));
			GUILayout.Label("Action", EditorStyles.miniBoldLabel);
			if (GUILayout.Button("Select GO"))
			{
				Selection.activeObject = ps.gameObject;
				EditorGUIUtility.PingObject(ps.gameObject);
			}
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.Space(4);
		}

		EditorGUILayout.EndScrollView();
	}


	void ListAudios()
	{
		Vector2 scrollPos = Vector2.zero;
		scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

		AudioSource[] audioSources = FindObjects<AudioSource>();

		GUILayout.Label($"Audio Sources Found: {audioSources.Length}", EditorStyles.boldLabel);
		GUILayout.Space(8);

		foreach (var source in audioSources)
		{
			if (source == null) continue;

			GUILayout.BeginVertical("box");

			GUILayout.BeginHorizontal();

			// Audio Clip
			GUILayout.BeginVertical(GUILayout.Width(200));
			GUILayout.Label("Audio Clip", EditorStyles.miniBoldLabel);
			string clipName = source.clip ? source.clip.name : "(None)";
			GUILayout.Label(clipName, GUILayout.Width(180));
			GUILayout.EndVertical();

			// Volume / Loop / PlayOnAwake
			GUILayout.BeginVertical(GUILayout.Width(200));
			GUILayout.Label("Settings", EditorStyles.miniBoldLabel);
			GUILayout.Label($"Volume: {source.volume:F2}");
			GUILayout.Label($"Loop: {(source.loop ? "Yes" : "No")}, PlayOnAwake: {(source.playOnAwake ? "Yes" : "No")}");
			GUILayout.EndVertical();

			// Spatial Blend / 3D
			GUILayout.BeginVertical(GUILayout.Width(150));
			GUILayout.Label("3D Settings", EditorStyles.miniBoldLabel);
			string blend = source.spatialBlend == 0 ? "2D" : "3D";
			GUILayout.Label($"Spatial Blend: {blend}");
			GUILayout.Label($"Priority: {source.priority}");
			GUILayout.EndVertical();

			// Select GO Button
			GUILayout.BeginVertical(GUILayout.Width(120));
			GUILayout.Label("Object", EditorStyles.miniBoldLabel);
			if (GUILayout.Button("Select GO"))
			{
				Selection.activeObject = source.gameObject;
				EditorGUIUtility.PingObject(source.gameObject);
			}
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.Space(4);
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
							if (tMaterial.HasProperty("_MainTex") && tMaterial.mainTexture != null)

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
	public class MeshNameComparer : IComparer<MeshDetails>
	{
		public int Compare(MeshDetails x, MeshDetails y)
		{
			// Compare the names of the objects
			return string.Compare(x.FoundInMeshFilters[0].gameObject.name, y.FoundInMeshFilters[0].gameObject.name, StringComparison.OrdinalIgnoreCase);
		}
	}
	public class MeshSizeComparer : IComparer<MeshDetails>
	{
		public int Compare(MeshDetails x, MeshDetails y)
		{
			// Compare the names of the objects
			return string.Compare(x.mesh.name, y.mesh.name, StringComparison.OrdinalIgnoreCase);
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


	private TextureSortOption selectedSortOption = TextureSortOption.MemoryUsage;

	void ApplyTextureSort()
	{
		Comparison<TextureDetails> comparison = null;

		switch (selectedSortOption)
		{
			case TextureSortOption.Name:
				comparison = (a, b) => string.Compare(a.texture.name, b.texture.name, StringComparison.OrdinalIgnoreCase);
				break;
			case TextureSortOption.MemoryUsage:
				comparison = (a, b) => a.memSizeKB.CompareTo(b.memSizeKB);
				break;
			case TextureSortOption.Resolution:
				comparison = (a, b) =>
					(a.texture.width * a.texture.height).CompareTo(b.texture.width * b.texture.height);
				break;
			case TextureSortOption.Format:
				comparison = (a, b) => a.format.ToString().CompareTo(b.format.ToString());
				break;
			case TextureSortOption.AlphaChannel:
				comparison = (a, b) => a.hasAlpha.CompareTo(b.hasAlpha);
				break;
			case TextureSortOption.TextureType:
				comparison = (a, b) => a.texture.GetType().Name.CompareTo(b.texture.GetType().Name);
				break;
		}

		if (comparison != null)
		{
			ActiveTextures.Sort((a, b) => isSortAscending ? comparison(a, b) : comparison(b, a));
			ActiveTextures = ActiveTextures.Distinct().ToList();
		}
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

	void SortMeshName()
    {
		ActiveMeshDetails.Sort(new MeshNameComparer());
		ActiveMeshDetails = ActiveMeshDetails.Distinct().ToList();
	}
	void SortMeshSize()
	{
		ActiveMeshDetails.Sort(delegate (MeshDetails details1, MeshDetails details2) { return details2.mesh.vertexCount - details1.mesh.vertexCount; });
		ActiveMeshDetails = ActiveMeshDetails.Distinct().ToList();
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
